# Providers assume a 45K context window

**Exists:** every provider reports `ContextWindow` as 45K tokens. The wiki researcher stops reading 10K below it (`Docs/Tech/Wiki.md`). Compaction does not use it: it acts on the wall the server reports (`Docs/Tech/Compaction.md`).

**Missing (needs design with the owner):** the real size per provider. WebQwen knows its own window (`WebQwenLaunch.ContextWindowSize`, the engine's default otherwise); llama.cpp reports it in `GET /props`; other OpenAI-compatible servers do not report it, so they need a setting or the assumed value. Against a server with a smaller window the researcher's reading bound lies past the real wall (`ResearcherContext.md`).
