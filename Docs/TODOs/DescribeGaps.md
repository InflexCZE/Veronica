# describe: gaps

**Exists:** `describe` lists a type's members from the doc comments the serialization generator bakes into `AccessibleTypeInfo`, with relations and units (`Docs/Tech/EditorTools.md`, describe).

**Missing:**
- **Doc files in a shipped Editor:** component summaries come from the XML doc files beside the game's assemblies, as the inspector's Documentation section does. Whether a released Editor carries those files is unchecked; without them a component has relations and fields but no summary.
- **Prefab visibility below a list:** a component's fields are listed with the inspector's prefab rule. An element type reached through a list inside a component's object builder (`describe(SomeComponent, "List[0]")`) is listed from a fresh instance of its own, without that rule, so it can show fields `read` hides there. No vanilla component has been seen to need it.
