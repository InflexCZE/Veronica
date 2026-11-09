# Context

Rules for everything the model reads: the system prompt, skills (`Skills.md`), wiki answers (`Wiki.md`), tool descriptions and tool errors, and the summary after a compaction (`Compaction.md`).

## General rules

- Add only what a modder doing a variation of the task would also need. Facts about the game qualify (where a value lives, which side of a link is authored, how entries sort). Steering towards one task's answer does not: no task values, no "look it up in vanilla" where real mods bring their own assets.
- A fact is checked against the game source before it is added. A wrong fact misleads more than a missing one.
- A convention is stated with its reason, not as a bare order.
- Behaviour every task needs lives in the system prompt, not repeated in each skill.
- Where the right action depends on the situation, the context names what to observe and both branches.
- Strong imperatives suppress other behaviour, such as asking the user or doing a step together with them. The context describes the work and keeps hard rules for what must never happen.
- The user's choices are asked for: text players see, folders, values the user did not give, steps done in the Editor. Anything readable in the project is looked up. An example value in a skill is a suggestion to offer.
- Game knowledge comes from reads and the wiki, and the context never forces a wiki call over a read the model already knows how to make: one read is cheaper than a research run.
- An ambiguous wish is acted on when the model picks a reading and confirms it with the user, who can then steer.
- When the model fights a tool, the tool changes, not the wording: one form per value, errors that do not lead into a wrong field, results that survive a cut-off reply. A hint in an error must be unambiguous.
- Skipped work is countered with required proof, not with emphasis.
- When wording stops moving a failure across eval batches, the levers left are mechanics and knowledge delivery (the wiki).
- Tool descriptions say only what the model would not guess; the system prompt carries no tool guidance.
- Messages the Editor injects into the conversation (reminders, compaction summaries) are marked as coming from the Editor, and the system prompt says the user has not seen them. Unmarked, a model takes such a message for the user's reply.

## Progressive disclosure

- The system prompt carries one index line per skill; bodies are read on demand (`Skills.md`, Delivery). An index that grows past about 4K tokens is a sign the skills need better progressive disclosure, not a longer index.
- Knowledge a task needs only sometimes stays out of the chat's context: a researcher reads it and only its answer enters the transcript (`Wiki.md`).

## System prompt

Who Veronica is, the marker rule above, then "How we work" and the skill index. Without any skills, only the rule to ask the user for what the project lacks is left, since there is no plan to work.

"How we work" is one procedure in four steps, because small models skipped activation or worked from memory when the same guidance was spread over separate hints:

1. Understand the task and ask the user for what the project lacks. A question about how something works, what it takes or what went wrong is answered, not built: told to activate before anything else, the model turned questions into plans.
2. Read the skills that may cover it.
3. Once the user wants the work done, activate the fitting skill before anything else, asking the user included: a small model that asked first often never activated.
4. Work the current plan item, asking the user for what the item needs (choices with `ask_user`, Editor steps with `wait_for_user`), then mark it with proof or a reason. A new thing gets its own name and text from the user even when a similar one exists, a skill's example folder is a suggestion to offer, and when unsure whether a choice is the user's the model asks. Told to work the item "only, yourself", models invented the user's choices; left to judge, they argued themselves out of asking.

After the procedure:

- Changing the project: read-only projects are never written; a change to their definitions goes on a partial override or copy in the mod. Values from a template, copy or comparable block are a starting point to check against what the user wants, not the user's values.
- Knowledge: game knowledge, a rule or a value, unit or size, rests on what was read in this session: the model reads it itself when it knows where to look and asks the wiki otherwise. What neither gives is reported as unknown. Without this, models quoted values from other games and answered rules from a skill's wording. When to ask the wiki is listed in `Wiki.md`.
- An answer about how content items connect is also shown as a graph on the Board, beside the text (`Board.md`).
