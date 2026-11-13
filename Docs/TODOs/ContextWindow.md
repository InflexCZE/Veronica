# Server providers assume a 45K context window

**Exists:** the local model reports the window it is started with (`Docs/Tech/WebQwen.md`). The OpenAI-compatible and Claude providers report an assumed 45K tokens. The wiki researcher stops reading 10K below the reported window (`Docs/Tech/Wiki.md`). Compaction does not use it: it acts on the wall the server reports (`Docs/Tech/Compaction.md`).

**Missing (needs design with the owner):** the real size for the server providers. llama.cpp reports it in `GET /props`; other OpenAI-compatible servers do not report it, so they need a setting or the assumed value. Against a server with a smaller window the researcher's reading bound lies past the real wall (`ResearcherContext.md`).
