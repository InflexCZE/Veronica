# OpenAI-compatible provider: server quirks not handled

**Exists:** `Source/Veronica.Agent/OpenAi/OpenAiProvider.cs`. It handles standard SSE, llama.cpp and OpenRouter error members, a 200 response without an event stream, and lines it cannot parse.

**Missing:**
- Errors sent as an `error:` line or after `event: error` are skipped, and the round ends empty.
- When tool-call fragments carry no `index`, the fallback is the position inside one chunk. A server that streams several calls this way, one per chunk, merges them into one.
- A round stopped or failed before the reply leaves two user messages in a row. A round stopped mid-call keeps a call with an empty name, partial arguments or an empty id. Strict servers and role-alternating templates (Mistral, Gemma) can then reject every later request until `/clear`. The fix belongs in `ClientToolLoop` or `Conversation` and affects every provider. (owner decision)
- An assistant message stopped during reasoning is sent with empty content. Some strict APIs reject it.
- A base URL with a query string (e.g. `?api-version=`) gets the path appended after the query.
- The provider is not disposed when the Editor exits. This is harmless.
