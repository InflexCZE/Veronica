# My Mods category

## Prompt

I want my thruster in its own G-Screen category called My Mods.

## User

- Has a finished ion thruster block, "My Thruster", in `Assets/MyThrusterBlock`, made from the Ion Thrusters template with their own imported models. It is a size variant in the vanilla Ion Thrusters tile now.
- Wants a new G-Screen category "My Mods" holding the block.
- When asked: the category's tooltip is "Blocks from my mods"; the subcategory is "Thrusters" with tooltip "My thrusters"; the tile is "My Thrusters"; the block kind's description is "Ion thruster from my own model". No preference for order. No preference for the folder until asked, then picks something sensible (e.g. `Assets/GScreen`).
- The G-Screen is visible only in game; the user does not start the game during the run.
- Does not know engine internals: definition types, field names, file paths in vanilla.

## Pass

1. A new `BlockKindDefinition` in the dev mod, and the block's `PowerableBlockDefinition.BlockKind` points at it (no longer at vanilla's `IonThrusterBlockKindDefinition`).
2. A new `BlockGroupDefinition` (the tile) named "My Thrusters" holds the kind, listed once, through the kind's `BlockGroups` or the tile's `BlockKinds` (both are authored and merged at load).
3. The tile's `Subcategories` holds a new `SubcategoryDefinition` with the agreed name and tooltip.
4. The subcategory's `Categories` holds a new `CategoryDefinition` named "My Mods", with the agreed tooltip.
5. No vanilla G-Screen definition is overridden.
6. The model called `show` on the block, and nothing outside the dev mod was changed.

## Fixture

`Eval/Tasks/MyModsCategory/` holds the imported models (as MyThrusterBlock's fixture) and the finished block in `Assets/MyThrusterBlock`: name, three models, Ion Thruster 100 thrust and power, nozzle placed.
