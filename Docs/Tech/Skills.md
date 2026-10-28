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
  - Appendices: what one function adds to a procedure, e.g. `block-thruster` for a block that thrusts. A procedure names, at the step where they apply, the appendices to activate.
  - A function without an appendix is built by reading a comparable vanilla block's definitions and mirroring them. This step is in `create-block`.
- Each skill is its own file and its own index line. The model loads only what the task needs.

## Files

- One Markdown file per skill, with `name` and `description` frontmatter. The description is the trigger only: when to load the skill, one sentence. It carries no instructions.
- Body sections, in order:
  - **Needs**: the inputs. Each is a file or value the user provides. The system prompt already says to ask for what cannot be found.
  - **Plan**: one-line items, what each part of the work is about ("Fill block name"), not the tool calls. Every skill has one, procedures and appendices alike. See Plan below.
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
- `skill(name)` returns a skill's overview: everything but its Plan section, so what it needs and its steps. Rounds that call only `skill` or the plan tools are free: loading guidance must not cost the task its budget.
- A broken skill file is skipped and logged, so one bad edit does not take the other skills down.
- Skills are read again on `/clear`, so an edited skill takes effect without restarting the Editor. During development they are read straight from `Source/Veronica/Skills`; where an installed Veronica keeps them is open (`Docs/TODOs/SkillSources.md`).

## Plan

Small models drop steps: after a failed write, or after handing off to an appendix skill, they forget the procedure's remaining steps and report the work as done. A plan the harness tracks keeps the remaining work in front of the model.

- Plan items and Steps are separate sections with no link between them. They are written to match. The model reads the Steps, and gets the items from the Editor one at a time. The items are a tuning surface of their own, and the place for deterministic checks later (`Docs/TODOs/PlanChecks.md`).
- A skill read shows no plan, and activating a skill names only its current item. Small models seeing the whole list mixed plan item numbers up with step numbers and worked ahead of the item they were on; each item is named when it becomes current, which is when it matters.
- Items are actionable and lead the model without naming tool calls. A step that only some tasks need has an item that says when ("..., only when the user asks"). The model skips it otherwise.
- Tools:

| tool | does |
|---|---|
| `skill(name)` | reads a skill's overview; the harness remembers it was read |
| `activate_skill(name)` | makes the skill the active one and starts its plan; the result names the first item. A skill not read yet comes with its overview, as a fallback: the model is meant to read first, since it cannot judge a skill it has not seen. |
| `mark_completed(proof)` | marks the current item completed, with one line on what was done; names the next |
| `mark_skipped(reason)` | marks the current item skipped, with the reason it does not apply; names the next |
| `wait_for_user()` | ends the turn to wait for the user's answer to a question in the reply |

- One active skill at a time. Chained skills hand off through their last item, which names the skill to activate next: activating another skill replaces a plan whose only open item is its last. Only the last skill of a chain shows the result.
- Activating another skill while an earlier item is still open is refused, naming the open items. A replace used to drop them without a word: in eval runs a failed write was followed by the next skill, and the plan's "Fill the models" item was lost. Each open item now has to be marked completed or skipped first, with its proof or reason.
- Strict order: marking takes no item number, so the model cannot skip ahead, close several items at once or name the wrong one. Skipping needs a reason, so the strict order does not deadlock on an item that does not fit.
- Marking an item completed needs proof: one line on what the model did. Ticking an item whose work never happened, or skipping one that applies, is a form of hallucination; small models did both, as if marking handed the work to the Editor. Stating what was done makes such a claim visible in the transcript, and the tool says that marking does none of the work.
- The tools are named as marks in the past tense: they are called after the work, not to do it.
- The next item is named only in the plan tools' results, not on every tool result: a reminder everywhere is noise the model learns to ignore. The two places a model loses its step also name it: the end gate below, and the length nudge, since a model cut off while thinking has lost track of where it was.
- The plan is harness state, not transcript. It lasts across turns until its last item is closed, so a question to the user mid-plan keeps it; `/clear` drops it with what was read.
- Rounds that call only the skill and plan tools are free, so bookkeeping does not eat the task's budget.
- End gate: ending a turn with an item open sends the model back once (the loop's end nudge), naming the open item and the choices. The message is marked as coming from the Editor, and the system prompt says so: unmarked, a model takes it for the user turning down its question. A deliberate stop to ask the user is a call to `wait_for_user`, which ends the turn without the reminder. Small models act through tool calls: offered skipping and a reply marker side by side, the 9B skipped item after item to wait for an answer. The reminder says skipping is only for items that do not apply, never for waiting. Ending again is let through, so no model loops. The reminder re-arms only after a tool call outside the skill and plan tools.
- The end gate and the free rounds belong to the client tool loop. The Claude provider runs its own loop, so it gets the plan tools without them (`ClaudeProvider.md`).
- Completion is the model's word and its proof; no item is checked yet.

## Testing

- `Veronica.Tests` covers the skill files and the `skill` tool, and checks that every shipped skill loads with a plan. The plan tools, the proof and reason, the refused and the allowed replace, the read tracking, the free rounds and the end gate are covered too.
- A skill itself is tested by hand in the Editor: the model runs it on the dev mod against a user prompt, reverted with git.
