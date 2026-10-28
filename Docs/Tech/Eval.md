# Eval: runs, transcripts and failure modes

The target model is a small local one (9B to 27B, about 50K context). It reaches modding goals only with help from its context: system prompt, skills, tool descriptions. That context is tuned from evidence: a corpus of real runs in the Editor, read for failure patterns.

The aim is collecting failures to spot patterns, not deterministic scoring. Runs are not expected to repeat, and there is no machinery for comparing variants.

## Roles

| role | does |
|---|---|
| master agent | Claude Code with the `veronica-eval` skill loaded. Picks tasks, starts one fork per batch (the runs of one context variant), saves the run reports, keeps the findings log, tests context hypotheses with further batches, and proposes context changes to the owner. It does not grade. |
| fork | a fork of the master, so it inherits the skill and the master's current investigation. Runs a batch one run after another, each end to end: starts the Editor, sends the task, answers the model's questions, watches progress, ends the run. Returns once, with the take-aways across the batch and a note per run. One fork per batch keeps the per-run detail out of the master's context. |
| Haiku | optional, at the fork's or master's choice: reads a transcript and extracts what is asked of it (notable sections, what the model is asking about now), so the caller does not read the whole transcript itself. |

- One run at a time: the Editor and the model share one machine.
- The fork plays the user. It answers fairly, as a modder who knows what they want, but does not hand out answers the model should find itself. Answers vary between runs on purpose: varied users reveal more failures.
- The fork decides when a run ends: the task is done, the run is hopeless, or the model hit the end of its context or another error. Stopping a hopeless run early saves time; running into the context limit is an accepted end.
- When the round budget runs out ("send continue"), the fork continues a productive run and ends a stuck one.
- The fork's report is free-form: whatever fits the task or the failure mode under investigation.

## Control channel

`Veronica.dll` serves a small HTTP JSON endpoint when `VERONICA_CONTROL_PORT` is set. It binds 127.0.0.1 only. Without the variable nothing listens.

| request | does |
|---|---|
| `GET /status` | whether a turn is running, how many turns ended, how the last one ended (answered, stopped, error with its text), and the context used so far |
| `GET /status?wait=<s>` | as above, but returns as soon as no turn is running, or after `<s>` seconds (at most 10 minutes) |
| `POST /send` | the body, as plain text, is sent as a user message, as if typed in the panel; refused while a turn runs |
| `POST /stop` | stops the running turn, as the panel's Stop does |

- The channel starts listening when the Veronica panel is created, after the project has loaded. A refused connection means the Editor is not ready yet.
- A raw loopback socket serves it rather than `HttpListener`, which needs a URL reservation for a non-admin user.
- The channel drives the same `Conversation` the panel shows, so the run is visible in the Editor.
- A turn counts as running from the moment `/send` accepts it, so `/status` never reports idle for a message just sent.
- Every turn ends: a context overflow or a server error fails the turn, and `/status` reports it. A run never waits on a turn that cannot finish.
- Progress during a turn is read from the transcript file, not from the channel.
- Context used is the whole prompt of the last round plus its reply: llama.cpp reports cached and newly processed prompt tokens apart.

## Transcript

When `VERONICA_RUN_DIR` is set, the plugin appends the conversation to `transcript.jsonl` in that folder as it happens, so a hung, crashed or killed Editor loses nothing.

- One JSON object per line, strings with escaped line breaks.
- Every line carries its index `i`, seconds since the run started `t`, and its `type`.
- The first line (`setup`) holds the provider, the system prompt and the tool definitions.
- Then one line per user message (`user`), assistant message (`assistant`: round, text, reasoning, tool calls with parsed arguments, round stats including context used), tool result (`tool`, with its duration), automatic user message from the tool loop (`nudge`), notice such as the round budget (`notice`) and turn end (`turn_end`: answered, stopped or error, the error text, context used).
- A failed round appends nothing to the conversation, so its partial reply is not recorded; the turn end carries the error.
- Lines are written whole when their message completes, not per streamed token.

## Run lifecycle

Every run starts a fresh Editor on a clean dev mod. The Editor's load time is accepted.

`Eval/Start-Run.ps1 -Task <name>`:
1. Kills a running Editor.
2. Resets `Assets/VeronicaDevMod` with git (restore and clean, including the Editor's caches).
3. Copies the task's fixture folder into the dev mod, when the task has one.
4. Builds `Veronica.dll`, so uncommitted context edits are in the run.
5. Creates the run folder and saves the changes under `Source` against `HEAD`, new files included, as `context.patch`, so each run records what differed.
6. Writes `run.json`: task, commit, dirty flag, model (the server's first listed unless `-Model`), start time, Editor process id, port.
7. Starts the Editor with the OpenAI-compatible provider pointed at the llama.cpp server (`-LlamaUrl`, default `http://localhost:8080/v1`), the control port (`-Port`, default 47811) and the run folder.
8. Prints the run folder, the port and the Editor's process id.

`Eval/Wait-Ready.ps1 -RunDir <dir>` waits for the control channel to answer, at most `-Seconds` (default 540), and prints `Ready`, `NotReady` or `Exited`. The wait is bounded so a fork waits with blocking calls: a fork that leaves work running in the background notifies the master each time it pauses, flooding the master's context.

`Eval/Stop-Run.ps1 -RunDir <dir>` kills the Editor and copies its logs into the run folder.

The owner keeps the llama.cpp server running; runs do not start or stop it.

## Tasks

`Eval/Tasks/<name>.md`, committed. Each holds:
- **Prompt**: the first message, as a user would type it.
- **User**: what the user wants and knows, for the fork's role-play.
- **Pass**: the criteria the run is judged against.
- **Fixture** (optional): `Eval/Tasks/<name>/`, copied over the dev mod before the run.

## Outputs

Not committed, under `.tmp/eval/`:
- `runs/<yyyyMMdd-HHmmss>-<task>/`: `run.json`, `transcript.jsonl`, `context.patch`, `logs/` (the Editor's), and the fork's `report.md`.
- `findings.md`: the master's running log of failure patterns with run references, hypotheses tested and their outcome.

Context changes reach the repo only after the owner approves them.
