# Question cut before the wait

**Exists:** a waiting round (`wait_for_user`) with no text of its own is refused, and the model is told to write its question in the same reply (`Docs/Tech/Agent.md`, turn-ending tools).

**Missing:** the 27B sometimes writes only the lead-in of its question, e.g. "Quick question before I generate the block:", then calls `wait_for_user`. The text is not empty, so the turn ends and the user sees no question; the user has to ask what it wanted, which costs a round. Seen 3 times in 33 eval runs.

Owner decision: not fixed in the loop now. A dedicated ask-the-user tool that carries the question as an argument is expected to remove it; revisit if it persists after that tool exists.

**Start at:** `Source/Veronica.Agent/ClientToolLoop.cs`, the silent-wait check.
