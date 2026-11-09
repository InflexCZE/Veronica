# Skills and plans

The tools act on the project (`Tools_Overview.md`). How to reach a goal with them comes from skills; what the game's concepts mean comes from the wiki (`Wiki.md`). The rules for what either may say are in `Context.md`.

## Skills and the wiki

| | skill | wiki |
|---|---|---|
| holds | how: which tool calls, in which order, plus the facts every run needs | what and why: concepts, hierarchies, art rules, layouts; knowledge a task needs only sometimes |
| reaches the model | the model reads it by name and activates it | a researcher reads it and answers the model's question |
| refers to the other | a step may say "ask the wiki about X" where X matters only sometimes | never |

## Composition

Skills are cut so the model combines them, not one per finished thing.

- Procedures: a task's steps (`create-block`, `override-definition`, `add-to-gscreen`).
- Appendices: what one function adds to a procedure (`block-thruster`). A procedure names, at the step where they apply, the appendices to activate.
- A function without an appendix is built by mirroring a comparable vanilla block's definitions; that step lives in `create-block`.
- A skill guides the model to read a value and write what does not fit the user's wish; it does not name the values a task checks.
- Each skill is its own file and index line, so the model loads only what the task needs.

## Skill files

- One Markdown file per skill, with `name` and `description` frontmatter. The description is the trigger only: when to load the skill, one sentence, no instructions.
- Body sections, in order: Needs (inputs the user provides), Plan (one-line items, see Plan), Steps (numbered, written as real tool calls, a conditional step says when), Show (the last step shows the result in the Editor with what to look at; testing in game is the user's call).
- No read-back check of edits: the model sees each change's reply, and the user works alongside it.
- Lean, not bare: a skill holds what every run needs and leaves the rest to the wiki, because it shares the context with the tool schemas and the transcript.
- Steps say what to do. A fact belongs in a step only when the step goes wrong without it.
- Definitions are named by type, never by a file name a template generates, since those vary per template.
- No lists of what is supported and no general gotchas: they go stale, and the tools' errors already name the valid choices.
- Importing files is the user's job in the Editor. A skill lists the imported items it needs, not how to import them.
- Source material: the official SE2 modding guides, rewritten from clicks into tool calls. Art, audio and other work outside the Editor is left to the user.
- ASCII only.

## Delivery

- The system prompt ends with the skill index (`Context.md`).
- `skill(name)` returns the overview: the skill without its Plan section.
- A broken file is skipped and logged, so one bad edit does not take the other skills down.
- Skills are read again on `/clear`, so an edited skill takes effect without restarting the Editor. They load from the source tree in a development build and from the install folder otherwise (`Distribution.md`), where the modder may edit and add them.

## Plan

Small models drop steps: after a failed write, or after handing off to an appendix skill, they forget the remaining steps and report the work done. A plan the harness tracks keeps the remaining work in front of the model.

- Plan items and Steps are separate sections, written to match, with no link between them. The model reads the Steps and gets the items from the Editor one at a time. The items are a tuning surface of their own and the place for deterministic checks later.
- The overview shows no plan, and activation names only the current item: seeing the whole list, small models mixed item numbers up with step numbers and worked ahead.
- Items lead the model without naming tool calls. An item for a conditional step says when; otherwise the model skips it.
- A skill without a Plan section cannot be activated.
- `activate_skill` on a skill not read yet returns its overview too, as a fallback: the model is meant to read first, since it cannot judge a skill it has not seen.

### Nesting

- One active skill at a time, on a stack of plans. Activating another skill pauses the open plan at its current item; when the new plan is done, the paused one resumes at that item.
- An item that names the next skill is done by activating it, and closed after the nested plan with proof of work that happened. Closing it before the activation made small models write proof of work not done yet, and replacing the plan on activation lost its remaining items.
- The resume result says to close the item if its work is done, not to redo it, since the item text still asks for that work. If it needs another skill, the model activates that one from the same item.
- A resumed skill whose overview is no longer in context comes with it, as on activation: after a compaction only the active skill's overview is carried over, and a parent resumed without its steps works from the item text alone.
- Activating a skill already paused lower on the stack is refused, naming the active item and how to close it: a second copy would leave two lists of the same items.
- The stack has no depth cap: chains are a few skills deep.

### Marking

- Strict order: marking takes no item number, so the model cannot skip ahead, close several items at once or name the wrong one. Skipping needs a reason, so strict order does not deadlock on an item that does not fit.
- Completing needs proof, one line on what was done. Ticking undone work or skipping applicable work is a form of hallucination; stating what was done makes it visible, and the tool says marking does none of the work.
- The tools are named as past-tense marks (`mark_completed`, `mark_skipped`): they are called after the work, not to do it.

### Reminders

- The next item is named in the plan tools' results, not on every tool result: a reminder everywhere is noise the model learns to ignore. The two places a model loses its step also name it: the end gate and the length nudge.
- Only the active plan's item is named anywhere (end gate, length nudge, research brief); paused plans are named when they pause and resume. The compaction summarizer is the exception and sees every open plan, since it must tell open work from finished (`Compaction.md`).
- End gate: ending a turn with an item open sends the model back once, through the tool loop's end nudge (`Agent.md`), naming the item and the choices. A deliberate stop for the user is `ask_user` or `wait_for_user`, which end the turn without it; the reminder names both and says skipping is never a way to wait, because small models act through tool calls and skipped item after item to get an answer. A second end is let through, so no model loops. It re-arms only after a tool call outside the skill and plan tools.
- `wait_for_user` is for steps the user does in the Editor, never questions (`Agent.md`, Questions to the user). It is a tool, not a reply marker, since beside the plan tools a marker read to small models as one more way to close an item.
- While no plan is open, editing tools' results carry a hint to activate a fitting skill: on the first edit, then every tenth. After a plan ends, the first hint waits for the tenth edit, because a follow-up to finished work is often a few edits and an early hint made the model restart a whole plan for a one-step change. Reads never carry it. Without the hint, small models read a skill and worked its steps without activating it.

### State

- The plan is harness state, not transcript. It lasts across turns until its last item is closed, so a question to the user mid-plan keeps it. `/clear` drops the plans and what was read.
- Rounds that call only the skill and plan tools (or `wiki`) are free, so bookkeeping does not cost the task its round budget.
- The end gate and free rounds belong to the client tool loop. The Claude provider runs its own loop and gets the plan tools without them (`ClaudeProvider.md`).
- Completion is the model's word and its proof; no item is checked yet.
