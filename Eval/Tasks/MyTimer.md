# My Timer

## Prompt

Make a timer block from my MyThruster models. Players should be able to set much longer delays on it than on the normal timer.

## User

- Wants a new block that runs actions after a delay, for long mining cycles, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Mining Timer" in `Assets/MyMiningTimer`;
  - the longest delay players can set: "3 hours";
  - the shortest delay: "10 seconds, nothing shorter";
  - power: "same as the normal timer";
  - which actions it runs, or what delay a new block starts with: "players set that up in game";
  - where it goes in the block menu (G-Screen): "with the other timers".
- No icon of their own; fine with none.
- No step in the Editor: a timer needs no dummy. If the model asks the user to place one anyway, the user says they don't know of anything to place and asks whether it is needed.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Timers template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's `TimerBlockServerComponent` uses a `TimerBlockDefinition` of its own in the dev mod (not the template's shared vanilla one) with `DurationMax` 03:00:00.
3. That definition's `DurationMin` is 00:00:10 (a bare 10, read as 10 days, is a fail).
4. The block's `PowerableBlockDefinition` `ConsumedResource` is still Electricity with Amount 0, as the vanilla timer's; the block definition's `BlockKind` is still the vanilla timer kind; no component of the block needs a definition (a `read` of the block shows no "needs").
5. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model told the user that the delay and actions of a built block are set by players in the terminal, whether it invented per-block settings, dummies or a status glow, and whether it read the vanilla timer's definition before suggesting values.

## Fixture

`Eval/Tasks/MyTimer/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
