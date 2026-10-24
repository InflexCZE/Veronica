// The engine operations wq.dll exposes. Each takes the native operation
// first and returns a promise; streamed output goes back through __host.
;(function (g) {
  'use strict'
  const host = g.__host
  let engine = null
  let slot = null

  // Operations run one after another: web-llm does not guard a reload
  // against a concurrent request, or a request against a reload.
  let tail = Promise.resolve()
  const serial = (run) => {
    const result = tail.then(run)
    tail = result.then(
      () => undefined,
      () => undefined,
    )
    return result
  }

  // web-llm clears its interrupt flag when a request takes the pipeline, so
  // an interrupt that arrives before generation starts is re-issued.
  let chatRunning = false
  let interruptRequested = false

  const sriFromHex = (hex) => {
    let binary = ''
    for (let i = 0; i < hex.length; i += 2) binary += String.fromCharCode(parseInt(hex.slice(i, i + 2), 16))
    return 'sha256-' + btoa(binary)
  }

  const loaded = () => {
    if (engine === null || slot === null) throw new Error('no model is loaded')
    return engine
  }

  const load = async (op, m) => {
    const webllm = g.__webllm
    if (engine === null) engine = new webllm.MLCEngine({ logLevel: 'INFO' })
    slot = null
    engine.setAppConfig({
      model_list: [
        {
          model: m.modelUrl,
          model_id: m.modelId,
          model_lib: m.libUrl,
          required_features: m.requiredFeatures,
          integrity: { model_lib: sriFromHex(m.libSha256), onFailure: 'error' },
        },
      ],
    })
    engine.setInitProgressCallback((report) => host.progress(op, report.progress, report.text))
    try {
      await engine.reload(m.modelId, m.contextWindowSize > 0 ? { context_window_size: m.contextWindowSize } : undefined)
    } finally {
      engine.setInitProgressCallback(undefined)
    }
    slot = await engine.allocateSlot()
  }

  const chat = async (op, r) => {
    const e = loaded()
    const request = { messages: r.messages, stream: true, stream_options: { include_usage: true }, slot }
    if (r.maxTokens > 0) request.max_tokens = r.maxTokens
    if (!Number.isNaN(r.temperature)) request.temperature = r.temperature
    if (!Number.isNaN(r.topP)) request.top_p = r.topP
    if (r.seed >= 0) request.seed = r.seed
    chatRunning = true
    try {
      const stream = await e.chat.completions.create(request)
      let usage = null
      let finishReason = null
      for await (const chunk of stream) {
        if (interruptRequested) e.interruptGenerate()
        const choice = chunk.choices && chunk.choices[0]
        if (choice) {
          const delta = choice.delta || {}
          if (delta.reasoning_content) host.delta(op, 1, delta.reasoning_content)
          if (delta.content) host.delta(op, 0, delta.content)
          if (choice.finish_reason) finishReason = choice.finish_reason
        }
        if (chunk.usage) usage = chunk.usage
      }
      const extra = (usage && usage.extra) || {}
      return {
        promptTokens: usage ? usage.prompt_tokens : 0,
        completionTokens: usage ? usage.completion_tokens : 0,
        prefillTokensPerS: extra.prefill_tokens_per_s,
        decodeTokensPerS: extra.decode_tokens_per_s,
        finishReason,
      }
    } finally {
      chatRunning = false
      interruptRequested = false
    }
  }

  g.__wq = {
    load: (op, m) => serial(() => load(op, m)),
    chat: (op, r) => serial(() => chat(op, r)),
    reset: (op) => serial(() => loaded().resetChat(slot)),
    score: (op, tokens, topK) =>
      serial(async () => {
        const s = await loaded().forwardTokensAndScore(slot, tokens, topK)
        return { argmax: s.argmax, vocabSize: s.vocabSize, top: s.top }
      }),
    // Not serialized: it must reach the running chat.
    interrupt() {
      if (engine === null || !chatRunning) return
      interruptRequested = true
      engine.interruptGenerate()
    },
  }
})(globalThis)
