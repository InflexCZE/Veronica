# Prefab component edits: review findings not fixed

**Exists:** the prefab component tools edit a block through the Editor's prefab layer (`Source/Veronica/Tools/Block.cs`).

**Missing:**
- **Read-only per file:** any edit is refused when any of the block's prefabs or compositions is read-only, even an edit that touches one side only. In practice a block's files sit in one project; a block mixing projects (an override of one side) would be refused whole.
- **No rollback:** `component_add` changes the loaded prefabs and compositions step by step (add, dependencies, definition). An exception between the steps leaves the in-memory state half changed and unsaved, and a later save writes it. The inspector's own Add Component behaves the same.
