# Veronica: Tech

| doc | scope |
|---|---|
| [WebQwen.md](WebQwen.md) | `WebQwen.exe`, the local model runtime provider |
| [VramSharing.md](VramSharing.md) | Editor and WebQwen taking turns on one GPU's VRAM |
| [Distribution.md](Distribution.md) | Release, setup tool, install folder, hooking into a mod, model download, updates |
| [EditorPlugin.md](EditorPlugin.md) | `Veronica.dll` as an Editor plugin: loading, build, dev mod |
| [EditorUi.md](EditorUi.md) | Veronica's panel, the Board, menu, fonts and theme inside the Editor |
| [Agent.md](Agent.md) | `Veronica.Agent.dll`: providers, tool loop, conversation |
| [EditorTools.md](EditorTools.md) | Agent tools for content authoring: find, read, write, create prefab, partial and definition, prefab components, show, present_graph |
| [ClaudeProvider.md](ClaudeProvider.md) | The `claude -p` provider and how it routes Veronica's tools into its turns |
| [McpEndpoint.md](McpEndpoint.md) | Veronica's tools over MCP for any local client, with any provider |
| [Skills.md](Skills.md) | Skills and wiki: how the model learns to reach modding goals |
| [Wiki.md](Wiki.md) | The wiki's pages, the researcher that reads them, and subagents |
| [Compaction.md](Compaction.md) | The chat going on past a full context window: the wall, the summarizer, what the turn goes on from |
| [WebQwenProtocol.md](WebQwenProtocol.md) | How `Veronica.dll` runs `WebQwen.exe` and talks to it |
| [Eval.md](Eval.md) | Eval runs in the Editor: control channel, transcripts, run lifecycle, tasks |
