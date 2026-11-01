# My Sound Block

## Prompt

Make a sound block from my MyThruster models, so players can play the usual sound block sounds on it.

## User

- Wants a new block that plays sounds players pick, as a speaker on their ship, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Speaker" in `Assets/MySpeaker`;
  - which sounds: "the same ones the normal sound block has";
  - power: "same as the normal sound block";
  - volume, looping or which sound plays: "players set that up in game";
  - new sounds of their own: "not now, the normal ones are fine";
  - where it goes in the block menu (G-Screen): "with the other sound blocks".
- No icon of their own; fine with none.
- No step in the Editor: a sound block needs no dummy. If the model asks the user to place one anyway, the user says they don't know of anything to place and asks whether it is needed.
- Does not know engine internals: component and definition types, field names, file paths in vanilla, audio event names.

## Pass

1. A new block prefab exists in the dev mod, created from the Sound Block template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model` is the imported `MyThruster` model (`FractureModel` and `DamagedModel` set to the fractured and deformed models, as vanilla's sound block has them, or left none: reported, not judged, since the block does not break apart).
2. The block has a `SoundBlockServerComponent` on the server whose `SoundBlockDefinition` offers the vanilla sound block's sounds: the vanilla `SoundBlock25_SoundBlockDefinition` itself, or a copy of it in the dev mod with every entry kept.
3. The block has a `SoundBlockComponent` on the client.
4. The block's `PowerableBlockDefinition` `ConsumedResource` is still Electricity with Amount 0, as the vanilla sound block's; the block definition's `BlockKind` is still the vanilla sound block kind; no component of the block needs a definition (a `read` of the block shows no "needs").
5. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model told the user that the playlist, volume and looping of a built block are set by players in the terminal, whether it invented per-block settings, dummies or a way to add new sounds, whether it copied the definition without a reason, and whether it read the vanilla sound block before adding the components.

## Fixture

`Eval/Tasks/MySoundBlock/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
