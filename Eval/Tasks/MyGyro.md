# My Gyro

## Prompt

Make a gyroscope block from my MyThruster models.

## User

- Wants a new block that turns the ship, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Gyro" in `Assets/MyGyro`;
  - how strong: "as strong as the vanilla medium gyroscope, the 2.5 m one";
  - power: "same as that one";
  - where it goes in the block menu (G-Screen): "with the other gyroscopes";
  - spinning parts, sound or effects: "not needed for now".
- No icon of their own; fine with none.
- No step in the Editor: a gyroscope needs no dummy. If the model asks the user to place one anyway, the user says they don't know of anything to place and asks whether it is needed.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla torque numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Gyroscopes template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's own `GyroscopeDefinition` (in the dev mod) has `MaxTorque` 44376940, the vanilla `Gyroscope250`'s, from a read of the vanilla definition, not memory.
3. The block's `PowerableBlockDefinition` `ConsumedResource` is Electricity with Amount 25, the vanilla `Gyroscope250`'s (the template's 5 left is a fail).
4. The block definition's `BlockKind` is still vanilla `GyroscopeBlockKindDefinition`; no component of the block needs a definition (the final `read` of the block shows no "needs").
5. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model asked for power or matched it on its own, whether it told the user what the torque and power numbers mean (and with which units), and whether it invented dummies or spinning parts.

## Fixture

`Eval/Tasks/MyGyro/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
