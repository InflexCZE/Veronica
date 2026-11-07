# Veronica: Spec

Veronica is a modding assistant integrated into the VRage3 / Space Engineers 2 Editor.

## Components

- **Editor plugin** (`Veronica.dll`): runs inside the Editor process (.NET, Avalonia), provides the UI and exposes MCP tools.
- **Providers**: the model that drives the MCP tools. The plugin works with any of them through the same tools.
  - `WebQwen.exe`: local model runtime. The plugin starts it and interoperates with it.
  - `claude -p`: Claude Code CLI.
  - Any OpenAI-compatible server, local or remote.

- [Context.md](Context.md): rules for what the model is told.

## Distribution

- The modder downloads `Veronica.exe` and puts it in a folder of their choice. Veronica installs into that folder.
- On first start `Veronica.exe` welcomes the modder and installs in the background while they pick a provider (local model, Claude, OpenAI-compatible server), set it up and test it.
- For the local model it measures the GPU, shows every model with whether it fits, recommends one and downloads the chosen ones. A model that does not fit can still be installed.
- Afterwards `Veronica.exe` manages the provider, the mods and the versions. It lists the modder's local mods; switching one on adds Veronica to it, switching it off removes it.
- The panel tells the modder when a newer Veronica exists; its button runs `Veronica.exe`, which updates once the Editor is closed.
- Skill and wiki files the modder edited survive an update: `Veronica.exe` lists them next to the new versions, to keep either.

| artifact | source |
|---|---|
| `Veronica.exe`; `Veronica.zip` (plugin, `WebQwen.exe`, skills, wiki) | GitHub releases of this repo |
| model weights, model runtime library | Hugging Face, at the revisions WebQwen pins |

## Scenarios
