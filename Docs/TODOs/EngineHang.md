# A hung engine blocks the chat

**Exists:** Stop sends `interrupt` and waits for the chat's `done` with no time limit (`Source/Veronica.Agent/WebQwen/WebQwenConnection.cs`). If the engine never answers (a GPU hang, a lost interrupt), the panel stays on Stop until the Editor restarts. The Editor also stays frozen with its VRAM evicted, because the turn's hold (`Docs/Tech/VramSharing.md`) never ends. A model load that never reports ready or exits holds the freeze the same way.

**Missing (needs design with the owner):** a way out, for example a second Stop, or a timeout after the interrupt, that kills WebQwen.exe. The round then fails as "WebQwen exited" and the next message starts a new process.
