# O2/H2 generator block

**Exists:** `block-reactor`, the MyReactor eval task and the `reactors` wiki page make a block that burns a fuel item into power, on the Reactors template's ItemToResourceConverterDefinition. The wiki notes that the converter's Multiple Resources fields (ResourcesProduced, MaxItemsPerSecondConsumed) make several resources at once, such as gas from ice, and that reactors leave them unused. `block-tank` and MyTank cover the tanks the gas goes to. No skill, wiki page or eval task covers a gas generator.

**Missing:** a block that turns an item (ice) into oxygen and hydrogen. Vanilla O2H2Generator150 and 250 each copy an ItemToResourceConverterDefinition: ItemConsumed, no single ResourceProduced, and ResourcesProduced, a keyed list with one entry per gas (Resource, ConversionRate, ProductionPriority), plus MaxItemsPerSecondConsumed, ResourcesPerItem and the power it needs to run. Each also has its own InventoryDefinition, InventoryStockpileDefinition (pulling ice from the conveyors) and PowerableBlockDefinition. Looks: spinning subparts with InfiniteRotationAnimation, production effects, LOD materials. A skill and an eval task (a new generator like O2H2Generator150) would need the nested ResourcesProduced entries written by element, the ice whitelist on the inventory, conveyor ports, and a check that the gas reaches a tank.

**Where to start:** vanilla `Assets/Blocks/O2H2Generator`, `block-reactor` and `Source/Veronica/Wiki/reactors.md`, the O2/H2 part of the "not reached" row moved here from [SkippedBlockKinds.md](SkippedBlockKinds.md).

**Owner decision:** deferred out of the items and recipes round. Open questions:
- Start from the Reactors template and switch its converter to Multiple Resources, or from another template closer to vanilla's?
- Its own `block-gas-generator` skill, or a variant inside `block-reactor`?
- Does the eval task check gas reaching a tank, which pulls `block-tank` into the run?
