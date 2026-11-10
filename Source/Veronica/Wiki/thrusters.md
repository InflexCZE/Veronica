---
name: thrusters
title: Thrusters: classes, power, flame and sound
source: SE2 source ThrusterComponent, ThrusterDefinition, ThrusterEffectsComponentDefinition, PowerableBlockComponent; vanilla thrusters (Assets/Blocks/Thrusters); resource types Electricity.def and Hydrogen.def
description: How a thruster works: its classes and what they draw, making an atmospheric thruster, its thrust direction, why a thruster block draws power or not, where its force, flame and sound are set, placing and sizing the flame, and why a new thruster shows no flame.
---
Classes: Ion and Atmospheric draw Electricity, Hydrogen draws Hydrogen. Vanilla ones are in Assets/Blocks/Thrusters/<class>/<size in cm>.

Parts:
- ThrusterComponent with its ThrusterDefinition: the thrust. The definition's fields set how the thruster behaves; each should fit what the user wants: as the matched vanilla thruster, changed as the user asks, or their own value.
  - ThrustPower: in N, a fixed value; there is no multiplier field. Thrust does no damage: blocks in front of the nozzle are not harmed.
  - ResourcesRequiredToThrust: in the class's resource unit: kW of Electricity, L/s of Hydrogen. It is all or nothing: the full amount while firing at any strength, none otherwise.
  - ThrustClass: the class as a string, Ion or Atmospheric for the electric ones. Ion and Atmospheric thrusters differ only by it and their values. It does not change the flame or sound: those are pointed at the class's own effects separately. A new atmospheric thruster starts from the Atmospheric Thrusters template, which brings the Atmospheric class, its flame effect and its G-Screen place; thrust, power and flame length are a starting point to match to a vanilla size, and it has no sound and no spinning fan.
  - ThrustDirection: Forward, Backward, Left, Right, Up or Down (default Forward), in block space. It must fit the model: for a model exported facing another way, change ThrustDirection or have the user rotate the model. A block thrusts in this one direction only; thrust in several directions takes one thruster block per direction.
- Thruster-type dummies: the nozzles (the dummies page).
- ThrusterEffectsComponent: on the client, draws the flame at the Thruster-type dummies. The flame is in its ThrusterEffectsComponentDefinition; the sound is OnUseAudio on the block's BlockEffectsComponentDefinition and plays while the thruster is working, firing or not (the ThrusterEffectsComponentDefinition has no sound). Both stay as they are unless the user asks.
  - The ThrusterEffectsComponentDefinition holds the flame's size (FlameLength, FlameRandomScale) and which particle effects play (OnThrustPoweredParticleEffect, OnLandingParticleEffect); it has no colour of its own (the particle-effects page).
    - FlameLength scales the whole flame; the Thruster dummy's scale does not. The dummy's position and rotation place the flame: a flame offset from the nozzle or showing through the block's side is fixed by moving the dummy (compare with the vanilla thruster's dummy).
    - A custom flame for one thruster: a new effect referenced from its OnThrustPoweredParticleEffect, with IsParameterDriven set (the particle-effects page).
  - In vanilla, every size of a class plays the same flame effect (one for all Ion, one for all Atmospheric, one for all Hydrogen); only the landing effects differ by size. Each size still has its own ThrusterEffectsComponentDefinition, so the flame's length differs per size. Changing that effect changes the flame of every thruster of the class.
  - No flame on a new thruster: its ThrusterEffectsComponentDefinition plays another class's effect (point it at a copy of that class's vanilla one), or the model has no Thruster-type dummy to draw it at.

Power: a thruster draws power only through a PowerableBlockComponent, the block component of thruster templates, whose PowerableBlockDefinition has the class's ConsumedResource. That ConsumedResource names the resource; its Amount is a base draw, 0 on vanilla thrusters, and ResourcesRequiredToThrust comes on top of it while firing. A block whose block component is a FunctionalBlockComponent (most other functions) thrusts without drawing any power, and its flame (ThrusterEffectsComponent, which looks up a PowerableBlockComponent) makes it fail to load. component_upgrade(block, "FunctionalBlockComponent", <a vanilla thruster of the class>) makes it a PowerableBlockComponent: the block keeps its settings (name, kind, mount points, recipe) and gets the thruster's ConsumedResource.
