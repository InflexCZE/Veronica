# Question cut before the wait

**Exists:** a waiting round (`wait_for_user`) with no text of its own is refused, and the model is told to write its question in the same reply (`Docs/Tech/Agent.md`, turn-ending tools).

**Missing:** the 27B sometimes writes only the lead-in of its question, e.g. "Quick question before I generate the block:", then calls `wait_for_user`. The text is not empty, so the turn ends and the user sees no question; the user has to ask what it wanted, which costs a round. Seen 3 times in 33 eval runs.

One option is to treat a waiting round whose text ends with a colon like a silent wait and refuse it with the same message. Whether a check this narrow is worth it, or the fix belongs in the wording, is an owner decision.

**Start at:** `Source/Veronica.Agent/ClientToolLoop.cs`, the silent-wait check.
