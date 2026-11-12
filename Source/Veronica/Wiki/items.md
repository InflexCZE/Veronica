---
name: items
title: Inventory items: item definitions, how they fit inventories, a new item, recipe timing and rules, the vanilla production chain
source: SE2 source ItemDefinition, ItemStackFlags, ItemTypes, ConsumableDefinition, DatapadDefinition, QuestItemDefinition, QuestItemValidator, BlockSchematicValidator, ResourceContainerItemDefinition, InventoryDefinition, InventoryComponent.IsItemCompatibleWithInventory, InventoryDataExtensions (stacking, whole amounts), ItemDefinitionExtensions, ItemToolbarTilePostProcessor, InventoryBlockDestructionExplosionComponent, FloatingObjectPrefabsConfiguration, ItemRecipesDefinition, ItemRecipeDefinition, ItemRecipePostprocessor, ItemRecipeValidator (mass conservation), ItemConverterRecipeValidator, ItemConverterComponent (time, efficiency, child converters), BackpackItemConverterDefinition, BackpackItemConverterComponent, IntegrityToolComponent, DrillDefinition (AutoConversions); vanilla Assets/Components, Assets/Items, Assets/CharacterTools, Assets/Characters/System inventories, production block inventories and ItemConverterDefinitions, Assets/System/ItemConverters, Assets/System/Configurations/FloatingObjectPrefabsConfiguration.def, Assets/Templates/FloatingObjects; live Editor run of a new item from the Floating Object template
description: What an inventory item is (ItemDefinition fields: Type, MassPerUnit, Flags for stacking and whole amounts, Icon, ExplosiveRisk), which inventories accept an item by its Type, how mass and stacks fill an inventory, the steps of a new item (definition, icon, dropped look from the Floating Object template, the prefab configuration entry, a recipe, a block that crafts it), how long a recipe takes with SpeedMultiplier, the recipe rules validation checks (mass, one recipe per item even outside any block, whitelisted types but not whitelisted items), where to put a new recipe, which vanilla block makes what, how to find what makes an item, backpack crafting by the hand welder and the hand drill's stone-to-ore conversion.
---
Item definitions:
- ItemDefinition is one kind of item. Consumables, datapads and quest items have their own subtypes (ConsumableDefinition, DatapadDefinition, QuestItemDefinition): copy a vanilla one of the same subtype for those.
- Vanilla items: Assets/Components/<Simple, Refined, Complex, Hi-Tech>/<item> (components), Assets/Items/Ores, Assets/Items/Ammo, Assets/Items/Consumables, Assets/CharacterTools/<tool>/Items (hand tools and weapons). Each item folder also holds the recipe that makes it, if any, and the item's dropped look. Many vanilla items are copies of the Floating Object template's example item, so their ItemDefinition is a partialdef, often named ..._FloatingObjectItem; find_content with the item name finds either form.

Fields:
- DisplayName, Description: localization keys; plain text shows as typed, with a warning (the localization page).
- Icon: a png in the mod's assets, the item's picture in game; a recipe shows its first output's icon unless it sets IconOverride. Name the file with a suffix such as <Name>_Icon.png: create_prefab refuses to make a prefab whose files would share a name with a file already in the folder.
- MassPerUnit: kg per unit. Inventories are limited by mass only; there is no volume. It also sets the item's share of a block's build cost (the block-stats page). Vanilla: ores 1, components 5 to 700 (steel plate 35, motor 25, compressor 90).
- Type: a flag set; Ore, Material, Component, Item, Consumable, Datapad, Quest, BlockSchematic. It decides which inventories accept the item (below). Vanilla uses Ore, Component (also for refined metals and bars; no vanilla item is Material), Item (tools, weapons, ammo), Consumable.
- Flags: Stackable (units of the same item share one stack) and Divisible (fractional amounts, as ores have); PresetNormal is both. Without Stackable every unit is its own stack; without either flag amounts are whole numbers. Vanilla components are Stackable, ores PresetNormal, tools 0, power cells 0 (each carries its own charge).
- DefaultMetaData: data every new stack starts with, such as ResourceContainerItemMetaData.FillRatio on vanilla power cells; their stored resource and capacity are a separate ResourceContainerItemDefinition.
- ExplosiveRisk: per unit; a block with InventoryBlockDestructionExplosionComponent explodes by the sum of it when destroyed. 0 for anything that is not ammo.
- ShouldAutoEquip (default true): only for items a toolbar tile shows (tools, consumables); adds the tile when the player gets the item. ShowDiscoveryFanfare, IsRareLoot: first-pickup fanfare, loot rarity.
- QuestItemDefinition and block schematics must have exactly their one Type flag and not be Stackable, or validation fails.

Which inventories accept an item:
- An inventory accepts an item when the item's Type shares a flag with its WhitelistedTypes, and its WhitelistedItems is empty or lists the item. An item with no Type flag fits every inventory.
- The vanilla character inventory (CharacterInventory) takes Ore, Material, Component and Item. Consumables go to a separate consumables inventory (5 stacks), datapads, quest items and schematics to their own.
- Vanilla production blocks: Smelter and Refinery in Ore, out Component; Assembler in any type, out Component; Fabricator in and out Component; Gearforge in Ore, Component, Item, out Item, Consumable. The vanilla Assembler's inventories also list WhitelistedItems: its input only the vanilla components its recipes use, its output only its own products.
- Fill: MaxMass in kg is the capacity; units fit while their mass fits. MaxStacks, when set, caps the number of stacks; non-stackable items use one stack per unit.

