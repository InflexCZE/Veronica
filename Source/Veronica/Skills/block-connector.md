---
name: block-connector
description: Use when a block should dock to another ship or station and join their conveyor networks like a connector, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Connectors template; a new block comes from create-block first, which also gives it its name and models
- which connectors it docks with: the user's, e.g. the vanilla large connectors
- the docking face: which face of the model docks, from the block that already uses the model or from the user
- conveyor ports: how many and on which faces, from the user
- the docking spot, detection box, item ejector spot and ports: from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: its connector, item ejector and inventory definitions, and its model definition's dummies
- Get from the user what this needs: which connectors it docks with, the docking face and the conveyor ports, and for a new model that they will place the spots
- Set up docking: the connector size and connection groups on the ConnectorDefinition
- Set up the item ejector: its dummy type, batch mass and speed on the ItemEjectorComponentDefinition
- Set up the dummies: docking face, detection box, ejector spot and conveyor ports, placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components; one listed with "needs <type>" has no definition yet. Read its ConnectorDefinition, ItemEjectorComponentDefinition and InventoryDefinition, the same definitions of the vanilla connector the user names (Assets/Blocks/Connectors), and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, which connectors it should dock with, which face docks, and how many conveyor ports on which faces; a vanilla connector's ports fit its own model, not this one. For a new model, say in the same message that they will place the spots in the Editor.
3. Docking, on the ConnectorDefinition:
   ConnectorSize: two connectors dock only when their sizes are the same definition. The template's is none, so it docks with nothing but other none-size connectors; write the size of the vanilla connector it should dock with (Assets/System/ConnectorSizes).
   ConnectionGroups: what joining two grids shares. The template has Physical and Terminal (the conveyors pass through Terminal); vanilla connectors also have Ship, which makes the docked grids one ship for toolbar actions and requires the same owner. Match the vanilla connector: element_add(def, "ConnectionGroups"), then write the new entry.
4. Item ejector, on the ItemEjectorComponentDefinition: with the ejector on, the connector throws items out of its inventory at the ejector spot. The template's EjectorDummy is none, so the block fails when it is built. Write EjectorDummy (Assets/System/DummyTypes/Connector_ItemEjectorComponentDefinition.def), MaxBatchMass (kg per throw) and EjectionSpeed (m/s) as the vanilla connector or as the user wants.
5. Dummies: add each with element_add(BlockModelComponentDefinition, "Dummies"), then write its Name and Type (Assets/System/DummyTypes/...):
   - Docking face (Pivot.def): where two connectors meet. Its forward (-Z) points out of the docking face; two connectors dock when their docking faces face each other closely.
   - Detection box (ConnectorVolume_DummyType.def): the box in front of the face where a counterpart is searched; its Scale is the box's full size in metres. Without it the block fails when built.
   - Ejector spot (Connector_ItemEjectorComponentDefinition.def): items appear there and fly out along its forward (-Z).
   - Conveyor ports (UseConveyor.def): each with a Name; a port without a name joins nothing. They join the block to the ship's conveyors; docking joins the two networks through the connector.
   Positions: for a model another block uses, copy the Transform and Scale of that block's dummies of the same Type; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.

Show:
show(the block). Ask the user to check the docking face, detection box, ejector spot and ports in the Render View (Preview Settings, Dummies); docking is tested in game.
