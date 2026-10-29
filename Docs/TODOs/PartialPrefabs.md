# Partial prefabs

**Exists:** `create_partial` makes partial copies and overrides of definitions through the Editor's Partial Definition generator.

**Missing:** partial copies and overrides of prefabs (`PrefabDefinition`, `PrefabBindingDefinition`). The Editor makes those through a separate path, `PrefabGeneratorProvider.CreatePartialPrefabAsync`, which also partializes the prefab's component definitions. `create_partial` refuses prefabs and says so.

Once an override exists, check live that `component_add` on it lands wholly in the override: the Editor's prefab layer loads the composition through `TryGetOverrideManipulator` but the prefab through `GetManipulator` of the opened asset, and no Editor test adds a component to an override.
