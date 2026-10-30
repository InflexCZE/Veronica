---
name: block-thruster
description: Use when a block should produce thrust, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files
- the vanilla thruster to match: class (Ion, Hydrogen or Atmospheric) and size. Vanilla thrusters are in Assets/Blocks/Thrusters/<class>/<size in cm>; list_folder shows the sizes.
- nozzle spots: from the block that already uses the model, or the user positions them in the Editor
- folder: a mod folder for new files, when some are made

Plan:
- Read the block's components: note which thrust parts it already has and which are missing, and what its block component is
- Get from the user what this needs: the vanilla thruster to match when they did not name class and size, and a folder when new files are made
- Set up the thrust: the ThrusterComponent, and a ThrusterDefinition with thrust power and power use from the matched thruster unless the user wants more or less force; the template's values are placeholders, never defaults: write ThrustPower even when the template's value looks close
- Set up the nozzle dummies: keep or create one Thruster-type dummy per nozzle; on a model another block already uses, copy that block's nozzle spots and ask the user to check; on a new model, show the block and ask the user to move them into place in the Editor (not to give numbers); read them back
- Set up the flame: add the flame component when it is missing; change flame and sound only when the user asked for it
- Show the block

Steps:
1. read(block) lists its components with sides and definitions. A block made from a thruster template (find_template("thrust") lists Ion Thrusters, Hydrogen Thrusters, Atmospheric Thrusters) already has a ThrusterComponent with a generated ThrusterDefinition, a ThrusterEffectsComponent for the flame and one Thruster-type dummy; its ThrustClass and ConsumedResource are already right. A block made for another function has none of them. Fill what exists, add what is missing.
2. Ask the user, with wait_for_user, for what is not given: which vanilla thruster to match (class and size; Ion and Atmospheric draw Electricity, Hydrogen draws Hydrogen) and a folder for new files.
   Power: a thruster draws power only through a PowerableBlockComponent, the block component of thruster templates, whose PowerableBlockDefinition has the class's ConsumedResource. A block whose block component is a FunctionalBlockComponent (most other functions) thrusts without drawing any power. Tell the user so in the same message; making it draw power means replacing that block component and moving its settings (name, kind, mount points, recipe) into a PowerableBlockDefinition, which the tools do not do.
3. read the vanilla thruster's ThrusterDefinition and BlockModelComponentDefinition.
   Thrust:
   Missing: component_add(block, "ThrusterComponent"). The parts come from the vanilla thruster's files; create_prefab would make a whole new placeable block.
   Its definition is a ThrusterDefinition:
   - The block's own (generated or in the mod): write(def, "ThrustPower", <vanilla value, scaled if the user wants more or less force>) and write(def, "ResourcesRequiredToThrust", <vanilla value>). It stays the component's definition: the block's other definitions are its own files too, and a repointed component leaves the generated one unused.
   - No definition yet: when the user wants the vanilla thruster's values as they are, point the component at its ThrusterDefinition with write(block, "Components[ThrusterComponent].Definition", <it>). For other values, create_partial a copy of it in the folder, write the values to the copy, and point the component at the copy.
   Never write to a vanilla file; a later change to a vanilla definition the block points at is made on a create_partial copy, then the component is pointed at the copy. The copy is a new file: ask the folder unless the user gave one.
4. On the block's BlockModelComponentDefinition, each nozzle is a dummy whose Type is the Thruster dummy type; flame comes out along the dummy's +Z. read(def, "Dummies") first: keep a Thruster-type dummy that is there for the first nozzle. None there, or more nozzles: element_add(def, "Dummies"), then write(def, "Dummies[i].Name", ..) and write(def, "Dummies[i].Type", "Thruster"). Leave the other dummies as they are.
   A model another block already uses (vanilla or another mod's) has its nozzle spots in that block's BlockModelComponentDefinition: copy each Thruster-type dummy's Transform and Scale from there, then show(the block's prefab) and ask the user to check, with wait_for_user.
   On a new or imported model, where a nozzle sits is the user's call: the model's geometry is not visible to you, so never compute positions, not even from a description. Ask the user to move the dummy in the Editor. show(the block's prefab), tell the user which dummy to move (its path, e.g. Dummies[0].Transform, and its scale Dummies[0].Scale) and that the Render View shows dummies under Preview Settings, Dummies, then wait_for_user.
   After the answer, read(def, "Dummies") and mark the item with the positions and scales read.
5. Flame: the ThrusterEffectsComponent on the client draws it at the Thruster-type dummies. Missing: component_add(block, "ThrusterEffectsComponent", <the vanilla thruster's ThrusterEffectsComponentDefinition>). Flame and sound stay as they are unless the user asks: write(def, "FlameLength", <m>) on the block's own ThrusterEffectsComponentDefinition (a create_partial copy when it is vanilla's), write(def, "OnUseAudio", "<FMOD event path>") on its BlockEffectsComponentDefinition.
