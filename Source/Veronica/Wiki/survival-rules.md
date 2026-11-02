---
name: survival-rules
title: Survival rules as data: block unlocks, contract rewards, grid cleanup by PCU
source: SE2 source ProgressionInitialUnlockConfiguration, ContractDefinition, ProgressionCheckpointDefinition, TrashRemovalConfiguration; vanilla Assets/System/Configurations, Assets/Colonization/VS2-*/Contracts
description: Why a mod block is locked in survival and how to unlock it from the start, where contract block rewards live, and why grids vanish above a PCU limit and which configuration raises it.
---
Block unlocks:
- A block in no unlock list is locked in survival progression: players cannot build it until it is unlocked. Creative is not affected.
- ProgressionInitialUnlockConfiguration (Assets/System/Configurations) holds what is unlocked from the start: Blocks (block prefabs), BlockKinds, Categories and Subcategories. All four merge into one set.
- A category or subcategory unlocks the block kinds of its G-Screen tiles (BlockGroups); a kind unlocks its blocks. Unlocking a mod's own subcategory unlocks all its blocks at once.
- A mod unlocks its blocks with an override of the vanilla ProgressionInitialUnlockConfiguration (find_content("ProgressionInitialUnlockConfiguration")) that adds one element per entry to a list and keeps vanilla's entries; an empty element breaks loading.
- Blocks unlocked later are contract rewards: ContractDefinition BlockRewards; vanilla contracts are in Assets/Colonization/VS2-*/Contracts.
- A block not unlocked from the start gets its unlock rules from its component items (the block definition's Items) and from progression checkpoints (ProgressionCheckpointDefinition types, e.g. ProductionBlockProgressionCheckpointDefinition) whose UnlockingBlocks list it.

Grid cleanup:
- Trash removal deletes grids above a PCU limit, smallest PCU first.
- TrashRemovalConfiguration (Assets/System/Configurations): PlayerGridPCU for player grids, NonPlayerGridPCU for NPC grids, each with TargetCleanupLimit and ExecuteCleanupLimit.
- A mod that raises the PCU players may build overrides these too, or grids above the vanilla limit vanish.

