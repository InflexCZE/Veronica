# Provider selection (owner decision)

**Exists:** the OpenAI-compatible provider, chosen by environment variables read once when the panel is created (`Docs/Tech/Agent.md`). WebQwen is used otherwise.

**Missing:**
- A way for the user to pick the provider and enter the server URL, model and key in the Editor, and to keep those settings.
- Switching provider without restarting the Editor.
- A full chat in the Editor against a live server is untested. Only the unit tests and one live round outside the Editor have run.

**Owner decision:** where the settings live (the panel, Editor preferences, a file), and how the API key is stored.

**Start at:** `Source/Veronica/ModelProvider.cs`.
