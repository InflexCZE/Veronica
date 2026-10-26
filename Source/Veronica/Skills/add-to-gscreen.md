---
name: add-to-gscreen
description: Use when a block should appear in a G-Screen place other than its template's family, or in a new tile, subcategory or category.
---
Needs:
- the block's block definition (its type ends in BlockDefinition); for a vanilla block, its override (override-definition)
- the place: which category, subcategory and tile, each existing or new; for new ones a name, and a tooltip for subcategories and categories
- folder: a mod folder, e.g. Assets/GScreen

The G-Screen nests Category > Subcategory > BlockGroup (a tile) > BlockKind (one block in all its sizes). Each level points up to its parents, so build from the bottom. Vanilla ones are in Assets/UI/Screens/GScreen; find_content("SubcategoryDefinition") and the like list them.

Steps:
1. Block kind:
   create_definition("BlockKindDefinition", <name>, folder)
   write(kind, "Name", <block name>); write(kind, "Description", <text>)
   write(block, "BlockKind", kind)
2. Tile: an existing BlockGroupDefinition, or a new one:
   create_definition("BlockGroupDefinition", <name>, folder); write(group, "Name", <tile name>)
   Then element_add(kind, "BlockGroups") and write(kind, "BlockGroups[i]", group).
3. For a new tile, its subcategory: an existing SubcategoryDefinition, or a new one:
   create_definition("SubcategoryDefinition", <name>, folder); write Name and Tooltip
   element_add(group, "Subcategories"); write(group, "Subcategories[i]", subcategory)
4. For a new subcategory, its category: an existing CategoryDefinition, or a new one:
   create_definition("CategoryDefinition", <name>, folder); write Name and Tooltip
   element_add(subcategory, "Categories"); write(subcategory, "Categories[i]", category)
5. Order, only when the user wants a position: write(x, "Order", n) on a group, subcategory or category, or "OrderingPriority" on a kind. Lower numbers come first; 0 sorts by name. Sizes within a kind sort themselves.

Show:
show(the block's prefab). The G-Screen itself is visible only in game (Project > Start Game).
