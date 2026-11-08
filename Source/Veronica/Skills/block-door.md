---
name: block-door
description: Use when a block should be a door with leaves that slide open, one create-block just made from the Doors template or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Doors template
- the leaves: how many, and each leaf's model, an imported .model
- how long it takes to open
- the spots: each leaf's closed and open spot, the panels players open it from, the doorway; the user positions them in the Editor

Plan:
- Read the block and its door definitions
- Get from the user what this needs
- Set the opening time and the doorway box
- Create each leaf: a bound prefab with its model and its slide between two spots
- Link the leaves to their closed spots
- Add the spots as dummies, placed by the user
- Show the block

Steps:
1. read(block) lists its components and definitions; read the DoorServerDefinition and the doorway BoxShapeEntityDetectorDefinition. A block made from the template opens in the vanilla sliding door's time, with its doorway box.
2. Ask the user, with ask_user, the leaves, how long it takes to open, and the panels; offer the vanilla sliding door (Assets/Blocks/Doors/SlideDoorNarrow/250) as a suggestion. Say they will place the spots in the Editor.
3. Door: write what the user wants that differs. OpeningDuration on the DoorServerDefinition is hh:mm:ss ("00:00:00.5"). QueryHalfExtents on the doorway detector definition is half the doorway box per axis, in metres, kept to the inside of the doorway.
4. Each leaf: create_definition("DummyTypeDefinition", ...) twice, its closed and its open spot. create_prefab("Empty Prefab", <leaf name>, folder) makes a bound prefab. create_definition("ModelComponentDefinition", ...) with its Model; create_definition("DoorSegmentTransformSLERPDefinition", ...) with ClosedPositionDummy and OpenPositionDummy. component_add(leaf, ...) in this order: DoorSegmentTransformSLERPComponent (with that definition), DoorSegmentComponent, ModelComponent (with the model definition), ParentRenderAdapterComponent (it brings HierarchyComponent), ModelRenderComponent and ChildTransformComponent. A door leaf is always a bound prefab like this, never one from the Subpart template.
5. element_add(block's ConditionalSubPartsDefinition, "SubParts", key=<leaf's closed dummy type>, value=<leaf's _Client prefab>) per leaf.
6. element_add(BlockModelComponentDefinition, "Dummies") per spot: each leaf's closed and open spot, a UseDoor panel per side (Assets/System/DummyTypes/UseDoor.def), one DoorwayObstacleDetectorDummy; write each one's Name and Type. Never compute positions: show(the block), ask the user to place them (name the paths) with wait_for_user, then read the Dummies again.

Show:
show(the block). The leaves appear at their closed spots; opening is seen in game.
