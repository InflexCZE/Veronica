---
name: gscreen
title: The G-Screen: categories, subcategories, tiles and their order
source: moved from the add-to-gscreen skill
description: How the G-Screen (the block menu) is built from categories, subcategories, tiles and block kinds, how a block joins a place, and how entries are ordered.
---
The G-Screen nests Category > Subcategory > BlockGroup (a tile) > BlockKind (one block in all its sizes).
- Each level points up to its parents: a kind lists its BlockGroups, a group its Subcategories, a subcategory its Categories. So a place is built from the bottom.
- A kind joins a tile through its own BlockGroups; that also works for vanilla tiles, without an override of them.
- A tile's BlockKinds only sets a manual order; leave it empty.
- Vanilla entries are in Assets/UI/Screens/GScreen; find_content("SubcategoryDefinition") and the like list them.
- Names players see: Name on each level, Tooltip on subcategories and categories, Description on a kind.

Order:
- Order on a group, subcategory or category, OrderingPriority on a kind within its tile. Sizes within a kind sort themselves.
- Order sorts ascending, but 0 means last: entries with Order 0 come after all ordered ones, by name.
- In a subcategory, tiles holding one block kind come after the other tiles.
- To place an entry before a vanilla one at 0, give it any Order above 0. To place it after, it needs a higher Order than the vanilla one, which means overriding the vanilla entry: a change to vanilla the user should agree to.

The G-Screen itself is visible only in game (Project > Start Game).
