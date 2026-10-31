# Question cut before the wait

**Exists:** questions to the user go through `ask_user`, which carries them as arguments (`Docs/Tech/Agent.md`, Questions to the user). `wait_for_user` is left for steps the user does in the Editor; a waiting round with no text of its own is refused, and the model is told to write it in the same reply.

**Missing:** the 27B sometimes wrote only the lead-in of its question, e.g. "Quick question before I generate the block:", then called `wait_for_user`. The text is not empty, so the turn ends and the user sees no question; the user has to ask what it wanted, which costs a round. Eval rate before `ask_user`: 1 of 10 (W1, inside a parallel-call burst), 1 of 4 build runs (H1), 2 of 6 (C1, each with two parallel `wait_for_user` calls after a lead-in ending in ":").

Owner decision: not fixed in the loop. Check in the next eval batches whether the cut still happens: a question still asked in plain text before `wait_for_user`, or an Editor step cut to its lead-in. Delete this file if it does not.

**Start at:** `Source/Veronica.Agent/ClientToolLoop.cs`, the silent-wait check.
