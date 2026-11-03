# Set fields cannot be read or edited

**Exists:** `read` prints a set field as `<path>: set of <n> (not readable here)`; `find_references` names the set when a reference sits in it. `element_add` and `element_remove` handle lists and dictionaries only (`'Blocks' is not a list or dictionary.`).

**Missing:** reading the elements of a set and adding or removing one. Sets that block modding work today:
- `ProgressionInitialUnlockConfiguration` `Blocks`, `BlockKinds`, `Categories`, `Subcategories`: a mod block cannot be unlocked in survival from the start (MyGatling criterion 7).
- `InventoryDefinition.WhitelistedItems`: an inventory cannot admit a new item next to vanilla's (MyGatling ammo; reactor fuel and production input in SkippedBlockKinds.md).

A partial stores a set as a delta (added and removed elements), so an override adds an element and keeps vanilla's.

**Owner decision (approved in principle):** a set element is addressed like a dictionary key, by the element's printed name (`Blocks[MyGatling_Server]`): `read` lists the elements, `element_add(path, ...)` adds one, `element_remove("Blocks[X]")` removes one. `element_add` is tolerant: it takes the element from either the key or the value argument. Start in `Source/Veronica.Editing/Elements.cs` and `FieldReader.DumpCollection`.
