# Prefab tag move

**Exists:** a component's tags are added and removed with `element_add` and `element_remove` on `Components[X].Tags`. Adding a tag another component holds, and removing a tag another component requires, are refused.

**Missing:** moving a tag from one component to another. The add is refused while the old holder has it, and the remove is refused while a dependent requires it, so a tag that dependents rely on cannot move. Rare; revisit when a task needs it (owner decision: how a move is expressed).
