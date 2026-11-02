---
name: welders
title: Welders: weld speed, welding area, components from the ship, power
source: SE2 source IntegrityToolComponent, IntegrityToolDefinition, DummyInteractedEntityProviderAdapterComponent, SphereShapeEntityDetectorComponent, AreaIntegrityToolEntityDetectorComponent, ItemRequesterComponent, PowerableBlockComponent; vanilla welders (Assets/Blocks/Welder), the Welder template
description: What a ship welder is made of: weld speed and its unit, the welding area and the dummy that places it, where the components it welds with come from, power, what the Welder template lacks, and the parts that are only looks.
---
Parts:
- IntegrityToolComponent, on the server, with its IntegrityToolDefinition: the welding. IntegrityChange multiplies how fast a block gets built: 1 is the block's own build time, 2 twice as fast; a negative value grinds blocks down instead. IsAreaTool makes it work on every block in its area. MaxInteractionDistance is in metres from the tool's centre.
- SphereShapeEntityDetectorComponent with its SphereShapeEntityDetectorDefinition: the welding area, a sphere of QueryRadius metres around its QueryDummy.
- DummyInteractedEntityProviderAdapterComponent: the tool's working point, at the dummy its definition names.
- AreaIntegrityToolEntityDetectorComponent: collects the blocks inside the area.
- InventoryComponent, on the server: holds the components the welder uses.
- ItemRequesterComponent, on the server: pulls the components a weld needs through the conveyor network into the welder's inventory. The welder finds it by the tag IItemRequester; it finds the inventory by the tag InventoryRequester.
- ConveyorComponent, connected only at UseConveyor dummies.
- PowerableBlockComponent as the block component, ConsumedResource Electricity with Amount 0: it draws nothing but works only while the grid has power.

Dummies: one of Type ToolDetection, the centre of the welding area and the working point. Without it the block fails when it is built. UseConveyor dummies are its conveyor ports.

Where the components come from: a weld takes components from the welder's inventory first; with an item requester it asks the conveyor network for the rest. Without one, it welds only with what players put into it by hand.

Players switch a welder on and off in its terminal; a mod does not preset it.

Vanilla welders: Assets/Blocks/Welder/<size>. Each has its own definitions except the area detector's, which is the template's shared one.

The Welder template: a block made from it gets its own PowerableBlockDefinition in the vanilla welders' G-Screen family, but five components with no definition (read shows "needs"), no item requester and no dummies.

Looks and extras, not needed to weld: IntegrityToolEffectsComponent (sparks and sound), ToolDamageComponent with a BoxShapeEntityDetectorComponent and a ToolDamage dummy (hurts characters and loose objects in front of the tool), InventoryStockpileComponent (pushes surplus out to the conveyors), and the ParticleEffects and ActivationParticle dummies.
