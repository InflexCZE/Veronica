# Wiki

The assistant answers how the game and the Editor work from knowledge shipped with Veronica, not from the model's memory.

- Questions such as "why does X behave so" or "what went wrong" get an answer, not a build.
- A lookup runs as a separate researcher that reads the wiki and the project read-only; the chat shows its steps and its answer.
- When the sources do not say, the answer says it is unknown instead of guessing.
- Wiki pages ship with Veronica. The modder may edit them and add their own in the install folder; edited pages survive updates (`Launcher.md`).
- With the Claude Code provider and other MCP clients there is no researcher: the model lists and reads the pages itself (`McpEndpoint.md`).
