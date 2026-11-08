---
name: block-survival
description: Use when a block should be buildable in a survival game with progression on, where a new block is locked unless its kind is unlocked from the start; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files
- folder: a mod folder for the unlock override, when the mod has none, e.g. the block's folder

Plan:
- Find the unlock configuration: the mod's override of ProgressionInitialUnlockConfiguration, or vanilla's when the mod has none
- Check whether the block is already unlocked: the block, its BlockKind, the kind's subcategories and their categories
- When it is not, add the block's _Server prefab to Blocks in the mod's override, creating the override when there is none; otherwise say which entry unlocks it and change nothing
- Tell the user how to test: a survival world with progression on

Steps:
1. find_references("ProgressionInitialUnlockConfiguration") lists the mod's override of it ("override of it"). A mod has at most one, and other blocks' unlocks may already be in it. Without one, check vanilla's.
2. The configuration's Blocks, BlockKinds, Subcategories and Categories together are what is unlocked from the start. The block is unlocked when any of these holds:
   - contains(config, "Blocks", <block>) lists its _Server prefab. Only the _Server prefab counts: the binding or the _Client prefab in Blocks unlocks nothing.
   - contains(config, "BlockKinds", <kind>) lists its kind: the BlockKind of the block's definition, the one holding UIData.
   - contains(config, "Subcategories", <subcategory>) lists a subcategory of the kind's tiles: read(kind, "BlockGroups"), then read(each tile, "Subcategories").
   - contains(config, "Categories", <category>) lists a category of those subcategories: read(subcategory, "Categories").
   A block with BlockKind none is unlocked only through Blocks. contains answers each check; do not read the whole configuration.
3. When none holds: element_add(override, "Blocks", value=<block>_Server). Without an override, create_partial(vanilla configuration, "override", folder) first. When create_partial says the mod already has an override, add to that one: it is the mod's override, which step 1 missed. Unlock the block by its _Server prefab, never by adding its kind, subcategory or category: those unlock every vanilla block in them too.
   When one holds, change nothing and tell the user which entry unlocks the block.
4. Tell the user: creative, and a world with progression off, have every block unlocked, so the unlock shows only in a survival world with progression on.
