# The 9B model does not finish the eval tasks reliably

**Exists:** the plan feature, "How we work", the plan tools with proofs, `wait_for_user`, tolerant lookup with nearest-name hints, and the transform short form bring qwen3.8-27b to 3 of 3 on both eval tasks (`IonThruster100Block`, `MyThrusterBlock`). qwen3.5-9b stays at 0-2 of 3 per batch. Run reports and the findings log are under `.tmp/eval/` (not committed).

**Missing (owner decision: parked until the wiki and a better loop exist):**
- The 9B asks in plain text, gets the end gate, then calls `wait_for_user` alone; that round has no text, so the call is refused and the turn ends on a second stop. Proposed, not applied: accept a bare `wait_for_user` when the reply the end gate answered had text and no tool calls.
- Nozzle proofs on the 9B often leave out the values read back. Proposed, not applied: the nozzle item says to put the values read in the proof.
- The 9B sometimes reads a skill and never activates it, and its proofs land on the wrong items when it runs ahead of the plan. The activation hint on edits did not change this. The 27B also once worked `block-thruster` without activating it, under `create-block`'s hand-off item.
- The activation hint is appended to edits made through the MCP endpoint too, and those edits count towards its interval.

**Start at:** `.tmp/eval/findings.md` (patterns with run references), `Source/Veronica.Agent/ClientToolLoop.cs` (turn-ending tools), `Source/Veronica/Skills/block-thruster.md`.
