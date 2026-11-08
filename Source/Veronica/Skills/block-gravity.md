---
name: block-gravity
description: Use when a block should make artificial gravity like a gravity generator, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Gravity Generators template
- how far the field can reach at most, and the field a built block starts with (reach, strength, shape): the user's own, or as the vanilla gravity generator (Assets/Blocks/GravityGenerator)
- the power it draws: the user's own, or as the vanilla gravity generator

Plan:
- Read the block: its GravityGeneratorComponent's starting field, its GravityGeneratorDefinition and its PowerableBlockDefinition's power
- Get from the user what this needs: the largest reach, the starting reach, strength and shape, and the power draw
- Set the limits on the GravityGeneratorDefinition and the power on the PowerableBlockDefinition
- Set the field a built block starts with on its GravityGeneratorComponent
- Show the block

Steps:
1. read(block) lists its components; read(block, "Components[GravityGeneratorComponent]") shows the field a built block starts with: AffectDistance, GravitationalAcceleration, GravityShape. Read the GravityGeneratorDefinition it names and the ConsumedResource of the block's PowerableBlockDefinition.
2. Ask the user, with ask_user, for the largest reach, the starting reach, strength and shape, and the power draw; offer the vanilla gravity generator's values as a suggestion. Values left from the template were not chosen for this block: its starting reach and strength are 0, so a built block makes no gravity until a player sets them, and its power draw is 0.
3. Limits, on the GravityGeneratorDefinition: MaxAffectDistance (m) is the most reach players can set; MinGravitationalAcceleration and MaxGravitationalAcceleration (m/s^2) bound the strength, a negative value pushing instead of pulling. SupportedGravityShapes lists the shapes players can pick. Write the values that do not fit what the user wants.
   Power, on the PowerableBlockDefinition: ConsumedResource.Amount is the kW drawn while the block is on.
4. Starting field, on the block (one write reaches both sides):
   write(block, "Components[GravityGeneratorComponent].AffectDistance", m), within MaxAffectDistance.
   write(block, "Components[GravityGeneratorComponent].GravitationalAcceleration", m/s^2), within the strength limits. Players see strength in g: 1 g is 9.80665 m/s^2.
   GravityShape when the user wants another shape than the one set: Field, Spherical or Cylindrical; ask the wiki what each does.

Show:
show(the block). Gravity is tested in game: players change reach, strength and shape in the block's terminal.
