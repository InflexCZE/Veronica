---
name: gscreen
title: The G-Screen: categories, subcategories, tiles and their order
source: vanilla G-Screen definitions in Assets/UI/Screens/GScreen and their definition types in the SE2 source (CategoryDefinition, SubcategoryDefinition, BlockGroup, BlockKind, CubeBlockDefinition UIData and BlockKind, ToolTileDefinition); vanilla paint gun (Assets/CharacterTools/PaintGun)
description: How the G-Screen (the block menu) is built from categories, subcategories, tiles and block kinds, how a block joins a place, which names and links it needs to show, how entries are ordered, hand tool tiles, block icons, why a block does not show or replaces a vanilla entry.
---
The G-Screen nests Category > Subcategory > BlockGroup (a tile) > BlockKind (one block in all its sizes).
- Each level points up to its parents: a kind lists its BlockGroups, a group its Subcategories, a subcategory its Categories. So a place is built from the bottom.
- A block joins a kind through BlockKind on its own CubeBlockDefinition (or the PowerableBlockDefinition or other descendant the block uses). A kind's block list is computed from that; it is not authored.
- A kind joins a tile through its own BlockGroups; that also works for vanilla tiles, without an override of them.
- BlockGroups on a kind and Subcategories on a group are lists: one kind can sit in several tiles, one tile under several subcategories, with no copy of the block.
- Kinds in the same group share one tile. A tile shows its group's Name but the icon and description of its first kind.
- A kind that needs a tile, icon and text of its own gets a group that holds only it, pointing at the shared subcategory, as vanilla does; it can stay in the shared group too. A family's sizes stay in one kind.
- Leave a tile's BlockKinds empty.
- Vanilla entries are in Assets/UI/Screens/GScreen; find_content("SubcategoryDefinition") and the like list them.
- Names players see: Name on each level, Tooltip on subcategories and categories, Description on a kind.
- Required for a block to show: BlockKind on the block; the kind's Name, Description and BlockGroups; the group's Name and Subcategories; the subcategory's Categories; a category's Name and Tooltip. Reuse a vanilla category unless the user wants a new top-level one (a CategoryDefinition plus a SubcategoryDefinition whose Categories points at it).

Order:
- Order on a group, subcategory or category, OrderingPriority on a kind within its tile. Sizes within a kind sort themselves.
- On every level 0 means automatic: the entry goes after all entries with a non-zero value, sorted by name key, then Guid. Lower non-zero values come first.
- In a subcategory, tiles holding one block kind come after the other tiles.
- To place an entry before a vanilla one at 0, give it any Order above 0. To place it after, it needs a higher Order than the vanilla one, which means overriding the vanilla entry: a change to vanilla the user should agree to.

Hand tools:
- A hand tool shows through a ToolTileDefinition: ToolPrefab (needs a ToolComponent), Icon, Description, Order, Subcategories (a map of category to subcategory). The vanilla paint gun's (Assets/CharacterTools/PaintGun) is one to copy.

Icons:
- A block's icon is Icon on its CubeBlockDefinition (UIData), a GUIAsset reference. A vanilla icon is reused by pointing at it; no copy.
- Vanilla icons are 200x200 PNG with alpha (RGBA). A new icon needs a real transparent background, or it shows a solid one.

Why a block does not show, or replaces a vanilla entry:
- The prefab's block component still points at the vanilla block definition, or at a partial copy that keeps the vanilla BlockKind: the block joins the vanilla kind. Give it a definition of its own (a copy), set its BlockKind and assign it in the prefab's component.
- A missing required name or link above: no BlockKind, a kind with no BlockGroup, a group in no subcategory, a subcategory in no category.
- The block fails validation, e.g. 'BlockModel is missing any collider' (its model has no collision shape).

The G-Screen itself is visible only in game (Project > Start Game).
