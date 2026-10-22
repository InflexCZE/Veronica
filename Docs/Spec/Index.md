# Veronica: Spec

Veronica is a modding assistant integrated into the VRage3 / Space Engineers 2 Editor.

## Components

- **Editor plugin** (`Veronica.dll`): runs inside the Editor process (.NET, Avalonia), provides the UI and exposes MCP tools.
- **Providers**: the model that drives the MCP tools. The plugin works with any of them through the same tools.
  - `WebQwen.exe`: local model runtime. The plugin downloads it to the user's machine, starts it and interoperates with it.
  - `claude -p`: Claude Code CLI.

## Distribution

| artifact | source |
|---|---|
| `Veronica.dll`, `WebQwen.exe` | GitHub releases of this repo |
| model weights, model runtime library | Hugging Face repo |

## Scenarios
