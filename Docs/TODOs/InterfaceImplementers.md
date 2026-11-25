# Finding which of a prefab's components implement an interface

**Exists:** `describe` on an interface lists every component type implementing it, game-wide, and on a component lists the interfaces it implements (`Docs/Tech/Tools_Readers.md`, describe). `component_add` names, in its reply, the components that provide an interface dependency the Editor's filling skips (`Docs/Tech/Tools_Creators.md`, interface dependencies).

**Missing:** on one prefab, which of its components implement a given interface, such as the components that add to a block's power draw through `IConsumedResourceModifier`. Today the model has to match the prefab's component list against describe's game-wide list by itself.

**Where to start:** the "Implemented by" lookup in `DescribeTool.cs` (`EditorTypeCatalog`), applied to the components a `read` of the prefab lists.

**Owner decision:** wanted. Open question: a new tool, or a mode of `describe` or `read`?
