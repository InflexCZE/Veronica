---
name: veronica-eval
description: Run Veronica eval tasks in the Editor with the local model, one fork per batch, collect transcripts, find failure patterns and propose context changes. Design in Docs/Tech/Eval.md.
---

# Veronica eval

Read first: `Docs/Tech/Eval.md` (roles, control channel, transcript, run lifecycle, tasks, method) and the Context engineering rules in `Docs/Tech/Skills.md`.
You are the master. Each batch is a fork of you, so everything here reaches the fork too.

## Before a batch

- The owner's llama.cpp server must be up: `curl -s http://localhost:8080/health` (or the URL the owner gave). If not, ask the owner; never start it yourself.
- Read `.tmp/eval/findings.md` if it exists: the patterns and hypotheses so far.
- One run at a time. Wait for a fork to finish before the next one starts.

## Master

- Start each batch with the Agent tool, `subagent_type: "fork"`. The fork runs all of it, one run after another, and returns once.
- The brief names: the model and server URL, the commit and what the working tree changes, the tasks in order with run counts, what is new since the last batch, what to look at (a hypothesis, a failure mode to provoke, regressions), and when to stop the batch (build fails, the Editor will not start twice, the server is down).
- For a hypothesis batch, edit the context (skills in `Source/Veronica/Skills`, the system prompt, tools) in the working tree before the fork starts. `Start-Run.ps1` saves the diff in each run folder. Change nothing under `Source` or `Eval/Tasks` until the batch ends.
- After the batch, add to `.tmp/eval/findings.md`: patterns with run folder references, hypotheses and their outcome.
- Do not grade runs again; the fork's report is the verdict. Re-read a transcript, or have a fork or Haiku do it, only to verify a pattern.
- For a fact about the game, send a fork that reads the SE2 source only (`SE2Root` in `Directory.Build.local.props`), with no Editor or MCP while a batch runs.
- After a batch, revert experimental context edits that did not hold, then propose context changes to the owner with the runs that support them. Commit only what the owner approves.

## Fork: a batch of runs

Wait in the foreground only: blocking tool calls with a timeout of up to 600000 ms, repeated as needed. Never run a command in the background or use Monitor. A fork that stops with background work running notifies the master each time, which floods the master's context. Leave files only in run folders and `.tmp/`. Do the runs one after another, each as below, and return to the master once, after the last.

1. `powershell -File Eval/Start-Run.ps1 -Task <name>` (add `-LlamaUrl` and `-Model` if not the defaults). It prints the run folder, port and Editor process id as soon as the Editor starts. Then `powershell -File Eval/Wait-Ready.ps1 -RunDir <dir>` until it prints `Ready` (`NotReady`: call again; `Exited`: the Editor died during load, see its logs after Stop-Run). On a model's first run, check that its tool calls work before going on.
   Then `powershell -File Eval/Get-Validation.ps1 -RunDir <dir> -Label start`. Exit 2 (an error outside the mod, e.g. vanilla): the Editor loaded corrupted and the run would be unreliable; Stop-Run and start it again once; if it fails again, stop the batch and report to the master. Exit 1 at start (the fixture has errors): go on with the run; those errors are the fixture's, not the model's, and do not count at the end. Report them to the master, who records them in a TODO for the task's fixture.
2. Read the task file. Write the message to `<run folder>/msg.txt`, then `curl -s --data-binary @<run folder>/msg.txt http://127.0.0.1:<port>/send` (the body is the message, plain text). A 409 says why it was refused.
3. Wait for the turn to end: `curl -s "http://127.0.0.1:<port>/status?wait=540"` in the foreground, again until `busy` is false or `questions` is not empty (a question card is open; with the Claude provider the turn stays busy while it waits). While waiting, read the new lines of `transcript.jsonl` (one JSON object per line, `i` is the line index) when you want to see progress; `stats.context` on assistant lines is the context used. A `wiki` call's `tool` line names its research run's file (`sessions`, e.g. `wiki-1.jsonl`): read it to judge the research (what it read, whether it over-researched, whether its answer helped).
4. When the turn ends, read what the model did and said, then:
   - **Question**: answer as the user in the task's User section would, with its values. Do not hand out what the model should find itself (paths in vanilla, values it can read). Vary your phrasing; a terse or slightly vague answer is fine when a real user would give one. Restating what the user wants unasked is a nudge; note it. When `questions` is not empty, answer the card: write a JSON array with one answer per question, in order (an option's text, your own text, or `null` to skip), to `<run folder>/answer.json`, then `curl -s --data-binary @<run folder>/answer.json http://127.0.0.1:<port>/answer`. A question asked in plain text instead of the card is answered with `/send`; note it.
   - **User step in the Editor**: play it through the MCP endpoint as the task's User section says.
   - **Round budget out**: send "continue" if the run is making progress; otherwise end the run.
   - **Done**: end the run.
   - **Error** (context full, server error): end the run.
   - **Hopeless** (loops, repeats failing calls, lost the goal): end it, even before the context runs out. Running into the context limit is also an accepted end.
5. `powershell -File Eval/Get-Validation.ps1 -RunDir <dir> -Label end`, then `powershell -File Eval/Stop-Run.ps1 -RunDir <dir>`.
6. Judge the run against the task's Pass criteria. Any validation error at the end that was not already in `validation-start` fails the run, whatever the criteria say; name each error in the verdict. Warnings do not fail a run; list new ones in `results.md` only. If the transcript is too long to keep reading, use a Haiku subagent to extract notable sections or what the model is asking about, instead of reading it all.
7. Write the run's note to `<run folder>/results.md` before starting the next run: the run folder, the verdict per criterion, how the run ended, user turns and context used, notable moments with transcript line numbers, values the model invented instead of asking, and every answer you gave as the user. Vary your answers between runs.

After the last run, return the take-aways across the batch first (verdict table, what the context change did, failure modes with run and line references, proposals split into skill wording, system prompt, tool shape and knowledge), then one line per run: its folder and verdict. The full notes stay in the `results.md` files.
