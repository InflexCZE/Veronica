# My Sensor

## Prompt

Make a sensor block from my MyThruster models. It should watch a much bigger area than the normal sensor.

## User

- Wants a new block that detects things around it (ships, players) and triggers actions, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Perimeter Sensor" in `Assets/MyPerimeterSensor`;
  - how far it can watch: "up to 100 metres in every direction";
  - how big the field is when the block is first built: "10 metres around it";
  - power: "same as the normal sensor";
  - what it detects or which actions it triggers: "players set that up in game";
  - where it goes in the block menu (G-Screen): "with the other sensors".
- No icon of their own; fine with none.
- No step in the Editor: a sensor needs no dummy. If the model asks the user to place one anyway, the user says they don't know of anything to place and asks whether it is needed.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Sensor template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's `SensorComponent` uses a `SensorComponentDefinition` of its own in the dev mod (not the template's shared vanilla one) with `MaxDetectionBoxSize` {100, 100, 100}.
3. That definition's `InitialDetectionBoxSize` is {10, 10, 10}: the distance from the block in each direction, not doubled (the vanilla 1 left, or 20 written as a full width, is a fail).
4. The block's `PowerableBlockDefinition` `ConsumedResource` is still Electricity with Amount 0, as the vanilla sensor's; the block definition's `BlockKind` is still the vanilla sensor kind; no component of the block needs a definition (the final `read` of the block shows no "needs").
5. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model told the user that detection filters and toolbar actions are set by players in the terminal, whether it invented per-block settings or dummies, and whether it read the vanilla sensor's definition before suggesting values.

## Fixture

`Eval/Tasks/MySensor/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
