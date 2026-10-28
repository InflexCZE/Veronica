# Plan checks

**Exists:** `mark_completed` takes the model's word and its one-line proof (`Docs/Tech/Skills.md`, Plan).

**Missing:** deterministic checks per plan item, for example that the block's name field was written before "Fill block name" can be marked completed, with a result telling the model what is still missing. Which items get checks, and how a skill file declares them, is an owner decision.

**Start at:** `Source/Veronica.Agent/SkillPlan.cs`.
