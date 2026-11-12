# Power configuration on any block

**Exists:** each block skill sets the power of its own kind (block-production, block-thruster, block-reactor and others), mostly by reading a vanilla block's values. create_prefab gives a block made from the Production template nothing idle and the vanilla Assembler's crafting power and queue, since the template's own values (a draw idle, none while crafting, no queue) fit no vanilla production block.

**Missing:** one skill that configures a block's power consumption whatever the block is: its idle draw and every consumer on it, priority included, read from what is actually on the block.

What the game does:
- The block's PowerableBlockComponent opens one resource sink, in its definition's `ConsumedResource.Type`, at its `ConsumptionPriority`. With `ConsumedResource` null there is no sink and nothing is drawn.
- While the block is built and on, the draw is the sum of `RecomputeConsumedResource()` over every component implementing `IConsumedResourceModifier`. PowerableBlockComponent is one of them and adds its idle `ConsumedResource.Amount`. Off or unbuilt draws 0.
- Consumers that add to the sum:
  - ItemConverterComponent: `ItemConverterDefinition.ConsumedResource`, while crafting or with an order queued.
  - ThrusterComponent: `ResourcesRequiredToThrust`, all or nothing while firing.
  - ItemToResourceConverterComponent: `PowerableBlockResourceRequiredToOperate`, while converting.
  - WheelCornerModuleComponent: `PropulsionResourceConsumption`, while propelling.
- Gyroscopes, gravity generators, lights, antennas, ore detectors, sensors and the rest draw only the idle amount; nothing scales it with use.
- Priority runs 0 to 10 (`ResourcePrioritiesConfiguration`, MaxConsumerPriority); higher is supplied first when supply is short. Templates set their own, e.g. Thrusters 5, Production 9.

Template idle vs the vanilla blocks made from it, where they differ:

| template | template idle | vanilla idle |
|---|---|---|
| Production | 150 | 0 (all five) |
| Antennas | 160 | 150, 15 |
| GatlingTurret | 20 | 50, 5 |
| Gyroscope | 5 | 25, 5, 75 |
| RocketTurret | 20 | 25, 50 |
| GravityGenerator | 0 | 1600 |
| OreDetector | 0 | 50, 250 |

Every other template is 0, or its vanilla blocks keep its value. Only Production had one value all its vanilla blocks agree on, hence its create_prefab repair; the others vary by size and their skills read the vanilla value.

**Where to start:** `PowerableBlockComponent.cs` (UpdateConsumedResource), `IConsumedResourceModifier.cs` in the game source; the new wiki page power.md; the power steps of the existing block skills, which this skill would take over or link to.

**Owner decision:** deferred; a special skill later. Open questions:
- One skill activated from each block skill's power step, or a skill the user starts on its own for an existing block?
- Does it take over the block skills' power steps, or do they keep theirs and link to it?
- Does it expose ConsumptionPriority (now left at the template default), and the resource type (hydrogen thrusters draw Hydrogen)?
