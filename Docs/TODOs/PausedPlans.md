# Paused plans outlive a change of task

Activating a skill pauses the open plan under it, and the paused plan resumes when the nested one is done. When the user moves to an unrelated task mid-plan, the paused plans stay: they come back once the new work's plan is done, and the model has to skip their items one by one with `mark_skipped`. There is no tool to drop a paused plan.

Missing: evidence from eval runs on how often this happens and what it costs; then a decision on a drop tool (owner decision). The stack also has no depth cap; the same runs show whether one is needed. No eval run has yet nested three plans deep on a new block, or activated two function skills from the same item. Where to start: `Source/Veronica.Agent/SkillPlan.cs` and the Plan section of `Docs/Tech/Skills.md`.
