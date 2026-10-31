---
name: thrusters
title: Thrusters: classes, power, flame and sound
source: SE2 source ThrusterComponent, ThrusterDefinition, ThrusterEffectsComponentDefinition, PowerableBlockComponent; vanilla thrusters (Assets/Blocks/Thrusters); resource types Electricity.def and Hydrogen.def
description: How a thruster works: its classes and what they draw, why a thruster block draws power or not, and where its force, flame and sound are set.
---
Classes: Ion and Atmospheric draw Electricity, Hydrogen draws Hydrogen. Vanilla ones are in Assets/Blocks/Thrusters/<class>/<size in cm>.

Parts:
- ThrusterComponent with its ThrusterDefinition: the thrust. The definition's fields set how the thruster behaves; each should fit what the user wants: as the matched vanilla thruster, changed as the user asks, or their own value.
  - ThrustPower: the most force the thruster pushes with, in N. Vanilla Ion 100: 8950.306.
  - ResourcesRequiredToThrust: what the thruster draws while it fires, in the class's resource unit: kW of Electricity, L/s of Hydrogen. It is all or nothing: the full amount while firing at any strength, none otherwise. Vanilla Ion 100: 40 kW.
- Thruster-type dummies: the nozzles (the dummies page).
- ThrusterEffectsComponent: on the client, draws the flame at the Thruster-type dummies. The flame is in its ThrusterEffectsComponentDefinition; the sound is in the block's BlockEffectsComponentDefinition. Both stay as they are unless the user asks.
  - The ThrusterEffectsComponentDefinition holds the flame's size (FlameLength, FlameRandomScale) and which particle effects play (OnThrustPoweredParticleEffect, OnLandingParticleEffect); it has no colour of its own (the particle-effects page).
  - In vanilla, every size of a class plays the same flame effect (one for all Ion, one for all Atmospheric, one for all Hydrogen); only the landing effects differ by size. Each size still has its own ThrusterEffectsComponentDefinition, so the flame's length differs per size. Changing that effect changes the flame of every thruster of the class.

Power: a thruster draws power only through a PowerableBlockComponent, the block component of thruster templates, whose PowerableBlockDefinition has the class's ConsumedResource. That ConsumedResource names the resource; its Amount is a base draw, 0 on vanilla thrusters, and ResourcesRequiredToThrust comes on top of it while firing. A block whose block component is a FunctionalBlockComponent (most other functions) thrusts without drawing any power. Making it draw power means replacing that block component and moving its settings (name, kind, mount points, recipe) into a PowerableBlockDefinition, which the tools do not do; the user should be told.
