# AI providers

The assistant runs on a frontier model or a local one, with the same tools either way. The modder picks one in the Launcher (`Launcher.md`).

| provider | for |
|---|---|
| built-in local model | the easiest and most compatible: no account, no server, works offline. Not the smartest or the fastest |
| Claude Code | a frontier model, on the modder's own Claude account |
| OpenAI-compatible server | a model the modder already hosts, local or remote |

## Built-in local model

- Targets 8 GB and 16 GB GPUs; the shipped models are chosen to fit them. Needs Windows with a D3D12 AMD or NVIDIA GPU.
- The Launcher shows the model's limits first and asks the modder to confirm before a model can be picked. It lists each model with its download size and VRAM need; a model that does not fit can still be installed. A measured fit check with a recommendation is planned ([RequirementsCheck](../TODOs/RequirementsCheck.md)).
- Several models can be installed; one is active. Without one, the chat points to the Launcher.
- The model starts with the first message and shows its load progress. Every start compiles the model's shaders, which is slow, until a shader cache exists ([ShaderCache](../TODOs/ShaderCache.md)).
- During a turn the Editor's viewports freeze on their last frame with a badge, so the model gets the GPU memory. The Editor's UI and the chat stay live, and edits show right after the turn. The Editor also gets its memory back while a question waits for the modder.
- Stop during a load ends the turn; the load goes on. A model process that dies fails the turn, and the next message starts a new one.

## Claude Code

- Needs Claude Code installed and signed in. A model and an effort can be chosen.
- Claude sees Veronica's tools only, not the modder's own Claude Code setup (MCP servers, hooks, memory). The account's identity still reaches Claude.
- No summarizing: Claude manages its own context. Claude reads wiki pages itself instead of asking a researcher (`Wiki.md`).
- Speeds include network overhead.

## OpenAI-compatible server

- Any server speaking OpenAI chat completions; the primary target is local servers such as llama.cpp.
- The address may be just a host name; the Launcher finds the server.
- Self-signed certificates are accepted. A reasoning effort can be set. Speeds show with llama.cpp only.
- The Editor keeps its GPU memory: the server may be remote or on another GPU.
