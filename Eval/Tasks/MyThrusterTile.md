# My Thruster tile

## Prompt

Put my thruster into its own tile in the G-Screen, next to the ion thrusters.

## User

- Has a finished ion thruster block, "My Thruster", in `Assets/MyThrusterBlock`, made from the Ion Thrusters template with their own imported models. It is a size variant in the vanilla Ion Thrusters tile now.
- Wants it in a tile of its own, beside the vanilla ion thrusters' tile. Does not know the G-Screen's internal names; when asked where, says "where the ion thrusters are".
- When asked: the tile is "My Thrusters"; the block kind's description is "Ion thruster from my own model". No preference for order. No preference for the folder until asked, then picks something sensible (e.g. `Assets/GScreen`).
- The G-Screen is visible only in game; the user does not start the game during the run.
- Does not know engine internals: definition types, field names, file paths in vanilla.

## Pass

1. A new `BlockKindDefinition` in the dev mod, and the block's `PowerableBlockDefinition.BlockKind` points at it (no longer at vanilla's `IonThrusterBlockKindDefinition`).
2. A new `BlockGroupDefinition` named "My Thrusters" holds the kind, listed once, through the kind's `BlockGroups` or the tile's `BlockKinds` (both are authored and merged at load).
3. That tile's `Subcategories` holds vanilla's `SpaceSubcategoryDefinition` (Movement > Space, where the ion thrusters' tile is).
4. No vanilla G-Screen definition is overridden, and no new subcategory or category is created.
5. The model called `show` on the block, and nothing outside the dev mod was changed.

## Fixture

`Eval/Tasks/MyThrusterTile/` holds the imported models (as MyThrusterBlock's fixture) and the finished block in `Assets/MyThrusterBlock`: name, three models, Ion Thruster 100 thrust and power, nozzle placed.
