# Skills and wiki: what the model knows about modding

The model has tools that act on the project (`EditorTools.md`). How to reach a goal with them, and what the Editor's concepts mean, comes from two kinds of data.

## The split

| | skill | wiki |
|---|---|---|
| holds | how: which tool calls, in which order, plus only the facts a step needs to be done right (which definition holds a field, a value to copy from vanilla) | what and why: concepts (prefab, definition, partial copy and override, template), the G-Screen hierarchy, art rules (LODs, mount points, texture naming), the vanilla folder layout |
| reaches the model | the model loads it by name before working on a task | a researcher subagent reads it and answers the model's question (`Docs/TODOs/Wiki.md`) |
| refers to the other | a step may say "ask the wiki about X" where X matters | never |

## Composition

- Skills are cut so the model combines them, not one per finished thing.
  - Procedures: a task's steps, e.g. `create-block`, `override-definition`, `add-to-gscreen`.
  - Appendices: what one function adds to a procedure, e.g. `block-thruster` for a block that thrusts. A procedure names, at the step where they apply, the appendices to load.
  - A function without an appendix is built by reading a comparable vanilla block's definitions and mirroring them. This step is in `create-block`.
- Each skill is its own file and its own index line. The model loads only what the task needs.

## Files

- One Markdown file per skill, with `name` and `description` frontmatter. The description is the trigger only: when to load the skill, one sentence. It carries no instructions.
- Body sections, in order:
  - **Needs**: the inputs. Each is a file or value the user provides. The system prompt already says to ask for what cannot be found.
  - **Steps**: numbered, written as the real tool calls. A step that only some tasks need says when.
  - **Show**: the last step shows the result in the Editor (`show`), with one line on what the user should look at. Testing in game is the user's call.
- No read-back check of the edits: the model sees each change's reply, and the user works alongside it.
- ASCII only.
- Size is a guideline: a skill should stay well under the 20K context it shares with the tool schemas and the transcript.

## Writing a skill

- Steps say what to do. Facts belong in a step only when the step goes wrong without them.
- Name definitions by their type (`ThrusterDefinition`), never by a file name a template generates: names vary per template.
- No lists of what is supported, and no general gotchas: they go stale, and the tools' own errors already name the valid choices.
- Importing files is the user's job, done in the Editor. A skill lists the imported items it needs; it does not describe the import.
- Source material: the official SE2 modding guides on Steam, rewritten from clicks into tool calls. Art, FMOD and other work outside the Editor is left to the user.

## Delivery

- The system prompt ends with the skill index, one line per skill, so the model knows which skills exist without paying for their bodies.
- `skill(name)` returns a skill's steps. Rounds that call only `skill` are free: loading guidance must not cost the task its budget.
- A broken skill file is skipped and logged, so one bad edit does not take the other skills down.
- Skills are read again on `/clear`, so an edited skill takes effect without restarting the Editor. During development they are read straight from `Source/Veronica/Skills`; where an installed Veronica keeps them is open (`Docs/TODOs/SkillSources.md`).

## Testing

- `Veronica.Tests` covers the skill files and the `skill` tool, and checks that every shipped skill loads.
- A skill itself is tested by hand in the Editor: the model runs it on the dev mod against a user prompt, reverted with git.
