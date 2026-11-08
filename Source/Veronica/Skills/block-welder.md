---
name: block-welder
description: Use when a block should weld and repair blocks in front of it like a ship welder, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Welder template; a new block comes from create-block first, which also gives it its name and models
- how fast it welds and how far around its tool spot it reaches: the user's own, or as a vanilla welder (Assets/Blocks/Welder)
- conveyor ports: how many and on which faces, the user's choice for their model
- the tool spot and the ports: from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: which components need a definition, its BlockKind and its model definition's dummies
- Get from the user what this needs: weld speed and reach, how many conveyor ports and where, and for a new model that they will place the tool spot and the ports
- Give every component that needs one a definition: a vanilla welder's, or a copy with the user's speed or reach
- Let it take components from the ship: add the item requester and the tag the welder finds it by
- Set up the dummies: the ToolDetection spot and the UseConveyor ports, placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components; one listed with "needs <type>" has no definition yet. A block made from the Welder template has five: IntegrityToolComponent (the welding), DummyInteractedEntityProviderAdapterComponent and SphereShapeEntityDetectorComponent (where it welds), AreaIntegrityToolEntityDetectorComponent and InventoryComponent. Read the block definition's BlockKind and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, how fast it welds and how far it reaches; offer a vanilla welder as a suggestion. Ask in the same message how many conveyor ports it has and on which faces: a vanilla welder's ports fit its own model. For a new model, say they will place the tool spot and the ports in the Editor.
3. Definitions: for each component that needs one, write(block, "Components[<component>].Definition", <the definition the same component has on a vanilla welder>). A value the user wants different goes on a create_partial copy of that definition, set on the component instead:
   - IntegrityToolDefinition IntegrityChange: the weld speed, multiplying how fast a block is built; 1 is a block's own build time, 2 twice as fast. A negative value grinds instead of welding.
   - SphereShapeEntityDetectorDefinition QueryRadius: the radius in metres of the welding area around the tool spot.
4. Components from the ship: the welder uses the components in its own inventory; to pull missing ones through the conveyor network it needs an ItemRequesterComponent, which the template lacks. component_add(block, "ItemRequesterComponent"), then element_add(block, "Components[ItemRequesterComponent].Tags", key "IItemRequester"): the welder finds the requester by that tag and ignores it without. Without the requester the block welds only with what players put into it by hand.
5. Dummies: the welder needs one ToolDetection dummy: the centre of the welding area, usually just in front of the welder's tip; without it the block fails when it is built. Conveyor ports are UseConveyor dummies, one per port. element_add(BlockModelComponentDefinition, "Dummies") per dummy, then write its Name and Type (Assets/System/DummyTypes/ToolDetection.def or UseConveyor.def).
   Positions: for a model another block uses, copy that block's dummies of the same Type; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.
6. Players switch the welder on in its terminal, and it works only while the grid has power.

Show:
show(the block). Ask the user to check the tool spot and the conveyor ports in the Render View (Preview Settings, Dummies).
