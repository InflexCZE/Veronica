# Prefab component edits: review findings not fixed

**Exists:** the prefab component tools edit a block through the Editor's prefab layer (`Source/Veronica/Tools/Block.cs`).

**Missing:**
- **Read-only per file:** any edit is refused when any of the block's prefabs or compositions is read-only, even an edit that touches one side only. In practice a block's files sit in one project; a block mixing projects (an override of one side) would be refused whole.
- **No rollback:** `component_add` changes the loaded prefabs and compositions step by step (add, dependencies, definition). An exception between the steps leaves the in-memory state half changed and unsaved, and a later save writes it. The inspector's own Add Component behaves the same.
- **Duplicate from a dependency:** a component added as another's dependency (the weapon's `InventoryComponent`, with no definition) and then added explicitly in the same batch of calls becomes a second component (`Inventory1`). Seen in MyGatling H1 run 3 (`.tmp/eval/runs/20261103-201645-MyGatling`, lines 127-131).
- **Interface dependencies:** the Editor's dependency filler skips a dependency declared as an interface. `component_add` fills `IRenderParent` with `ParentRenderAdapterComponent` on a block (a prefab with a `CubeBlockComponent`, always a child entity; owner decision) and names every other unfilled interface dependency in its reply, with the components offered that provide it. Still open: which provider a prefab that is not a block gets (`ParentRenderAdapterComponent` or `RootEntityRenderComponent` for `IRenderParent`), and other interface tags.
- **Side override on a one-sided prefab:** the side list (`InventoryComponent` to the server) applies only on a block with both sides. On a prefab with no binding, or a binding with one prefab, the type is still offered and goes on that one prefab, whichever side it is.
