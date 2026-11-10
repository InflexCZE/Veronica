---
name: block-thruster
description: Use when a block should produce thrust, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files
- the vanilla thruster to match: class (Ion, Hydrogen or Atmospheric) and size. Vanilla thrusters are in Assets/Blocks/Thrusters/<class>/<size in cm>; list_folder shows the sizes.
- nozzle spots: from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block's components: note which thrust parts it already has and which are missing, and what its block component is
- Get from the user what this needs: the vanilla thruster to match when they did not name class and size, and a folder when new files are made
- Make the block draw power: upgrade its block component when it is a FunctionalBlockComponent
- Set up the thrust: the ThrusterComponent and its ThrusterDefinition, with every value fitting what the user wants
- Set up the nozzle dummies: one Thruster-type dummy per nozzle, placed by copying or by the user; read them back
- Set up the flame: add the flame component when it is missing; change flame and sound only when the user asked for it
- Show the block

Steps:
1. read(block) lists its components: type, sides, tags, definition. A block made from a thruster template (find_template("thrust") lists them) already has a ThrusterComponent with its own ThrusterDefinition, a ThrusterEffectsComponent for the flame and one Thruster-type dummy. A block made for another function has none of them.
2. Ask the user, with ask_user, for what is not given: which vanilla thruster to match (class and size; Ion and Atmospheric draw Electricity, Hydrogen draws Hydrogen) and a folder for new files.
3. Power: a thruster draws power only through a PowerableBlockComponent, and the flame needs one too; a block whose block component is a FunctionalBlockComponent (most functions other than thrust) would thrust for free and fail to load with the flame. Upgrade it: component_upgrade(block, "FunctionalBlockComponent", <the matched vanilla thruster's prefab binding>): the matched thruster is the closest base, with its class's resource. The block keeps its name, recipe and every other value; the reply names the ConsumedResource the template brought.
4. Thrust: read the matched thruster's ThrusterDefinition. A missing ThrusterComponent: component_add(block, "ThrusterComponent"). Its definition: the block's own when it has one; otherwise the matched thruster's as it is, or a create_partial copy of it when values differ. Write each value that does not fit what the user wants: as the matched thruster, changed as the user asks, or their own value. Parts come from existing files; create_prefab would make a whole new placeable block.
5. Nozzles: each nozzle is a dummy whose Type is Thruster in the BlockModelComponentDefinition's Dummies; flame comes out along the dummy's +Z. Keep a Thruster-type dummy that is there for the first nozzle; for each nozzle more, element_add(def, "Dummies"), then write its Name and Type. Positions: for a model another block uses, copy the Transform and Scale of that block's Thruster-type dummies; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[1].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions and scales read back.
6. Flame: the ThrusterEffectsComponent draws it at the Thruster-type dummies. Missing: component_add(block, "ThrusterEffectsComponent", <the matched thruster's ThrusterEffectsComponentDefinition>). Flame and sound stay as they are unless the user asks.
