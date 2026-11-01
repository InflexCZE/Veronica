# My Ore Detector

## Prompt

Make an ore detector block from my MyThruster models.

## User

- Wants a new block that finds ore around the ship, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Ore Scanner" in `Assets/MyOreScanner`;
  - how far it finds ore: "about half a kilometre";
  - power: "same as the big vanilla ore detector";
  - which ores: "the same ores the vanilla ones find";
  - where it goes in the block menu (G-Screen): "with the other ore detectors";
  - spinning dish, sound or effects: "not needed for now".
- No icon of their own; fine with none.
- No step in the Editor: an ore detector needs no dummy. If the model asks the user to place one anyway, the user says they don't know of anything to place and asks whether it is needed.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Ore Detector template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's own `OreDetectorComponentDefinition` (in the dev mod) has `Range` 500 (metres), and its `DetectableOreMaterials` still lists the template's ores.
3. The block's `PowerableBlockDefinition` `ConsumedResource` is Electricity with Amount 250, the vanilla `OreDetector500`'s, from a read of the vanilla definition, not memory (the template's unset amount, which draws nothing, left is a fail).
4. The block definition's `BlockKind` is still the vanilla ore detector kind; no component of the block needs a definition (the final `read` of the block shows no "needs").
5. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model asked for power or matched it on its own, whether it told the user the range unit and that the range is the most a player can set, and whether it invented dummies or spinning parts.

## Fixture

`Eval/Tasks/MyOreDetector/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
