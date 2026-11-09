# Veronica agent rules

Veronica is an AI assistant for modding Space Engineers 2 inside the VRage3 Editor: it reads and edits a mod's content through tools, guided by skills and a wiki, driven by a local model or a hosted AI provider.

## Repository

| path | holds |
|---|---|
| `Docs/Spec`, `Docs/Tech` | feature spec and architecture, indexes below |
| `Docs/TODOs` | known missing or unfinished work |
| `Source` | the code: plugin, agent, editing layer, Launcher (`Veronica.Setup`), WebQwen runtime, tests |
| `Source/Veronica/Skills`, `Source/Veronica/Wiki` | shipped skills and wiki pages |
| `Eval` | eval run scripts; `Eval/Tasks` the eval tasks |
| `Assets/VeronicaDevMod` | the dev mod for live runs |
| `Build` | dependency fetch and release publish scripts |

## Rules

- `Docs/TODOs/Index.md` lists known missing or unfinished work, one file per item. When work leaves something undone (a deferred piece, a known gap, a review finding not fixed), add or update its file in the same commit, without asking; delete the file when the item is done.
- The docs hold only decisions the owner approved. A design fork found during implementation goes to the owner as options with a recommendation; it is not decided in code.

@../Docs/Spec/Index.md

@../Docs/Tech/Index.md

## Commit style

- Headline: what the change brings to the user or the project, not how it was done or the history behind it.
- Blank line, then `- ` bullets, as many as needed, for what a quick look at the diff does not tell: why a non-obvious choice was made, an owner decision, a TODO added or closed.
- Eval results stay: when a commit adds, fixes or improves an eval task, name the model, the task and the result (`27B MyGatling 0/3 -> 3/3`).
- No implementation details the diff shows, no test or line counts, no run IDs, transcript line numbers or commit hashes.
