# Tool rewards

**Exists:** `ClientToolLoop` refunds half the round budget for a tool result with `EarnedReward`, but no tool sets it. `Prompts.ChatLimits` raises `MaxRounds` to 50 so that a whole block build fits in one turn.

**Missing:** decide which tool results earn a reward (for example a successful write), set `EarnedReward` there, and bring `MaxRounds` back down to the default. Owner decision: what counts as progress.
