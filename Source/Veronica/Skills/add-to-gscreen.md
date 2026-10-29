---
name: add-to-gscreen
description: Use when a block should appear in a G-Screen place other than its template's family, or in a new tile, subcategory or category.
---
Needs:
- the block's definition: a CubeBlockDefinition or a type derived from it; find_content("CubeBlockDefinition", folder) finds it; for a vanilla block, its override (override-definition)
- the place: which category, subcategory and tile, each existing or new; for new ones a name, and a tooltip for subcategories and categories
- folder: a mod folder, e.g. Assets/GScreen

Plan:
- Create the block kind and point the block at it
- Find or create the tile and add it to the kind
- Find or create the subcategory, for a new tile
- Find or create the category, for a new subcategory
- Set the order, when the user wants a position
- Show the block

The G-Screen nests Category > Subcategory > BlockGroup (a tile) > BlockKind (one block in all its sizes). Each level points up to its parents, so build from the bottom. Vanilla ones are in Assets/UI/Screens/GScreen; find_content("SubcategoryDefinition") and the like list them.

Steps:
1. Block kind:
   create_definition("BlockKindDefinition", <name>, folder)
   write(kind, "Name", <block name>); write(kind, "Description", <text>)
   write(block, "BlockKind", kind)
2. Tile: an existing BlockGroupDefinition, or a new one:
   create_definition("BlockGroupDefinition", <name>, folder); write(group, "Name", <tile name>)
   Then element_add(kind, "BlockGroups") and write(kind, "BlockGroups[i]", group).
   A kind joins a tile through its own BlockGroups; that also works for vanilla tiles without an override. A tile's BlockKinds only sets a manual order; leave it empty.
3. For a new tile, its subcategory: an existing SubcategoryDefinition, or a new one:
   create_definition("SubcategoryDefinition", <name>, folder); write Name and Tooltip
   element_add(group, "Subcategories"); write(group, "Subcategories[i]", subcategory)
4. For a new subcategory, its category: an existing CategoryDefinition, or a new one:
   create_definition("CategoryDefinition", <name>, folder); write Name and Tooltip
   element_add(subcategory, "Categories"); write(subcategory, "Categories[i]", category)
5. Order, only when the user wants a position: write(x, "Order", n) on a group, subcategory or category, or "OrderingPriority" on a kind. Order sorts ascending, but 0 means last: entries with Order 0 come after all ordered ones, by name. In a subcategory, tiles holding one block kind come after the other tiles. To place an entry before a vanilla one at 0, give it any Order above 0; to place it after, it needs a higher Order than the vanilla one, which means overriding the vanilla entry, so ask the user first. A kind's OrderingPriority works the same within a tile; sizes within a kind sort themselves.

Show:
show(the block's prefab). The G-Screen itself is visible only in game (Project > Start Game).
