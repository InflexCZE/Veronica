---
name: item-to-resource
title: Item to resource blocks: reactors, gas generators, any block that turns an item into power, hydrogen or oxygen
source: SE2 source ItemToResourceConverterComponent and ItemToResourceConverterDefinition (ResourceConversion), InventoryStockpileComponent and InventoryStockpileDefinition, FunctionalBlockDestructionExplosionComponent, PowerableBlockDefinition, ResourceTypeDefinition; vanilla reactors (Assets/Blocks/Reactor) and O2/H2 generators (Assets/Blocks/O2H2Generator), the uranium ore and ice items, the Reactors and O2H2 Generator templates
description: A block that consumes one item and makes one or more resources from it (power, hydrogen, oxygen): the single-output and list-of-outputs forms of its converter and when each applies, conversion rates and how fast it consumes the item, turning a single output into a list, power it draws while converting, its input inventory, taking the item from the conveyors, gases leaving through conveyor ports, reactor explosion, the templates and vanilla blocks to start from.
---
One item in, one or more resources out (1:N). The ItemToResourceConverterComponent, on the server, does it with its ItemToResourceConverterDefinition:
- ItemConsumed: the item it consumes (the fuel). None: it makes nothing. The item must be divisible (an ore is; a validation error otherwise).
  - It is always an item in the block's input inventory: no component turns a gas or another resource into a resource, so a generator fed from a gas tank is not possible.
- What it makes, in one of two forms. Exactly one is set; neither is a validation error:
  - Single output, for one resource: ResourceProduced (the resource), MaxResourceProduction (the most it makes per second), ResourcesPerItem (how much one unit of the item makes) and ProductionPriority. It consumes at most MaxResourceProduction / ResourcesPerItem units a second. MaxItemsPerSecondConsumed is not read. The game marks this form deprecated; vanilla reactors still use it.
  - List of outputs, for one or more resources: ResourcesProduced, one entry per resource: Resource, ConversionRate (how much one unit of the item makes, above 0) and ProductionPriority. MaxItemsPerSecondConsumed is the most of the item it consumes per second; each resource's most per second is MaxItemsPerSecondConsumed * its ConversionRate. Vanilla O2/H2 generators use it.
  - With entries in ResourcesProduced, ResourceProduced, MaxResourceProduction, ResourcesPerItem and ProductionPriority are not read; ResourceProduced still set is a validation warning, so it is written none.
- How much of the item it consumes: as much as the most demanded output needs, not the sum. A generator whose hydrogen is drawn and oxygen is not consumes for the hydrogen alone; the oxygen that much ice could make is lost unless something draws it.
- PowerableBlockResourceRequiredToOperate: power it draws only while converting (the power page). None: it needs no power to convert. It works only with a PowerableBlockDefinition whose ConsumedResource is set; without power it stops converting.

Units: Electricity in kW (ResourcesPerItem and ConversionRate in kW*s, kJ, per unit), Hydrogen and Oxygen in L/s (ConversionRate in L per unit). An ore's unit is 1 kg (its ItemDefinition's MassPerUnit). Resources: Assets/System/ResourceTypes (Electricity.def, Hydrogen.def, Oxygen.def).

Turning a single output into a list, keeping what it makes: add a ResourcesProduced entry for the old resource with ConversionRate = ResourcesPerItem and the old ProductionPriority, set MaxItemsPerSecondConsumed = MaxResourceProduction / ResourcesPerItem, then add the other resources and write ResourceProduced none.

The other parts:
- InventoryComponent on the server, tagged InventoryInput: the input inventory. Its InventoryDefinition's MaxMass is how much it holds, in kg; WhitelistedItems and WhitelistedTypes decide what goes in, and the item must be allowed.
- InventoryStockpileComponent: keeps the inventory stocked from the conveyor network. While the inventory is filled below FillRatioPull (a share, 0 to 1), it pulls the definition's Items. With FillRatioPull null or no Items, the block takes nothing by itself: the item has to be pushed in or loaded by hand.
- ConveyorComponent: the conveyor network connects only at UseConveyor dummies; a player also opens the inventory at them (the inventory-and-conveyors and dummies pages). Hydrogen and oxygen leave the block only through the conveyor network, so a block making them needs at least one port; power goes over the grid without one.
- PowerableBlockComponent as the block component: ConsumedResource none for a block that needs no power (reactors), Electricity for one that draws power to convert (O2/H2 generators).
- FunctionalBlockDestructionExplosionComponent, on reactors only: a block destroyed while working explodes, at its ExplosionCentre dummy or the block's centre without one.

Templates: find_template matches words in the template's name: "reactor" finds Reactors, "generator" finds O2H2 Generator (and Gravity Generators, which is not one); "hydrogen", "oxygen" and "power" find none of them. None is right for every block. What a block made from each brings, every part its own definition:
- Reactors: single output Electricity, next to no power, no item; a uranium-ore-only input inventory; a stockpile pulling nothing; ConsumedResource none; the reactor explosion; the vanilla reactors' G-Screen family; no dummies, so no conveyor ports.
- O2H2 Generator: a list of outputs, hydrogen at 4 and oxygen at 2 L per unit as vanilla's, no item, at most 1 unit a second (vanilla: 25 and 225); an input inventory taking any item; a stockpile pulling nothing; ConsumedResource Electricity with no idle draw and no power while converting; no explosion; the vanilla O2/H2 generators' G-Screen family; no dummies, so no conveyor ports.
Values the template sets that the user did not choose are placeholders: read the vanilla block of the same kind the user names.

Vanilla: reactors in Assets/Blocks/Reactor/Reactor/<size>, burning uranium ore; O2/H2 generators in Assets/Blocks/O2H2Generator/<size>, consuming ice and drawing power while converting. Each has its own converter, input inventory and stockpile definition; sizes differ.

Looks only, not needed to work: spinning subparts, effects, terminal and button dummies of the vanilla models.
