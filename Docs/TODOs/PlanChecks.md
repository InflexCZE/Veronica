# Plan checks

**Exists:** `mark_completed` takes the model's word and its one-line proof (`Docs/Tech/Skills.md`, Plan).

**Missing:** deterministic checks per plan item, for example that the block's name field was written before "Fill block name" can be marked completed, with a result telling the model what is still missing. Which items get checks, and how a skill file declares them, is an owner decision.

The owner leans towards an independent reviewer agent that checks the task's objectives against the result, rather than per-item programmatic checks, which lock in the skill and do not generalize when the model needs to deviate from it. The proofs and skip reasons recorded on the plan items are the claims such a reviewer would verify against the project.

**Start at:** `Source/Veronica.Agent/SkillPlan.cs`.
