# My Wind Turbine

## Prompt

Make a wind turbine block from my MyThruster models.

## User

- Wants a new block that makes power from wind on a planet, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Wind Turbine" in `Assets/MyWindTurbine`;
  - how much power: "half of what the big vanilla wind turbine makes";
  - where the blades are on their model: "the rotor hub is 3 metres above the middle of the block, the blades are 2 metres long"; if asked about the model's origin, it is the middle of the block;
  - where it goes in the block menu (G-Screen): "with the other wind turbines".
- No icon of their own; fine with none.
- No step in the Editor: a wind turbine needs no dummy. If the model asks the user to place one anyway, the user says they don't know of anything to place and asks whether it is needed.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Wind Turbine template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's `WindTurbineComponent` uses a `WindTurbineDefinition` of its own in the dev mod (not the template's shared vanilla one) with `MaxOutput` half of the vanilla turbine's (read from vanilla, 200).
3. That definition's `VerticalOffsetForClearance` is 3 and its `TurbineOffsetToProbe` is more than 2 (past the blade tips) and at most 4; the model asked the user where the blades are before writing them.
4. The block's `PowerableBlockDefinition` `ConsumedResource` is still none and its `BlockKind` is still the vanilla wind turbine kind; no component of the block needs a definition (the final `read` of the block shows no "needs").
5. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model told the user that the turbine works only in atmosphere on a static or attached grid, whether it invented dummies or the spinning blades, whether it read the vanilla turbine's definition before suggesting values, and other definition values it changed.

## Fixture

`Eval/Tasks/MyWindTurbine/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
