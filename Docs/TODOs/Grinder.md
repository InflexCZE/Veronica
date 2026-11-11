# Grinder block

**Exists:** `block-welder` and the MyWelder eval task make a ship welder from the Welder template. The skill notes that a negative IntegrityChange grinds instead of welding. The Grinder template holds only the conveyor, inventory and permission components. No skill, wiki page or eval task covers a grinder.

**Missing:** a block that grinds blocks in front of it back into their components and sends them to the conveyors. Vanilla Grinder350 (its ServerComposition) adds to its base: InventoryComponent, DummyInteractedEntityProviderAdapterComponent, IntegrityToolComponent, ParentOwnerProviderComponent (no definition), SphereShapeEntityDetectorComponent, BoxShapeEntityDetectorComponent, DrillDamageComponent, AreaIntegrityToolEntityDetectorComponent and InventoryStockpileComponent, each but one with its own definition next to the block, plus its own GrindersPowerableBlockDefinition. Looks: a spinning disc (ConditionalSubPartsDefinition, drill animator), effects, LOD materials. A skill and an eval task (a new grinder like Grinder350) would need the components added in a working order, the ToolDetection dummy and conveyor ports, and the output push to the conveyors.

**Where to start:** vanilla `Assets/Blocks/Grinder/350`, `block-welder` as the closest skill, the Grinder row moved here from [SkippedBlockKinds.md](SkippedBlockKinds.md).

**Owner decision:** deferred out of the items and recipes round. Open questions:
- Start from the Grinder template (about eight components to add) or from the Welder template (five of the tool components already there)?
- Its own `block-grinder` skill, or a grinding variant inside `block-welder`?
- What the BoxShapeEntityDetector and DrillDamageComponent do on a grinder (damage to characters or to the grid's blocks) is not verified; are they in scope or looks-and-effects to skip?
