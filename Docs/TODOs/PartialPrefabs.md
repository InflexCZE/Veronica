# Partial prefabs

**Exists:** `create_partial` makes partial copies and overrides of definitions through the Editor's Partial Definition generator.

**Missing:** partial copies and overrides of prefabs (`PrefabDefinition`, `PrefabBindingDefinition`). The Editor makes those through a separate path, `PrefabGeneratorProvider.CreatePartialPrefabAsync`, which also partializes the prefab's component definitions. `create_partial` refuses prefabs and says so.
