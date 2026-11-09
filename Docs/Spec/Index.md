# Spec

What the user gets from each feature.

## Belongs here

- The objective of each feature: what the user gets out of it and why it exists.
- Behaviour the user sees and relies on.
- Scope: what the feature covers and what it deliberately leaves out.
- One file per feature, or per group of features that refer to each other too tightly to read apart.

## Does not belong here

- Components, processes, protocols, classes, file names, binary names: `Docs/Tech` and the code.
- How a behaviour is achieved: `Docs/Tech` when it is a decision, the code otherwise.
- UI labels as feature names. Features are named by what they technically are.
- Literals that go out of sync: exact messages, defaults, paths. A number stays when the feature depends on it.
- History of how a behaviour came about.
- Filler. A feature with little to say has a short file.

## Files

| file | feature |
|---|---|
| [Chat.md](Chat.md) | Conversation with the assistant in the Editor |
| [Board.md](Board.md) | Content items and their relations shown to the user |
| [ContentEditing.md](ContentEditing.md) | What the assistant can find, read, change and create in a mod |
| [Skills.md](Skills.md) | Guided modding tasks |
| [Wiki.md](Wiki.md) | Answers about the game from shipped knowledge |
| [AiProviders.md](AiProviders.md) | The models that can drive the assistant |
| [McpEndpoint.md](McpEndpoint.md) | The assistant's tools for external MCP clients |
| [Launcher.md](Launcher.md) | Install, provider setup, enabling Veronica per mod, updates |
