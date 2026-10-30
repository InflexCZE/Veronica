---
name: thrusters
title: Thrusters: classes, power, flame and sound
source: SE2 source ThrusterComponent, PowerableBlockComponent; vanilla thrusters (Assets/Blocks/Thrusters)
description: How a thruster works: its classes and what they draw, why a thruster block draws power or not, and where its force, flame and sound are set.
---
Classes: Ion and Atmospheric draw Electricity, Hydrogen draws Hydrogen. Vanilla ones are in Assets/Blocks/Thrusters/<class>/<size in cm>.

Parts:
- ThrusterComponent with its ThrusterDefinition: the thrust. The definition's fields set how the thruster behaves; each should fit what the user wants: as the matched vanilla thruster, changed as the user asks, or their own value.
- Thruster-type dummies: the nozzles (the dummies page).
- ThrusterEffectsComponent: on the client, draws the flame at the Thruster-type dummies. The flame is in its ThrusterEffectsComponentDefinition; the sound is in the block's BlockEffectsComponentDefinition. Both stay as they are unless the user asks.

Power: a thruster draws power only through a PowerableBlockComponent, the block component of thruster templates, whose PowerableBlockDefinition has the class's ConsumedResource. A block whose block component is a FunctionalBlockComponent (most other functions) thrusts without drawing any power. Making it draw power means replacing that block component and moving its settings (name, kind, mount points, recipe) into a PowerableBlockDefinition, which the tools do not do; the user should be told.
