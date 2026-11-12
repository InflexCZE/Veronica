# Eval

The target models are small local ones. They reach modding goals only with help from their context: system prompt, skills, tool descriptions, wiki. That context is tuned from evidence: real runs in the Editor, read for failure patterns.

The aim is collecting failures to spot patterns, not deterministic scoring. Runs are not expected to repeat, and there is no machinery for comparing variants.

## Roles

- **Master:** Claude Code with the `veronica-eval` skill. Picks tasks, starts one fork per batch, keeps the findings log, tests context hypotheses with further batches, and proposes context changes to the owner. It does not grade.
- **Fork:** runs one batch, one run after another, each end to end, and returns once with the take-aways. A fork per batch keeps the per-run detail out of the master's context.
- **Haiku** (optional): extracts what the caller asks from a long transcript, so the caller does not read all of it.
- One run at a time: the Editor and the model share one machine.
- The fork plays the user: fairly, as a modder who knows what they want, without handing out what the model should find itself. Answers vary between runs on purpose, since varied users reveal more failures.
- The fork decides when a run ends: done, hopeless, or an error such as a context wall compaction cannot get past. Stopping a hopeless run early saves time.
- When the round budget runs out, the fork continues a productive run and ends a stuck one.

## Control channel

The plugin serves a small HTTP JSON endpoint on loopback when its port setting is set; without it nothing listens. It sends messages, answers question cards, stops turns, reports status, and reports the Editor's validation issues.

- A raw loopback socket serves it rather than `HttpListener`, which needs a URL reservation for a non-admin user.
- It drives the same conversation the panel shows, so a run is visible in the Editor, and answering a card runs the card's own code, so a run exercises the user's path.
- A turn counts as running from the moment a message is accepted, so status never reports idle for a message just sent. Every turn ends, failed if need be, so a run never waits on a turn that cannot finish. The panel's Compact choice is not on the channel, so a turn failed on a wall compaction cannot get past stays failed.
- A status wait also returns when a question card opens. A Claude turn stays running while its card is open, so this gives the fork one rule for every provider.
- Progress during a turn is read from the transcript, not the channel.
- Context used is the whole prompt of the last round plus its reply: llama.cpp reports cached and newly processed prompt tokens apart.
- Validation is for grading only, never a tool for the model: valid mods carry many benign warnings that would mislead it. An error the run added fails it; warnings pass; errors a fixture has at load are the fixture's. An error outside the mod means the Editor loaded corrupted and the run is unreliable.

## Transcript

- When the run folder setting is set, the plugin records every provider session to a file of its own as it happens, so a hung, crashed or killed Editor loses nothing.
- The recording wraps the provider, so the chat, subagents and the summarizer are recorded the same way. The chat's file stays exactly what the chat model saw; a session opened during a tool call is linked both ways with that call.
- One JSON object per line, written whole when its message completes, not per token. A failed round appends nothing to the conversation, so only the turn end carries its error.
- A compaction shows as a turn end with the wall's error, the summarizer's own file, and a line in the chat's file with the message the turn goes on from (`Compaction.md`).

## Run lifecycle

- Every run starts a fresh Editor on the dev mod reset with git, with the task's fixture copied in. The Editor's load time is accepted as the price of independent runs.
- Each run builds the plugin from the working tree, so uncommitted context edits are in the run, and saves the diff against `HEAD` into the run folder, so each run records what differed.
- The Editor runs the OpenAI-compatible provider against a llama.cpp server the owner keeps running; runs never start or stop it.
- The MCP endpoint (`McpEndpoint.md`) lets the fork play the user's steps in the Editor, such as positioning a dummy the model asked for, by calling the tools as a user would edit.
- Waits are bounded so a fork waits with blocking calls: a fork that leaves work in the background notifies the master each time it pauses, flooding the master's context.
- The scripts are in `Eval/`.

## Tasks

- `Eval/Tasks/<name>.md`, committed: the first message as a user would type it, what the user wants and knows for the role-play, and the pass criteria. An optional fixture folder is copied over the dev mod.
- Small generated fixture files, such as an icon, go to git as they are; the script that made them does not. Large generated files stay out of git so they do not bloat it: the fixture holds a script that writes the same bytes every run. Either way the fixture keeps the files' metas captured from the Editor's own import; without them the Editor stops at load to ask about them.
- Fixtures reuse the MyThruster models, whatever the shape: no new model files go into git.

## Method

| batch | context | runs |
|---|---|---|
| baseline | unchanged, for a new task or model | 3 per task |
| hypothesis | one change in the working tree | 3 per task it targets, plus a regression task the change can touch |
| confirmation | the committed context | the tasks the hypothesis batch did not run |
| model comparison | the same commit for every model | the same tasks in the same order |

- Nothing under `Source` or `Eval/Tasks` changes while a batch runs: every run builds the working tree and reads its task file.
- A new model's first run checks that its tool calls work before the rest of the batch.
- A criterion that disagrees with the game is checked in the game source; a wrong task is fixed.
- A run counts beyond pass or fail: user turns, context used, whether what it asked was the user's to answer, proof accuracy, invented claims. A wish the user restated unasked is a nudge and noted.
- A change is kept when it moves its failure without a regression in the other tasks; otherwise it is reverted. Context changes follow `Context.md`.
- Facts about the game come from the SE2 source, read by a subagent that does not touch the Editor while a batch runs.
- Run folders and the findings log stay out of the repo, under `.tmp/eval/`. Context changes reach the repo only after the owner approves them.
