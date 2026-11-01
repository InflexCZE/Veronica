# My Antenna

## Prompt

Make an antenna block from my MyThruster models.

## User

- Wants a new block that broadcasts like a radio antenna, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Antenna" in `Assets/MyAntenna`;
  - range: "30 km" (if asked whether that is a lot: "it's a long range relay, yes");
  - power: "same as the big vanilla antenna";
  - where it goes in the block menu (G-Screen): "with the other antennas";
  - effects or sounds: "not needed for now".
- No icon of their own; fine with none.
- No step in the Editor: an antenna needs no dummy. If the model asks the user to place one anyway, the user asks whether it is needed.
- Once the block is done (the model shows it or says it is finished), the user asks one follow-up: "Can it show 'Relay Alpha' on the HUD instead of the ship name?" Then ends the run after the answer.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla power numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Antennas template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's `AntennaComponent` uses an `AntennaDefinition` in the dev mod (a copy of the template's shared one) with `MaxBroadcastRange` 30000; the vanilla `AntennaDefinition` is unchanged (any write to a vanilla file is a fail).
3. The block's `PowerableBlockDefinition` `ConsumedResource` is Electricity with Amount 150, the vanilla `Antenna1250`'s, from a read of the vanilla definition, not memory (the template's 160 left is a fail).
4. The block definition's `BlockKind` is still vanilla `AntennaBlockKindDefinition`; no component of the block needs a definition (the final `read` of the block shows no "needs").
5. The model called `show` on the new prefab, and nothing outside the dev mod was changed.
6. The HUD text follow-up: the answer says the marker shows the grid's name and players set a custom message in the block's terminal, per built block; the model writes nothing for it and invents no field.

Not required, reported: whether the model asked the wiki for the follow-up, whether it asked power or matched it on its own, whether it told the user what the numbers mean (and with which units), and whether it invented dummies.

## Fixture

`Eval/Tasks/MyAntenna/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
