---
name: block-production
description: Use when a block should craft items from a queue like a fabricator or assembler, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Production (Manual) template
- what it crafts: the recipe tabs and recipe sets of a vanilla production block, or of the user's own
- how many orders it queues and the power it draws: the user's own, or as a vanilla production block (Assets/Blocks, e.g. Fabricator)
- the spots players use: conveyor ports (how many, on which faces), and a spot that opens its production screen at the block when the user wants one; from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: its ItemConverterDefinition, its PowerableBlockDefinition and its model definition's dummies
- Get from the user what this needs: what it crafts, the queue size, the power, how many conveyor ports and where, whether they want a production screen spot, and for a new model that they will place them
- Set the converter: the recipes, the queue size and the power while crafting
- Set the block's power when idle
- Set up the spots: the UseConveyor ports and a UseProduction dummy when wanted, placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components; the ItemConverterComponent names its ItemConverterDefinition, which a block made from the template owns. Read it, the PowerableBlockDefinition and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, what it crafts, how many orders it queues, the power, how many conveyor ports it has and on which faces, and whether they want a spot that opens its production screen at the block; offer a vanilla production block as a suggestion. The template crafts nothing (RecipeDefinitions empty), queues nothing (MaxQueueSize 0) and draws a fixed power while on: its values were not chosen for this block. A vanilla block's spots fit its own model, not the user's. For a new model, say they will place the spots in the Editor.
3. Recipes, on the ItemConverterDefinition: RecipeDefinitions maps a recipe tab (an ItemConverterRecipeCategoryDefinition, Assets/System/ItemConverters) to a list of ItemRecipesDefinitions, the recipe sets shown under that tab. To craft what a vanilla block crafts, read its ItemConverterDefinition's RecipeDefinitions and add the same: element_add(def, "RecipeDefinitions", key: <tab>), element_add(def, "RecipeDefinitions[<tab>]"), write(def, "RecipeDefinitions[<tab>][0]", <recipe set>); repeat per tab and set.
   MaxQueueSize: how many orders players can queue; 0 queues nothing, so the block never crafts.
4. Power: the block draws its PowerableBlockDefinition's ConsumedResource.Amount (kW) all the time it is on, plus the ItemConverterDefinition's ConsumedResource (kW) while it crafts. Vanilla production blocks draw nothing idle and their power only while crafting. Write the values that do not fit what the user wants.
5. Spots: a UseProduction dummy (Assets/System/DummyTypes/UseProduction.def) opens the grid's production screen when a player uses that spot of the block; without one players still reach it through the terminal, so it is the user's choice. Crafting pulls its ingredients from the conveyor network, which connects only at UseConveyor dummies (Assets/System/DummyTypes/UseConveyor.def). A block made from the template has none. element_add(BlockModelComponentDefinition, "Dummies") once per spot, then write its Name and Type.
   Positions: for a model another block uses, copy the Transform and Scale of that block's dummies of the same Type; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.

Show:
show(the block). Ask the user to check the conveyor ports and the production screen spot, when there is one, in the Render View (Preview Settings, Dummies).
