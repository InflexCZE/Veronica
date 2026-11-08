# Eval: runs, transcripts and failure modes

The target model is a small local one (9B to 27B, about 50K context). It reaches modding goals only with help from its context: system prompt, skills, tool descriptions. That context is tuned from evidence: a corpus of real runs in the Editor, read for failure patterns.

The aim is collecting failures to spot patterns, not deterministic scoring. Runs are not expected to repeat, and there is no machinery for comparing variants.

## Roles

| role | does |
|---|---|
| master agent | Claude Code with the `veronica-eval` skill loaded. Picks tasks, starts one fork per batch (the runs of one context variant), keeps the findings log, tests context hypotheses with further batches, and proposes context changes to the owner. It does not grade. |
| fork | a fork of the master, so it inherits the skill and the master's current investigation. Runs a batch one run after another, each end to end: starts the Editor, sends the task, answers the model's questions, watches progress, ends the run, writes the run's `results.md`. Returns once, with the take-aways across the batch. One fork per batch keeps the per-run detail out of the master's context. |
| Haiku | optional, at the fork's or master's choice: reads a transcript and extracts what is asked of it (notable sections, what the model is asking about now), so the caller does not read the whole transcript itself. |

- One run at a time: the Editor and the model share one machine.
- The fork plays the user. It answers fairly, as a modder who knows what they want, but does not hand out answers the model should find itself. Answers vary between runs on purpose: varied users reveal more failures.
- The fork decides when a run ends: the task is done, the run is hopeless, or the model hit the end of its context or another error. Stopping a hopeless run early saves time. A full context is compacted and the turn goes on (`Compaction.md`); a wall compaction cannot get past is an accepted end.
- When the round budget runs out ("send continue"), the fork continues a productive run and ends a stuck one.
- The fork's report is free-form: whatever fits the task or the failure mode under investigation.

## Control channel

`Veronica.dll` serves a small HTTP JSON endpoint when `VERONICA_CONTROL_PORT` is set. It binds 127.0.0.1 only. Without the variable nothing listens.

| request | does |
|---|---|
| `GET /status` | whether a turn is running, how many turns ended, how the last one ended (answered, stopped, error with its text), the context used so far, and the open question card's questions with their options (`questions`, empty when none is open) |
| `GET /status?wait=<s>` | as above, but returns as soon as no turn is running or a question card is open, or after `<s>` seconds (at most 10 minutes) |
| `POST /send` | the body, as plain text, is sent as a user message, as if typed in the panel; refused while a turn runs |
| `POST /answer` | the body, a JSON array with one answer per open question (`null` skips it), answers the card as the panel's Answer does; refused when no card is open |
| `POST /stop` | stops the running turn, as the panel's Stop does |
| `GET /validation[?wait=<s>]` | the Editor's validation issues (severity, mod or not, project, item, field, message; infos left out) with error and warning counts, read once asset processing and content pushes have been idle a moment, or after `<s>` seconds (default 300) with `idle` false and `busy` naming, per project, the processings in progress and the active and pending content pushes |

- The channel starts listening when the Veronica panel is created, after the project has loaded. A refused connection means the Editor is not ready yet.
- A raw loopback socket serves it rather than `HttpListener`, which needs a URL reservation for a non-admin user.
- The channel drives the same `Conversation` the panel shows, so the run is visible in the Editor.
- A turn counts as running from the moment `/send` accepts it, so `/status` never reports idle for a message just sent.
- Every turn ends: a context overflow compaction cannot get past or a server error fails the turn, and `/status` reports it. The Compact choice of the panel's card is not on the channel, so the turn stays failed. A run never waits on a turn that cannot finish.
- Progress during a turn is read from the transcript file, not from the channel.
- `/answer` runs the card's own code, so a run exercises the answer path a user takes. A Claude turn stays running while its card is open (`ClaudeProvider.md`); the wait returning on an open card gives the fork one rule for every provider.
- Context used is the whole prompt of the last round plus its reply: llama.cpp reports cached and newly processed prompt tokens apart.
- Validation is for grading only, never a tool for the model: valid mods carry many benign warnings that would mislead it. An error the run added fails it; warnings pass. Errors a fixture already has at load are the fixture's, recorded in a TODO, and do not count. An error outside the mod, such as in vanilla, means the Editor loaded corrupted and the run is unreliable.

## Transcript

When `VERONICA_RUN_DIR` is set, the plugin records every provider session to a file of its own in that folder as it happens, so a hung, crashed or killed Editor loses nothing. The recording wraps the provider, so whatever opens a session is recorded the same way.

- The chat's session goes to `transcript.jsonl`, a subagent's to `<name>-<n>.jsonl` (`wiki-1.jsonl`, `compaction-1.jsonl`, ...). The chat's file stays exactly what the chat model saw.
- A session opened during a tool call is linked both ways: its `setup` line names the parent file and call id (`parent`), and the call's `tool` line names the files it opened (`sessions`).
- One JSON object per line, strings with escaped line breaks.
- Every line carries its index `i`, seconds since the session's file was opened `t`, and its `type`.
- A `setup` line holds the provider, the system prompt and the tool definitions. It comes first, and again when a turn's system prompt or tools differ from the last. `/clear` writes a `clear` line, and the next turn writes its setup again.
- Then one line per user message (`user`: the turn's new message, or a subagent's brief), assistant message (`assistant`: round, text, reasoning, tool calls with parsed arguments, round stats including context used), tool result (`tool`, with its duration), automatic user message from the tool loop (`nudge`), notice such as the round budget (`notice`) and turn end (`turn_end`: answered, stopped or error, the error text, context used).
- A failed round appends nothing to the conversation, so its partial reply is not recorded; the turn end carries the error.
- Lines are written whole when their message completes, not per streamed token.

