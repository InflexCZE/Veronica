---
name: block-thruster
description: Use after create-block made the block, when it produces thrust; it does not create the block.
---
Needs:
- thrust class: Ion, Hydrogen or Atmospheric (Ion and Atmospheric draw Electricity, Hydrogen draws Hydrogen)
- a comparable vanilla thruster: same class, closest size. Vanilla thrusters are in Assets/Blocks/Thrusters/<class>/<size in cm>; list_folder shows the sizes.
- where each nozzle is on the model, unless the model is a vanilla thruster's, reworked

Plan:
- Fill thrust power and power use: both, from the comparable thruster unless the user wants more or less force; the template's values are placeholders, never defaults
- Place the nozzle dummies: move the template's Thruster-type dummy to the nozzle position and scale; add one only for each extra nozzle
- Set flame and sound if the user asked for it; otherwise skip this item
- Show the block

Steps:
1. The template is the class's thruster template: find_template("thrust") lists them (Ion Thrusters, Hydrogen Thrusters, Atmospheric Thrusters). Its ThrustClass and ConsumedResource are already right.
2. read the vanilla thruster's ThrusterDefinition and BlockModelComponentDefinition.
3. On the new ThrusterDefinition:
   write(def, "ThrustPower", <vanilla value, scaled if the user wants more or less force>)
   write(def, "ResourcesRequiredToThrust", <vanilla value>)
   The template's values are placeholders, not the class's.
4. On the new BlockModelComponentDefinition, each nozzle is a dummy whose Type is the Thruster dummy type; flame comes out along the dummy's +Z. The template has one at a placeholder spot:
   write(def, "Dummies[0].Transform.Transform.Position", {X: .., Y: .., Z: ..})
   write(def, "Dummies[0].Scale", {X: .., Y: .., Z: ..})
   Copy the vanilla nozzles' positions and scale for a reworked vanilla model; otherwise use the nozzle spots the user gave. More nozzles: element_add(def, "Dummies"), then write its Name, Position, Scale and write(def, "Dummies[i].Type", "Thruster").

5. Flame and sound stay as the template's unless the user asks: write(def, "FlameLength", <m>) on the generated ThrusterEffectsComponentDefinition, write(def, "OnUseAudio", "<FMOD event path>") on the generated BlockEffectsComponentDefinition.
