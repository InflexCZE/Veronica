---
name: veronica-eval
description: Run Veronica eval tasks in the Editor with the local model, one fork per run, collect transcripts, find failure patterns and propose context changes. Design in Docs/Tech/Eval.md.
---

# Veronica eval

Read `Docs/Tech/Eval.md` first: roles, control channel, transcript, run lifecycle, task format.
You are the master. Each run is a fork of you, so everything here reaches the fork too.

## Before a batch

- The owner's llama.cpp server must be up: `curl -s http://localhost:8080/health` (or the URL the owner gave). If not, ask the owner; never start it yourself.
- Read `.tmp/eval/findings.md` if it exists: the patterns and hypotheses so far.
- One run at a time. Wait for a fork to finish before the next one starts.

## Master

- Start each run with the Agent tool, `subagent_type: "fork"`. The prompt names the task and what this run should look at (a hypothesis, a failure mode to provoke, or none).
- For a hypothesis run, edit the context (skills in `Source/Veronica/Skills`, the system prompt) in the working tree before the fork starts. `Start-Run.ps1` saves the diff in the run folder.
- After each run, save the fork's report as `report.md` in its run folder, and add to `.tmp/eval/findings.md`: patterns with run folder references, hypotheses and their outcome.
- Do not grade runs again; the fork's report is the verdict. Re-read a transcript, or have a fork or Haiku do it, only to verify a pattern.
- After a batch, revert experimental context edits, then propose context changes to the owner with the runs that support them. Commit only what the owner approves.

## Fork: one run

Wait in the foreground only: blocking tool calls with a timeout of up to 600000 ms, repeated as needed. Never run a command in the background or use Monitor. A fork that stops with background work running notifies the master each time, which floods the master's context. Return to the master once, with the final report.

1. `powershell -File Eval/Start-Run.ps1 -Task <name>` (add `-LlamaUrl` if not the default). It prints the run folder, port and Editor process id as soon as the Editor starts. Then `powershell -File Eval/Wait-Ready.ps1 -RunDir <dir>` until it prints `Ready` (`NotReady`: call again; `Exited`: the Editor died during load, see its logs after Stop-Run).
2. Read the task file. Write the message to `<run folder>/msg.txt`, then `curl -s --data-binary @<run folder>/msg.txt http://127.0.0.1:<port>/send` (the body is the message, plain text). A 409 says why it was refused.
3. Wait for the turn to end: `curl -s "http://127.0.0.1:<port>/status?wait=540"` in the foreground, again until `busy` is false. While waiting, read the new lines of `transcript.jsonl` (one JSON object per line, `i` is the line index) when you want to see progress; `stats.context` on assistant lines is the context used.
4. When the turn ends, read what the model did and said, then:
   - **Question**: answer as the user in the task's User section would. Answer fairly, but do not hand out what the model should find itself (paths in vanilla, values it can read). Vary your phrasing; a terse or slightly vague answer is fine when a real user would give one.
   - **Round budget out**: send "continue" if the run is making progress; otherwise end the run.
   - **Done**: end the run.
   - **Error** (context full, server error): end the run.
   - **Hopeless** (loops, repeats failing calls, lost the goal): end it, even before the context runs out. Running into the context limit is also an accepted end.
5. `powershell -File Eval/Stop-Run.ps1 -RunDir <dir>`.
6. Judge the run against the task's Pass criteria. If the transcript is too long to keep reading, use a Haiku subagent to extract notable sections or what the model is asking about, instead of reading it all.
7. Return the report (the master saves it as `report.md` in the run folder; forks cannot write files there): free-form, whatever fits the task or the failure mode under investigation. Include the pass verdict, how the run ended, notable moments with transcript line numbers, and every answer you gave as the user.
