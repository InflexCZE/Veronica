---
name: production
title: Production blocks: recipes, the queue, power while crafting, inventories, where players use them
source: SE2 source ItemConverterComponent, ItemConverterDefinition, ItemConverterRecipeCategoryDefinition, ItemRecipesDefinition, ItemRecipeDefinition, AutoItemConverterComponent, AutoItemConverterDefinition, InventoryStockpileDefinition, InventoryDefinition, recipe validation, PowerableBlockComponent; vanilla production blocks (Fabricator, Gearforge, Assembler, Smelter, Refinery), UseActionBindings.def, GridConveyorSystem.def, the Production (Manual) template
description: What a crafting block (fabricator, gearforge, assembler) does: which recipes it offers and how they are grouped into tabs, what a recipe holds and the validation rules it must pass (inventory whitelists, one recipe per item, circular crafting), the order queue, crafting without orders, the power it draws idle and while crafting, its input and output inventories, how it gets ingredients, the production screen spot, what the template brings.
---
Parts:
- ItemConverterComponent, on the server prefab only, with its ItemConverterDefinition:
  - RecipeDefinitions: maps a recipe tab of the production screen (an ItemConverterRecipeCategoryDefinition, Assets/System/ItemConverters: Components, Tools, Weapons, Ammo, ...) to a list of ItemRecipesDefinitions, the recipe sets offered under that tab. Empty: the block offers nothing to craft. Several tabs and sets can be combined; a recipe set belongs to no block, so another block's set is referenced as it is.
  - MaxQueueSize: how many orders players can queue. 0: nothing can be queued, so the block never crafts.
  - ConsumedResource: power in kW added to the block's own draw while it crafts or has an order waiting.
  - SpeedMultiplier: how fast recipes run. EfficiencyMultiplier: how much each recipe yields.
  - RemoveImpossibleRecipes: drops an order that cannot be completed from the queue.
- Two InventoryComponents on the server: InventoryInput holds the ingredients, InventoryOutput the products. MaxMass is the capacity in kg. The template's accept every item type.
  - Every recipe's input item types must be allowed by the input inventory's WhitelistedTypes, and its outputs by the output inventory's; otherwise validation fails and the mod's definitions do not load.
- InventoryStockpileComponent on the output inventory: pushes products to the conveyor network once the output is filled above FillRatioPush (a share, 0 to 1); it pushes every item not listed in its Items. Other blocks then pull from it. PreventStockpilingFromRemovingItems on an InventoryDefinition stops other blocks taking items out (for input inventories), PreventStockpilingFromAddingItems stops them putting items in (for outputs).
- ConveyorComponent: an order pulls its ingredients from the conveyor network, which connects only at UseConveyor dummies (the inventory-and-conveyors page). With no ports the block is cut off from the ship's cargo.
- PowerableBlockComponent as the block component: its ConsumedResource.Amount is drawn all the time the block is on, in kW. Total draw = that + the converter's ConsumedResource while crafting.

Recipes: an ItemRecipeDefinition has Inputs, Outputs and TimeToConvert. A block offers a mod's recipe only when its ItemConverterComponent points at an ItemConverterDefinition whose RecipeDefinitions list a set holding it.

One recipe per item: a recipe with a single Outputs entry may not make an item another single-output recipe already makes (validation error 'Alternatives are forbidden!'). To change how an item is made, override its existing recipe. Recipes with two or more Outputs entries are exempt; when several recipes make the same item the converter uses only the first.

Circular crafting: validation follows the inputs of single-output recipes down to raw items; an item that comes back up in its own chain is an error ('Circular crafting detected'), whatever the amounts. A reverse recipe (recycler, disassembler) passes when it has two or more Outputs entries, e.g. a small second item or the same item split into two entries.

Where players use it: the production screen is the Production tab of the grid's terminal, listing the grid's production blocks; players reach it from any terminal access to the grid. A UseProduction dummy is a spot on the block that opens the terminal straight at that tab, a shortcut, not needed to craft. Vanilla production blocks have one or more, besides conveyor ports.

Vanilla production blocks: Assets/Blocks/<name>/<size> (Fabricator, Gearforge, Assembler, ...). They draw nothing idle (block Amount 0) and their power only while crafting. Each offers its own recipe sets.

The Production (Manual) template: a block made from it gets its own ItemConverterDefinition (no recipes, queue 0, no extra power while crafting), its own input and output InventoryDefinitions and its own PowerableBlockDefinition (a fixed draw while on, no BlockKind, so no block menu place). It has no dummies: no conveyor ports and no production screen spot.

Crafting without orders: AutoItemConverterComponent is an ItemConverterComponent that queues recipes itself from what is in its input inventory. Its AutoItemConverterDefinition adds InventoryUpdateFrequency (must not be 0) and FillRatio; MaxQueueSize and RemoveImpossibleRecipes are on the base definition as above. It needs an inventory tagged InventoryStockpile on the block. No vanilla block uses it. No production component works without a queue.

Looks only, not needed to craft: the vanilla blocks' subparts, LCD panels and their dummies, the material swapper and effects.
