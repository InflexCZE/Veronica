# Providers assume a 45K context window

**Exists:** every provider reports `ContextWindow` as 45K tokens. The wiki researcher stops reading 10K below it (`Docs/Tech/Wiki.md`).

**Missing:** the real size per provider. WebQwen knows its own window (`WebQwenLaunch.ContextWindowSize`, the engine's default otherwise); llama.cpp reports it in `GET /props`; other OpenAI-compatible servers do not report it, so they need a setting or the assumed value.
