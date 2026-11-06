# Provider selection

**Exists:** the OpenAI-compatible and Claude providers, chosen by environment variables read once when the panel is created. WebQwen is used otherwise. `Docs/Tech/Distribution.md` decides that `Veronica.exe` selects and sets up the provider and writes the settings the plugin reads at the next Editor start.

**Missing:**
- `Veronica.exe`'s provider selection and setup pages, and each provider's check (WebQwen: model files verified; Claude: `claude` found and signed in; OpenAI-compatible: the server lists the model).
- A full chat in the Editor against a live OpenAI-compatible server is untested. Only the unit tests and one live round outside the Editor have run.

**Start at:** `Source/Veronica/ModelProvider.cs`.