Reading a run across a compaction (`Compaction.md`):
- The session's turn that hit the wall ends with a `turn_end` line, outcome `error`, whose error is the server's refusal or that the reply ran into the end of the window.
- The summarizer's session is `compaction-<n>.jsonl`: its brief is the transcript as it was rendered, its assistant line the summary.
- When the summary worked, a `compacted` line follows in `transcript.jsonl`, in place of a `user` line: `text` is the message the turn goes on from, which is now the whole conversation the chat model sees, and `session` names the summarizer's file. The turn's rounds follow it, up to the turn's real end.
- No `compacted` line after the `turn_end` means the turn ended there: the allowance was spent or the summary failed (its file says which).

## Run lifecycle

Every run starts a fresh Editor on a clean dev mod. The Editor's load time is accepted.

`Eval/Start-Run.ps1 -Task <name>`:
1. Kills a running Editor.
2. Resets `Assets/VeronicaDevMod` with git (restore and clean, including the Editor's caches).
3. Copies the task's fixture folder into the dev mod, when the task has one, and runs its `make_fixture.py` on the dev mod, when it has one.
4. Builds `Veronica.dll`, so uncommitted context edits are in the run.
5. Creates the run folder and saves the changes under `Source` against `HEAD`, new files included, as `context.patch`, so each run records what differed.
6. Writes `run.json`: task, commit, dirty flag, model (the server's first listed unless `-Model`), reasoning effort (`-Effort`, empty for the template's default), start time, Editor process id, port.
7. Starts the Editor with the OpenAI-compatible provider pointed at the llama.cpp server (`-LlamaUrl`, default `http://localhost:8080/v1`), the effort, the control port (`-Port`, default 47811), the run folder and the MCP endpoint (`McpEndpoint.md`, `-McpPort`, default 47812). The endpoint lets the fork play the user's Editor steps, such as moving a dummy the model asked the user to position, by calling the tools as the user would edit in the Editor (`Eval/Mcp-Call.ps1`); the task's User section says what to do.
8. Prints the run folder, the port, the Editor's process id and the MCP port.

`Eval/Wait-Ready.ps1 -RunDir <dir>` waits for the control channel to answer, at most `-Seconds` (default 540), and prints `Ready`, `NotReady` or `Exited`. The wait is bounded so a fork waits with blocking calls: a fork that leaves work running in the background notifies the master each time it pauses, flooding the master's context.

`Eval/Get-Validation.ps1 -RunDir <dir> [-Label <name>]` makes one `/validation` request, which waits for processing itself, saves it as `validation-<name>.json` in the run folder, prints a summary line and every error, and exits 0 (no errors), 1 (errors on the mod), 2 (errors outside the mod), 3 (no answer) or 4 (processing never settled within the wait; the errors are stale and what was busy is printed).

`Eval/Stop-Run.ps1 -RunDir <dir>` kills the Editor and copies its logs into the run folder.

The owner keeps the llama.cpp server running; runs do not start or stop it.

## Tasks

`Eval/Tasks/<name>.md`, committed. Each holds:
- **Prompt**: the first message, as a user would type it.
- **User**: what the user wants and knows, for the fork's role-play.
- **Pass**: the criteria the run is judged against.
- **Fixture** (optional): `Eval/Tasks/<name>/`, copied over the dev mod before the run. Generated files (images a script can draw) stay out of git: the fixture holds `make_fixture.py`, which writes the same bytes every run, and the files' metas captured from the Editor's own import. Without their metas the Editor would stop at load on its prompt for files without one.
- Fixtures reuse the MyThruster models, even for rings or door leaves: no new model files go into git.

## Method

| batch | context | runs |
|---|---|---|
| baseline | unchanged, for a new task or model | 3 per task |
| hypothesis | one change in the working tree | 3 per task it targets, plus a regression task the change can touch |
| confirmation | the committed context | the tasks the hypothesis batch did not run |
| model comparison | the same commit for every model | the same tasks in the same order |

- Nothing under `Source` or `Eval/Tasks` changes while a batch runs: every run builds the working tree, and the fork reads the task file for every run.
- A new model's first run checks that its tool calls work before the rest of the batch.
- A criterion that disagrees with the game is checked in the game source; when the task is wrong, the task is fixed.
- A run counts besides pass or fail: user turns, context used, what was asked and whether it was the user's to answer, proof accuracy, invented claims. A restated wish from the user is a nudge and is noted in the verdict.
- A change is kept when it moves its failure without a regression in the other tasks; otherwise it is reverted. Context changes follow the rules in `Skills.md` (Context engineering).
- Facts about the game come from the SE2 source, read by a subagent that does not touch the Editor while a batch runs.

## Outputs

Not committed, under `.tmp/eval/`:
- `runs/<yyyyMMdd-HHmmss>-<task>/`: `run.json`, `transcript.jsonl`, a file per subagent run (`wiki-<n>.jsonl`, `compaction-<n>.jsonl`), `context.patch`, `validation-start.json` and `validation-end.json`, `logs/` (the Editor's), and the fork's `results.md`.
- `findings.md`: the master's running log of failure patterns with run references, hypotheses tested and their outcome.

Context changes reach the repo only after the owner approves them.
