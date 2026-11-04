---
name: reactors
title: Reactors: fuel, power, how long fuel lasts, fuel inventory, taking fuel from conveyors
source: SE2 source ItemToResourceConverterComponent and ItemToResourceConverterDefinition, InventoryStockpileComponent and InventoryStockpileDefinition, FunctionalBlockDestructionExplosionComponent, PowerableBlockDefinition; vanilla reactors (Assets/Blocks/Reactor), the uranium ore item, the Reactors template
description: What a reactor block does: the fuel it burns, the power it makes and its unit, how long a unit of fuel lasts, its fuel inventory, how it takes fuel from the conveyors, its explosion, what the template brings and what it leaves out.
---
Parts:
- ItemToResourceConverterComponent, on the server, with its ItemToResourceConverterDefinition:
  - ItemConsumed: the fuel item. None: the block burns nothing and makes no power.
    - Any item can be the fuel, but fuel is always an item in the fuel inventory: no component turns a gas or another resource into power, so a generator fed from a gas tank is not possible.
  - ResourceProduced: what it makes, Electricity for a reactor.
  - MaxResourceProduction: the most power it makes, in kW.
  - ResourcesPerItem: the energy one unit of fuel gives, in kW*s (kJ). At full power the block burns MaxResourceProduction / ResourcesPerItem units of fuel a second. An ore's unit is 1 kg (its ItemDefinition's MassPerUnit).
  - The Multiple Resources fields (ResourcesProduced, MaxItemsPerSecondConsumed) are for a block making several resources at once, such as gas from ice; vanilla reactors use the single resource fields above.
- InventoryComponent on the server, tagged InventoryInput: the fuel inventory. Its InventoryDefinition's MaxMass is how much it holds, in kg; WhitelistedItems and WhitelistedTypes decide what goes in, and the fuel must be allowed.
- InventoryStockpileComponent: keeps the inventory stocked from the conveyor network. While the inventory is filled below FillRatioPull (a share, 0 to 1), it pulls the definition's Items. With FillRatioPull null or no Items, the block takes no fuel by itself: it has to be pushed in or loaded by hand.
- ConveyorComponent: fuel reaches the block through the conveyor network, which connects only at UseConveyor dummies; a player also opens the fuel inventory at them (the inventory-and-conveyors and dummies pages).
- FunctionalBlockDestructionExplosionComponent: a reactor destroyed while working explodes, at its ExplosionCentre dummy or the block's centre without one.
- PowerableBlockComponent as the block component, with ConsumedResource none: a reactor needs no power to run.

Vanilla reactors: Assets/Blocks/Reactor/Reactor/<size>. Each has its own converter, fuel inventory and stockpile definition; they do not all burn the same fuel.

The Reactors template: a block made from it gets its own ItemToResourceConverterDefinition (no fuel, next to no power), its own fuel InventoryDefinition, its own InventoryStockpileDefinition (pulls nothing) and its own PowerableBlockDefinition in the vanilla reactors' block menu family. Its own explosion definition has a vanilla reactor's Explosion. It has no dummies, so no conveyor ports.

Looks only, not needed to make power: the spinning core subparts, the effects, the terminal and button dummies of the vanilla model.
