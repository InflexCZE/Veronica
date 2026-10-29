# Prefab child entities

**Exists:** the prefab component tools (`read` on a block, `component_add`, `component_remove`) work on a block's root entity.

**Missing:** child entities (subparts, in the root's `HierarchyComponent.Children`). The tools neither show them nor add or remove their components. Start from `IEntityNode.Children` in the Editor's prefab layer; each child has its own composition. The path form for a child's components is not decided (owner decision).
