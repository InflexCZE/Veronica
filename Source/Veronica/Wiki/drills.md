---
name: drills
title: Drills: digging, reach, damage in front, ore storage and conveyors
source: SE2 source DrillComponent, DrillDefinition, SphereShapeDefinition, SphereShapeEntityDetectorDefinition, BoxShapeEntityDetectorDefinition, ShapeEntityDetectorComponent, ToolDamageComponent, InventoryDefinition, InventoryStockpileDefinition; vanilla drills (Assets/Blocks/Drills), the Drills template
description: What a ship drill is made of: the two drilling actions and their brush shapes, drilling speed, the reach that starts drilling, the damage box in front, where the ore goes and how it reaches the conveyors, the dummies it needs, power, what the Drills template leaves empty, and the parts that are only looks.
---
Parts:
- DrillComponent, on the server, with its DrillDefinition. Two actions: the primary digs and collects ore into the block's inventory; the secondary digs without collecting. Each cuts its brush shape (PrimaryBrushShape, SecondaryBrushShape; a SphereShapeDefinition's Radius is in metres) at its spot (PrimaryActionDummy, SecondaryActionDummy). PrimaryDrillingTimeModifier and SecondaryDrillingTimeModifier multiply the time a material takes to dig: 1 is standard, less is faster.
- SphereShapeEntityDetectorComponent with its SphereShapeEntityDetectorDefinition: QueryRadius, in metres, around its QueryDummy (the PrimaryDrill spot); terrain inside it starts the drilling.
- ToolDamageComponent with its ToolDamageDefinition, and BoxShapeEntityDetectorComponent with its BoxShapeEntityDetectorDefinition: damage to what is in front of the drill, in a box of QueryHalfExtents (half size, metres) around the DrillDamage spot. DamageAdapterPrefab is the prefab that applies the damage; the vanilla one has adapters for characters, plants and blocks.
- InventoryComponent, on the server: the ore. MaxMass is in kg; WhitelistedTypes Ore keeps other items out. When it is full, mined ore drops on the ground (DropOreAsFallback).
- InventoryStockpileComponent with its InventoryStockpileDefinition: FillRatioPush pushes items out to the conveyor network while the inventory is fuller than that ratio (0 pushes everything); none never pushes. PreventStockpilingFromAddingItems on the inventory stops other blocks pushing items into the drill.
- ConveyorComponent: the network connects at UseConveyor dummies, one per port.
- PowerableBlockComponent as the block component: the drill digs only while the block works (built, on and powered).

Dummies, each required where a component names it: without one the block fails when it is built.
- PrimaryDrill and SecondaryDrill: where the two actions cut; often the same spot at the drill head.
- DrillDamage: the centre of the damage box.

The Drills template: a block made from it gets its own copy of every definition and the vanilla drills' BlockKind, but they are left empty: no brush shapes, a detector radius and damage box of 0, no damage prefab, no push, an inventory that takes any item, and no dummies. Such a block never digs, and the game's validation reports each empty value. Its DrillToolEffectsComponent points at an abstract base definition, which validation also rejects; a vanilla drill's effects definition fixes it. The block's own definitions are filled rather than replaced: a component set on a vanilla definition leaves its own copy empty, and that copy still fails validation.

Looks only, not needed to dig: the spinning head (a subpart with its animator), the drilling particles and sounds, the camera shake.
