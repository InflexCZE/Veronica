# My Warhead

## Prompt

Make a warhead block from my MyThruster models.

## User

- Wants a new explosive block, a demolition charge, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Demo Charge" in `Assets/MyDemoCharge`;
  - how big the blast is: "as big as the large vanilla warhead";
  - the countdown: "10 seconds";
  - whether it goes off when shot or badly damaged: "yes, like the normal warhead";
  - power: "same as the normal warhead";
  - whether it should start armed or counting down: "no, players arm it in game";
  - where it goes in the block menu (G-Screen): "with the other warheads".
- No icon of their own; fine with none.
- No step in the Editor: a warhead needs no dummy. If the model asks the user to place one anyway, the user says they don't know of anything to place and asks whether it is needed.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Warhead template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's `TimerExplosiveBlockComponent` uses a `TimerExplosiveBlockDefinition` of its own in the dev mod with `DefaultTimeToExplosion` 00:00:10 (a bare 10, read as 10 days, is a fail).
3. That definition's `Explosion` is the large vanilla warhead's `ExplosionDefinition` (`Warhead250_ExplosionDefinition`), or a copy of it with the same `ExplosionRadius` and damages; its `MinimumIntegrityForExplosion` is above 0 (vanilla's 0.5 reported).
4. The block's `ExplosiveBlockEffectsComponent` has an `ExplosiveBlockEffectsDefinition` (a vanilla warhead's; which one is reported), and no component of the block needs a definition (a `read` of the block shows no "needs").
5. The block's `PowerableBlockDefinition` `ConsumedResource` is still Electricity with Amount 0, as the vanilla warheads'; the block definition's `BlockKind` is still the vanilla warhead kind.
6. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model read a vanilla warhead before suggesting values, whether it told the user that arming and the countdown are done by players in the terminal and need power, and whether it invented dummies or a preset armed state.

## Fixture

`Eval/Tasks/MyWarhead/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
