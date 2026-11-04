---
name: block-subpart
description: Use when a block should get a part with its own model that can move, such as a ring or fan that spins, on a block that already exists; it does not create the block or make the part move (subpart-spin does).
---
Needs:
- the block: its prefab binding or any of its files
- the part's model: an imported .model
- a folder for the part's files, e.g. the block's folder
- where the part sits: the user positions its spot in the Editor

Plan:
- Read the block
- Create the part: a prefab from the Subpart template with the part's model
- Link it: a ConditionalSubPartsComponent on the block that places the part at a new dummy type
- Add the spot: a dummy of that type, placed by the user
- Next skill: subpart-spin when the part should spin

Steps:
1. read(block) lists its components; read its BlockModelComponentDefinition's Dummies.
2. create_prefab("Blocks/Subparts/Subpart", <part name>, folder). create_definition("ModelComponentDefinition", ...), write its Model, then write(part prefab, "Components[ModelComponent].Definition", <it>): the template's own points at no model.
3. create_definition("DummyTypeDefinition", ...) for the part's spot. create_definition("ConditionalSubPartsDefinition", ...), component_add(block, "ConditionalSubPartsComponent", <it>), element_add(it, "SubParts", key=<dummy type>, value=<part prefab>).
4. element_add(BlockModelComponentDefinition, "Dummies"), write its Name and Type (the new dummy type). Never compute its position: show(the block), ask the user to place it (name the path, e.g. Dummies[0].Transform) with wait_for_user, then read the dummy again.

Show:
show(the block). The part appears at its spot while the block works.
