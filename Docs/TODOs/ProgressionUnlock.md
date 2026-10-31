# Survival unlock of a new block

**Exists:** `create-block` makes a placeable block and gives it a G-Screen place through its BlockKind.

**Missing:** vanilla blocks are also listed by their server prefab in the progression data: `System/Configurations/ProgressionInitialUnlockConfiguration.def` (unlocked from the start) and colonization reward groups (`System/Colonization/Rewards/BlockGroupRewards`, unlocked later). No skill or wiki page says whether a modded block outside these lists can be built in survival, or how a mod adds its block to them. Start in the SE2 source where the progression reads these lists, then decide with the owner whether it belongs in `create-block` or a skill of its own.
