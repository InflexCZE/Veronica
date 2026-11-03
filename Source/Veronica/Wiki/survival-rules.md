---
name: survival-rules
title: Survival rules as data: block unlocks, contract rewards, grid cleanup by PCU
source: SE2 source ProgressionSessionComponent (IsBlockUnlocked), ProgressionInitialUnlockConfiguration, ProgressionInitialUnlockPostProcessor, BlockKindDefinitionPostProcessor, BlockProgressionPostProcessor, ItemProgressionPostProcessor, BackpackCraftingPostProcessor, ProductionBlockProgressionCheckpointDefinition, BlockRewardGroupDefinition, ContractDefinition, BlockSchematicServerSessionComponent, UnlockBlocksActionComponent, BlockEquipHelper, GridProjectionsSessionComponent, PartialDefinitionGeneratorProvider, set deltas of partial definitions (DeltaEncodedCollectionsHelper), TrashRemovalConfiguration, GridPCUTrashRemovalSessionComponent; vanilla Assets/System/Configurations/ProgressionInitialUnlockConfiguration.def and TrashRemovalConfiguration, Assets/Colonization/VS2-*/Contracts
description: Why a mod block is locked in survival and how to unlock it from the start or later, where contract block rewards live, and when grids are deleted by total PCU and which configuration raises the limits.
---
Block unlocks:
- In survival with progression on (the world default), a player can pick a block from the G-Screen or toolbar only when it is unlocked. Creative, or a world with progression off, unlocks everything; testing an unlock needs a survival world with progression on. A block marked CreativeOnly can never be picked or projected in survival.
- ProgressionInitialUnlockConfiguration (Assets/System/Configurations) holds what is unlocked from the start: Blocks (block server prefabs, named <Block>_Server; not the _Client prefab and not the binding prefab without a suffix), BlockKinds, Categories and Subcategories. All four merge into one set.
- A category unlocks its subcategories, a subcategory the block kinds of its G-Screen tiles (BlockGroups), a kind every block whose BlockKind is that kind, a mod's blocks included. A mod block in an already unlocked kind is unlocked with no change; vanilla unlocks the gatling cannon by its prefab, not its kind. A block with no BlockKind, or whose kind is on no G-Screen tile, is unlocked only through Blocks.
- A mod unlocks its block with an override of the vanilla ProgressionInitialUnlockConfiguration (find_content("ProgressionInitialUnlockConfiguration")): add one element per entry, e.g. the block's _Server prefab to Blocks, or the mod's own kind or subcategory. Vanilla's entries stay as long as the override does not delete them. Do not leave an empty element. The Editor allows one override of it per mod, so a second block goes into the same override.
- Unlocking a vanilla kind, subcategory or category also unlocks the vanilla blocks in it.
- Unlocks are shared by a faction. Blocks unlocked later come from block reward groups (BlockRewardGroupDefinition, its Blocks list of _Server prefabs), referenced from a contract's BlockRewards (vanilla contracts in Assets/Colonization/VS2-*/Contracts), a block schematic's reward or a mission's unlock-blocks action.
- A block's component items and progression checkpoints do not lock it: they only add recommended progression and tech level hints to its G-Screen tile.
- Items, ammo included, are never locked by progression; unlocking a block needs nothing for its items.

Grid cleanup:
- Trash removal starts when the total PCU of all player grids (separately, of all NPC grids) reaches ExecuteCleanupLimit. It then deletes whole grids, smallest PCU first, until the total is at or below TargetCleanupLimit. Pinned grids are skipped. One large grid is not deleted for its own size.
- TrashRemovalConfiguration (Assets/System/Configurations): PlayerGridPCU for player grids, NonPlayerGridPCU for NPC grids, each with TargetCleanupLimit and ExecuteCleanupLimit.
- A mod that lets players build more overrides these too, or grids vanish once the world total passes the vanilla limit.
