---
name: block-production
description: Use when a block should craft items from a queue like a fabricator or assembler, or should craft more or faster, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Production (Manual) template, or a block that already crafts
- what it crafts: the recipe tabs and recipe sets of a vanilla production block, or of the user's own
- recipes of the mod's own, when the user wants them: what each makes, from what and how many, and how long it takes; an item the mod adds comes from add-item first
- how fast it crafts, how many orders it queues and the power it draws: the user's own, or as a vanilla production block (Assets/Blocks, e.g. Fabricator)
- the spots players use: conveyor ports (how many, on which faces), and a spot that opens its production screen at the block when the user wants one; from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: its ItemConverterDefinition, its PowerableBlockDefinition, its inventories' InventoryDefinitions and its model definition's dummies
- Get from the user what this needs: what it crafts, recipes of its own, how fast, the queue size, the power, how many conveyor ports and where, whether they want a production screen spot, and for a new model that they will place them
- When the block should craft an item that does not exist yet: activate add-item for it
- Set the converter: the recipe sets, the speed, the queue size and the power while crafting
- Recipes of the mod's own, when the user wants them: each recipe, the set that holds it, and inventories that accept its items
- Set the block's power when idle
- Set up the spots: the UseConveyor ports and a UseProduction dummy when wanted, placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components; the ItemConverterComponent names its ItemConverterDefinition, which a block made from the template owns. Read it, the PowerableBlockDefinition, the InventoryDefinition of each InventoryComponent and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, what it crafts, any recipes of its own (what each makes, from what, how long in a block of normal speed), how fast it crafts, how many orders it queues, the power, how many conveyor ports it has and on which faces, and whether they want a spot that opens its production screen at the block; offer a vanilla production block as a suggestion. A block made from the template crafts nothing yet (RecipeDefinitions empty); its queue, speed and power are the vanilla Assembler's (nothing idle, a draw while crafting), a starting point, not the user's choice: read the vanilla block the user names for its values. A vanilla block's spots fit its own model, not the user's. For a new model, say they will place the spots in the Editor.
3. An item the block should craft that does not exist yet: activate add-item for it. It returns here; the recipe that crafts it is step 5.
4. Recipes, on the ItemConverterDefinition: RecipeDefinitions maps a recipe tab (an ItemConverterRecipeCategoryDefinition, Assets/System/ItemConverters) to a list of ItemRecipesDefinitions, the recipe sets shown under that tab; sets under one tab are merged. To craft what a vanilla block crafts, read its ItemConverterDefinition's RecipeDefinitions and add the same: element_add(def, "RecipeDefinitions", key: <tab>), element_add(def, "RecipeDefinitions[<tab>]"), write(def, "RecipeDefinitions[<tab>][0]", <recipe set>); repeat per tab and set.
   SpeedMultiplier: a recipe takes its TimeToConvert divided by it; 2 crafts twice as fast. Vanilla blocks have 1.
   MaxQueueSize: how many orders players can queue; 0 queues nothing, so the block never crafts.
5. Recipes of the mod's own, one per item it makes:
   create_definition("ItemRecipeDefinition", <name>, folder). Per ingredient: element_add(recipe, "Inputs") (no key, the engine makes one), then write(recipe, "Inputs[<n>].Item", <item>) and its Amount; the same on Outputs for what it makes. TimeToConvert is a TimeSpan written as text, the time at SpeedMultiplier 1: "00:00:03" for 3 seconds, "00:00:01.5" for one and a half; a bare number is read as days. Each item has one recipe in the whole game: a second recipe for an item vanilla already makes fails validation.
   The set that holds it depends on which block the user asked to craft it:
   - the block this skill is building or changing in the mod: create_definition("ItemRecipesDefinition", <name>, folder), element_add(set, "Recipes"), write(set, "Recipes[<n>]", <recipe>) per recipe; list the set under the tab next to the vanilla sets: element_add(def, "RecipeDefinitions[<tab>]"), write(def, "RecipeDefinitions[<tab>][<n>]", <set>).
   - only when the user asked that a vanilla block crafts it too (an item of the same group as what that block crafts): this changes the vanilla block for every player of the mod; create_partial(<that vanilla set>, "override", folder) and add the recipe to its Recipes the same way.
   Inventories: an order completes only when the block's input inventory accepts every input and its output inventory every output: the item's Type must be in WhitelistedTypes and, when WhitelistedItems is not empty, the item must be listed there. Validation checks only the types, so an item missing from WhitelistedItems passes validation and the order never finishes. The template's inventories accept every item. For a whitelist that lacks an item: element_add(InventoryDefinition, "WhitelistedItems", <item>) on a definition the block owns; a vanilla one is copied first (create_partial(<it>, "copy", folder), then written to the inventory component).
6. Power: the block draws its PowerableBlockDefinition's ConsumedResource.Amount (kW) all the time it is on, plus the ItemConverterDefinition's ConsumedResource (kW) while it crafts. Vanilla production blocks draw nothing idle and their power only while crafting. Write the values that do not fit what the user wants.
7. Spots: a UseProduction dummy (Assets/System/DummyTypes/UseProduction.def) opens the grid's production screen when a player uses that spot of the block; without one players still reach it through the terminal, so it is the user's choice. Crafting pulls its ingredients from the conveyor network, which connects only at UseConveyor dummies (Assets/System/DummyTypes/UseConveyor.def). A block made from the template has none. element_add(BlockModelComponentDefinition, "Dummies") once per spot, then write its Name and Type.
   Positions: for a model another block uses, copy the Transform and Scale of that block's dummies of the same Type; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.

Show:
show(the block). Ask the user to check the conveyor ports and the production screen spot, when there is one, in the Render View (Preview Settings, Dummies).
