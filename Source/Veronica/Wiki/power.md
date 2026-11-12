---
name: power
title: Block power: what a block draws, idle and while working, priority, needing power present
source: SE2 source PowerableBlockComponent, PowerableBlockDefinition, IConsumedResourceModifier, ItemConverterComponent, ThrusterComponent, ItemToResourceConverterComponent, WheelCornerModuleComponent, ResourcePrioritiesConfiguration; vanilla blocks and the block templates' PowerableBlockDefinitions
description: How much power a block draws and when: the one resource sink on the PowerableBlockComponent, its resource type, the idle draw every block has while on, the components that add a draw while they work (crafting, thrusting, converting, driving), blocks that draw nothing at all, consumption priority when power runs short, why a block with a draw of 0 still needs power on the grid, and why template power values are not vanilla values.
---
The sink: a block draws a resource only through a PowerableBlockComponent as its block component. Its PowerableBlockDefinition's ConsumedResource names the resource (Type: Electricity, Hydrogen for hydrogen thrusters) and the idle Amount. ConsumedResource none: the block has no sink, draws nothing and needs no power to run (reactors, vanilla batteries). A FunctionalBlockComponent as the block component draws nothing either (block-components page: component_upgrade).

Total draw, only while the block is built and on: the idle Amount plus what each working component on the block adds. Off or not fully built: 0. All of it is counted in the block's one resource type.

| component | field | drawn |
|---|---|---|
| PowerableBlockComponent | its definition's ConsumedResource.Amount | all the time the block is on |
| ItemConverterComponent | ItemConverterDefinition.ConsumedResource | while crafting or with an order queued |
| ThrusterComponent | ThrusterDefinition.ResourcesRequiredToThrust | the full amount while firing at any strength, none otherwise |
| ItemToResourceConverterComponent | its definition's PowerableBlockResourceRequiredToOperate | while converting |
| WheelCornerModuleComponent | its definition's PropulsionResourceConsumption | while driving |

Every other function (gyroscopes, gravity generators, lights, antennas, ore detectors, sensors, doors and the rest) draws only the idle Amount: nothing scales it with use, so a gyroscope turning hard draws what an idle one does.

Units: Electricity in kW, Hydrogen in L/s.

Needing power present: a block with a ConsumedResource works only while its grid has that resource to give, even with Amount 0 and nothing else drawing: it draws nothing but stays unpowered, and does nothing, on a grid without power. A battery with a ConsumedResource cannot start a grid on its own for the same reason.

Priority: ConsumptionPriority on the PowerableBlockDefinition, 0 to 10. When supply is short, higher priority is supplied first and the lowest go without. Each template sets its family's priority. Leave it as it is unless the user asks.

Template values: a template's power values are placeholders, not vanilla's. Read the vanilla block the user names for its idle Amount and its components' draw; vanilla sizes of one kind often differ.
