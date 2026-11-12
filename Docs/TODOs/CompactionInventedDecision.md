# A compaction summary can turn a conditional step into a user decision

**Exists:** the summarizer brief says a user decision is what the user said, not what the model chose (`Docs/Tech/Compaction.md`). Open plans are handed to it with their items.

**Missing:** seen once, Bonsai at a 25.6K window (MyAssembler 20261112-154633): create-block's item "survival, when the user wants it" lost its condition in one summary ("activate block-survival to make the block buildable"), and the next summaries listed "buildable in survival from the start" as a user decision. The user was never asked; the model unlocked the block. One case in 17 compactions over Bonsai and the 27B, so a fix cannot be confirmed by runs yet.

**Where to start:** if it recurs, the candidate fix is a brief line: an open plan item that depends on the user's choice stays open with its condition unless the user answered it. Owner decision: no generic summarizer tuning; act on this only when it shows up again.