A new item, in order:
1. create_definition ItemDefinition next to the mod's other content; write DisplayName, Type, MassPerUnit, Flags and Icon (the png's path). Take Type and Flags from a vanilla item of the same kind, and a mass in line with it.
2. Dropped look: create_prefab from the Floating Objects/Floating Object template. It makes the prefab chain (binding, client, server and their compositions) and a copy of the template's model component, <name>_FloatingObjectModelComponent, which both compositions already use and which carries the pickup dummy. Write the item's model into that copy's Model.
3. FloatingObjectPrefabsConfiguration: an override of vanilla's (Assets/System/Configurations; the configurations page) with an ItemPrefabs entry, key the ItemDefinition, value the prefab's _Server definition. Without it a dropped unit shows the generic fallback cube; datapads and quest items have fallbacks of their own by Type.
4. A recipe that makes it, in a recipe set a block offers (below). Without one the item exists but nothing crafts it.
5. Uses: a block's build cost (block-stats page), reactor fuel (reactors page), ammo (configurations page).

Recipes (the production page has the parts and the tabs):
- A recipe repeats while its order runs; one run takes TimeToConvert / the converter's SpeedMultiplier. SpeedMultiplier 2 crafts twice as fast; vanilla blocks have 1. Outputs are multiplied by EfficiencyMultiplier. Vanilla times: Smelter 0.2 to 0.7 s, Refinery 1 to 1.2 s, Assembler 1.5 to 4 s, Fabricator about 4.2 s, Gearforge up to 10 s.
- Inputs may not be empty. Amounts may be fractions for divisible items (ores).
- Mass: when a recipe a block offers has outputs weighing more than its inputs (Amount * MassPerUnit), validation notes it, as a note for now; vanilla recipes keep outputs at or under the inputs' mass (35 iron ore -> 1 steel plate of 35 kg). Choose the new item's MassPerUnit and the input amounts together.
- One recipe per item is checked across every ItemRecipeDefinition loaded, also those no block offers: vanilla has unused recipes for the power cell and the power module, so a new single-output recipe for those fails as an alternative.
- Validation checks each recipe's inputs and outputs against the block's input and output WhitelistedTypes only. A product missing from the output inventory's WhitelistedItems passes validation but never fits, so the order never completes: add the new item to a copied vanilla whitelist (element_add on WhitelistedItems), or use the template's inventories, which accept everything.
- Several sets under one tab are merged into one list. A block can keep a vanilla set as it is and add a second set of the mod's own under the same tab; nothing is copied.
- When an order's ingredients are missing, the block asks other production blocks on the same conveyor network that can make them to craft them, never itself. A recipe whose inputs another block crafts works across the chain on its own.

Where to put a new recipe:
- An item of the same group added to what a vanilla block already crafts: add the recipe to that block's vanilla recipe set, through an override of the set.
- A new kind of item for a new block: a new ItemRecipesDefinition of the mod's own, listed under the tab next to the vanilla sets the block offers.

Vanilla production chain:
- Smelter: ore -> simple components (steel plate, tube, motor, wire, glass, electronic parts, construction component, metal grid, lead bar, gold thread, chrome coating, magnesium powder, turbine blades).
- Refinery: ore -> refined components (stainless steel, titanium sheet, silver rod, superconductor, enriched uranium, platinum electrode, permanent magnet, magnesium bar, radiation shielding).
- Assembler: components -> complex components (compressor, motherboard, hard drive, ion chamber, combustion chamber, pressure pipe, heavy-duty plate, radio-comm module, reactor core, chemical synthesizer).
- Fabricator: complex and refined -> hi-tech (mainframe, ion accelerator, ultra-resistant plate, advanced compressor) and gravity core, titanium blades, laser.
- Gearforge: ore and components -> tools (welders, grinders, drills Mk1 to Mk4; each Mk takes the one below), weapons, ammo, consumables, one set per tab.
- Smelter, Refinery, Assembler and Fabricator list their sets under the Components tab. The Ingots, Blocks and Placeholder tabs exist but no vanilla block uses them.
- The Components folder group does not say which block makes an item (gravity core, laser and titanium blades are in Complex but made by the Fabricator); the recipe set does.

Finding what makes an item: find_references on the ItemDefinition lists the recipes that make or use it; on a recipe, the recipe sets holding it; on a set, the ItemConverterDefinitions offering it, each one a production block's.

Crafting outside blocks:
- Backpack crafting: a hand welder carries a BackpackItemConverterDefinition with a list of recipes. While welding, it crafts a component the block needs from what the player carries, picking the recipe whose first output is that component. Vanilla hand welders offer the Smelter's steel plate, tube, glass, electronic parts, motor and construction component recipes. Here SpeedMultiplier multiplies the time (vanilla 0.15, much faster), the opposite of a block's.
- Hand drill: its DrillToolDefinition's AutoConversions, a list of recipes, turn mined items into others at once. Vanilla turns stone into 0.8 iron, 0.1 silicon and 0.1 nickel ore. Each conversion has one input, no two share an input, and every item in it must be Divisible. Ship drills' DrillDefinition has the same list, empty in vanilla. These are ItemRecipeDefinitions too, so a single-output one counts for the one-recipe-per-item rule.
