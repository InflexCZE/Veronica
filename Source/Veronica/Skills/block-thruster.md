---
name: block-thruster
description: Use when a block should produce thrust, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files
- the vanilla thruster to match: class (Ion, Hydrogen or Atmospheric) and size. Vanilla thrusters are in Assets/Blocks/Thrusters/<class>/<size in cm>; list_folder shows the sizes.
- nozzle spots: from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block's components: note which thrust parts it already has and which are missing, and what its block component is
- Get from the user what this needs: the vanilla thruster to match when they did not name class and size, and a folder when new files are made
- Set up the thrust: the ThrusterComponent and its ThrusterDefinition, with every value fitting what the user wants
- Set up the nozzle dummies: one Thruster-type dummy per nozzle, placed as the place-dummies reference says; read them back
- Set up the flame: add the flame component when it is missing; change flame and sound only when the user asked for it
- Show the block

Steps:
1. read(block); the block-components reference says how to read it, add a component and pick its definition. A block made from a thruster template (find_template("thrust") lists them) already has a ThrusterComponent with its own ThrusterDefinition, a ThrusterEffectsComponent for the flame and one Thruster-type dummy. A block made for another function has none of them.
2. Ask the user, with wait_for_user, for what is not given: which vanilla thruster to match (class and size; Ion and Atmospheric draw Electricity, Hydrogen draws Hydrogen) and a folder for new files.
   Power: a thruster draws power only through a PowerableBlockComponent, the block component of thruster templates, whose PowerableBlockDefinition has the class's ConsumedResource. A block whose block component is a FunctionalBlockComponent (most other functions) thrusts without drawing any power. Tell the user so in the same message; making it draw power means replacing that block component and moving its settings (name, kind, mount points, recipe) into a PowerableBlockDefinition, which the tools do not do.
3. Thrust: read the matched thruster's ThrusterDefinition, then add or fill the block's ThrusterComponent and its definition as the block-components reference says. The ThrusterDefinition's fields set how the thruster behaves; each one should fit what the user wants: as the matched thruster, changed as the user asks, or their own value. Parts come from existing files; create_prefab would make a whole new placeable block.
4. Nozzles: each nozzle is a dummy whose Type is Thruster; flame comes out along the dummy's +Z. Keep a Thruster-type dummy that is there for the first nozzle; add one for each nozzle more. Place them as the place-dummies reference says, then mark the item with the positions and scales read back.
5. Flame: the ThrusterEffectsComponent on the client draws it at the Thruster-type dummies. Missing: component_add(block, "ThrusterEffectsComponent", <the matched thruster's ThrusterEffectsComponentDefinition>). Flame and sound stay as they are unless the user asks: the flame is in the ThrusterEffectsComponentDefinition, the sound in the BlockEffectsComponentDefinition.
