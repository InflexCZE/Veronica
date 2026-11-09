# OpenAI-compatible provider

Any server that speaks OpenAI chat completions, local or remote. The primary target is local servers such as llama.cpp.

- One streamed request per round, run by the client tool loop (`Agent.md`).
- Selected by settings (`Distribution.md`). Without them the local model is the provider.
- The Editor keeps its VRAM: the server may be remote or on another GPU.
- Any server certificate is accepted: locally hosted servers are typically self-signed.

## Request

- The body is plain chat completions plus llama.cpp's extensions.
- Thinking has its own token budget on top of the answer's, so a long think cannot cut a tool call mid-arguments; `max_tokens` is the sum. The server closes the thinking at the budget and the model still answers.
- The reasoning effort goes to the chat template, not the API: models whose template reads it get it, others ignore it. It is the one generation setting the client sends, because evals compare it per run, which would otherwise need a server restart per value.
- Each assistant message sends its reasoning back as `reasoning_content`, which thinking models need (`Agent.md`, Conversation).

## Replies

- Reasoning is read from either of the two names servers use for it.
- A prompt refused as too long, and a reply that ends as `length` short of the tokens it was sent, are reported as the context wall (`Compaction.md`).
- Round stats come from llama.cpp's timings; other servers show none.
