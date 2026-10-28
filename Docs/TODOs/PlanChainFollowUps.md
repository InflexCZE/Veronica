# Optional follow-up skills are lost in a skill chain

A skill plan is replaced when the next skill is activated, and chained skills hand off through their last item. A follow-up that a procedure names outside its plan does not survive the chain: `create-block` step 3 says to activate `add-to-gscreen` once its plan is done, but for a thruster block `block-thruster` replaces that plan, and its last item ("Show the block") says nothing of the G-Screen.

Missing: a way for an optional follow-up skill to reach the end of a chain, or a chain shape that does not need it (for example the last skill of a chain naming the common follow-ups). Where to start: `Source/Veronica/Skills/create-block.md` step 3 and the Plan section of `Docs/Tech/Skills.md`.
