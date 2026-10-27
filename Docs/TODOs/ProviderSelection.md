# Provider selection (owner decision)

**Exists:** the OpenAI-compatible provider and the Claude provider, chosen by environment variables read once when the panel is created (`Docs/Tech/Agent.md`, `Docs/Tech/ClaudeProvider.md`). WebQwen is used otherwise.

**Missing:**
- A way for the user to pick the provider and enter its settings (server URL, model, key; Claude model, effort, port) in the Editor, and to keep those settings.
- Switching provider without restarting the Editor.
- A full chat in the Editor against a live OpenAI-compatible server is untested. Only the unit tests and one live round outside the Editor have run.

**Owner decision:** where the settings live (the panel, Editor preferences, a file), and how the API key is stored.

**Start at:** `Source/Veronica/ModelProvider.cs`.
