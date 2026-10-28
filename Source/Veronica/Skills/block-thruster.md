---
name: block-thruster
description: Use after create-block made the block, when it produces thrust; it does not create the block.
---
Needs:
- thrust class: Ion, Hydrogen or Atmospheric (Ion and Atmospheric draw Electricity, Hydrogen draws Hydrogen)
- a comparable vanilla thruster: same class, closest size. Vanilla thrusters are in Assets/Blocks/Thrusters/<class>/<size in cm>; list_folder shows the sizes.
- the user positions the nozzles in the Editor

Plan:
- Fill thrust power and power use: both, from the comparable thruster unless the user wants more or less force; the template's values are placeholders, never defaults: write ThrustPower even when the template's value looks close
- Set up the nozzle dummies: keep or create one Thruster-type dummy per nozzle, then show the block and ask the user to position them; read them back
- Set flame and sound if the user asked for it; otherwise skip this item
- Show the block

Steps:
1. The template is the class's thruster template: find_template("thrust") lists them (Ion Thrusters, Hydrogen Thrusters, Atmospheric Thrusters). Its ThrustClass and ConsumedResource are already right.
2. read the vanilla thruster's ThrusterDefinition and BlockModelComponentDefinition.
3. On the new ThrusterDefinition:
   write(def, "ThrustPower", <vanilla value, scaled if the user wants more or less force>)
   write(def, "ResourcesRequiredToThrust", <vanilla value>)
   The template's values are placeholders, not the class's.
4. On the new BlockModelComponentDefinition, each nozzle is a dummy whose Type is the Thruster dummy type; flame comes out along the dummy's +Z. The template has one at a placeholder spot: keep it for the first nozzle. More nozzles: element_add(def, "Dummies"), then write(def, "Dummies[i].Name", ..) and write(def, "Dummies[i].Type", "Thruster").
   Where a nozzle sits is the user's call: the model's geometry is not visible to you, so do not compute positions. show(the block's prefab), tell the user which dummy to move (its path, e.g. Dummies[0].Transform, and its scale Dummies[0].Scale) and that the Render View shows dummies under Preview Settings, Dummies, then wait_for_user.
   After the answer, read(def, "Dummies") and mark the item with the positions and scales read.

5. Flame and sound stay as the template's unless the user asks: write(def, "FlameLength", <m>) on the generated ThrusterEffectsComponentDefinition, write(def, "OnUseAudio", "<FMOD event path>") on the generated BlockEffectsComponentDefinition.
