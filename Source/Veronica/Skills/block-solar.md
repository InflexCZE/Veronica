---
name: block-solar
description: Use when a block should make power from sunlight like a solar panel, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Solar Panels template
- the most power it makes, and whether its cells face one side or both: the user's own, or as a vanilla solar panel (Assets/Blocks/SolarPanel/SolarPanel/<size>)
- the sun sensor spots on each face with cells: from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: its SolarGeneratorDefinition's output and efficiency, and its model definition's dummies
- Get from the user what this needs: the most power it makes, one side or both, and for a new model that they will place the sun sensor spots
- Set the output and efficiency on the block's SolarGeneratorDefinition
- Set up the sun sensor dummies on each face with cells, placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components; the SolarGeneratorComponent names the block's SolarGeneratorDefinition. Read MaxOutput and EfficiencyMultiplier on it, and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, the most power the panel makes and whether its cells face one side or both; offer a vanilla solar panel to match as a suggestion. Values left from the template were not chosen for this block. For a new model, say in the same message that they will place the sun sensor spots in the Editor, and ask how many per face.
3. On the SolarGeneratorDefinition:
   write(def, "MaxOutput", n): kW of Electricity in full sunlight.
   write(def, "EfficiencyMultiplier", 1 for cells on one side, 2 for both). Output is the average over all sun sensors, so on a two-sided panel only half of them see the sun at once; 2 gives such a panel its full output with one side lit.
4. Sun sensors: the panel sees the sun only through dummies of the definition's DummyType (SolarOcclusionDetector); with none the block fails validation and makes no power. A block made from the template has none: for each spot, element_add(BlockModelComponentDefinition, "Dummies"), then write its Name and Type (Assets/System/DummyTypes/SolarOcclusionDetector.def). Each sensor counts the sun only when its forward (the dummy's -Z) points out of the cell face and nothing shades it, so a two-sided panel needs sensors facing out of each side.
   Positions: for a model another block uses, copy the Transform and Scale of that block's sensor dummies; for a new model, the user places them, each facing out of its face: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.

Show:
show(the block). Ask the user to check the sun sensor dummies in the Render View (Preview Settings, Dummies); the output is tested in game, in sunlight.
