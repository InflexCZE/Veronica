---
name: connectors
title: Connectors: docking, connector size, connection groups, the item ejector and the spots a connector needs
source: SE2 source ConnectorComponent, ConnectorDefinition, MechanicalBlockComponent, MechanicalBlockHelper, MechanicalBlockDefinition, MechanicalConveyorComponent, MechanicalBlockCounterpartDetectionComponent, DummyShapedEntityDetectorComponent, ItemEjectorComponent, ConveyorGraphPostProcessor, CubeGridConfiguration, PowerableBlockDefinition; vanilla connectors (Assets/Blocks/Connectors), GridConveyorSystem.def, GridConfiguration.def, PermissionsConfig.def, MechanicalBlockConfig.def, the Connectors template
description: How a connector block docks to another connector and joins conveyor networks: its components, connector size, connection groups (Physical, Terminal, Ship), the docking face (Pivot), detection box (ConnectorVolume), the item ejector and its spot, conveyor ports, power, what the template brings, and the parts that are only looks.
---
Parts:
- ConnectorComponent with its ConnectorDefinition: ConnectorSize and ConnectionGroups.
- MechanicalBlockCounterpartDetectionComponent and MechanicalBlockCounterpartAlignmentComponent: find a counterpart in front of the docking face and pull the two together; a DummyShapedEntityDetectorComponent measures the detection box.
- MechanicalConveyorComponent: links the two blocks' conveyors while docked.
- InventoryComponent (server, tag EjectorInventory) and ItemEjectorComponent: the connector's own storage and throwing items out of it.
- PowerableBlockComponent as the block component.

Docking:
- Two connectors dock only when their ConnectorDefinitions name the same ConnectorSize (Assets/System/ConnectorSizes). A size of none docks only with other none-size connectors.
- A connector docks when it is built and on; a docked connector undocks when it stops being functional. The PowerableBlockDefinition's ConsumedResource Amount is 0 in the template and vanilla.
- ConnectionGroups are what joining two grids shares:
  - Physical: the grids are joined physically.
  - Terminal: the docked grids appear in each other's terminal; the conveyors join through this group (GridConveyorSystem's ConveyorConnectionTypes).
  - Ship: the docked grids count as one ship, so toolbar actions reach blocks across the dock, and the grids need the same owner.

Spots (dummies of the BlockModelComponentDefinition):
- Pivot: the docking face. Its forward (-Z) points out of the face; a counterpart is found and aligned against it, and two connectors dock when their faces face each other closely. A mechanical block without one has no docking face.
- ConnectorVolume: the detection box in front of the face; the dummy's position and rotation place it and its Scale is its full size in metres. Without it the block fails when it is built.
- The ejector spot, type Connector_ItemEjectorComponentDefinition: items appear there when its volume is free and fly out along its forward (-Z) at EjectionSpeed (m/s), at most MaxBatchMass (kg) per throw (a single unit when one unit weighs more). The ItemEjectorComponentDefinition's EjectorDummy names this type; with none the block fails when it is built.
- UseConveyor ports: each needs a Name, or it joins nothing. The block's ports, inventory and the docking link form one conveyor node.

Vanilla connectors: Assets/Blocks/Connectors/<size> (50, 150), each with its own ConnectorSize; vanilla's ConnectionGroups add Ship to the template's.

The Connectors template: a block made from it gets its own copies of the ConnectorDefinition (ConnectorSize none, Physical and Terminal only), the ItemEjectorComponentDefinition (no ejector dummy type, no batch mass, no speed), the InventoryDefinition and the PowerableBlockDefinition, in the vanilla connectors' G-Screen family. The detection and alignment keep the template's shared definitions. Its dummies list is empty.

Looks only, not needed to dock: the lock state indicator, effects and sounds, the damage particles (on a Particles dummy in vanilla) and the construction particles.
