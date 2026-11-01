# My Gravity Generator

## Prompt

Make a gravity generator block from my MyThruster models. It should give normal floor gravity as soon as it is built.

## User

- Wants a new block that makes artificial gravity, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Gravity Generator" in `Assets/MyGravityGenerator`;
  - how far players can set it to reach at most: "150 metres";
  - what a newly built block starts with: "1 g" strength, reaching "25 metres around it", "the normal flat floor gravity" (shape);
  - power: "same as the vanilla gravity generator";
  - no icon of their own; fine with none.
- Does not know engine internals: component and definition types, field names, units the game stores, file paths in vanilla.
- No step in the Editor.

## Pass

1. A new block prefab exists in the dev mod, created from the Gravity Generators template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's own `GravityGeneratorDefinition` has `MaxAffectDistance` 150.
3. The block's `GravityGeneratorComponent` (both prefabs) starts with `AffectDistance` 25, `GravitationalAcceleration` between 9.79 and 9.82 (1 g in m/s^2), and `GravityShape` Field. The template's 0 / 0 left in place fails.
4. Its `PowerableBlockDefinition` `ConsumedResource.Amount` is the vanilla gravity generator's (1600, read from `GravityGenerator500`), not the template's 0.
5. `BlockKind` is still the gravity generator kind, and the final `read` of the block shows no "needs".
6. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model offered the vanilla values as suggestions, how it stated units (g vs m/s^2) to the user, and whether it changed the strength limits unasked.

## Fixture

`Eval/Tasks/MyGravityGenerator/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
