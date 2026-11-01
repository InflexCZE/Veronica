---
name: gyroscopes
title: Gyroscopes: torque, power and what turns the ship
source: SE2 source GyroscopeComponent, GyroscopeDefinition, GridMovementCollectorComponent, GridGyroscopesComponent, PowerableBlockDefinition, the Torque unit; vanilla gyroscopes (Assets/Blocks/Gyroscopes/Gyroscopes), the Gyroscopes template
description: How a gyroscope block turns a ship: its torque and unit, how the ship's gyroscopes add up, power, what happens with 0 torque, and the parts that are only looks.
---
Parts:
- GyroscopeComponent, on the server only, with its GyroscopeDefinition. The definition has one field: MaxTorque, the most turning force the block adds to its ship, in N*m.
- PowerableBlockComponent as the block component. Its PowerableBlockDefinition's ConsumedResource is Electricity; its Amount is the kW the gyroscope draws while it is on.
- No dummy is needed for the gyroscope to work.

How it turns the ship: the ship adds up the MaxTorque of all its working gyroscopes and turns with that. A heavier, larger ship turns slower with the same torque, so the vanilla sizes differ by orders of magnitude. A gyroscope that is off or has no power adds nothing. With 0 torque the block builds and draws power but never turns the ship, and nothing reports an error.

Vanilla gyroscopes: Assets/Blocks/Gyroscopes/Gyroscopes/<size in cm> (50, 250, 750), each with its own GyroscopeDefinition and PowerableBlockDefinition.

The Gyroscopes template: a block made from it gets its own GyroscopeDefinition with MaxTorque 0 and its own PowerableBlockDefinition with the 50 cm gyroscope's power draw, in the vanilla gyroscopes' G-Screen family.

Looks only, not needed to turn: the spinning parts of vanilla gyroscopes (a ConditionalSubPartsComponent with subpart models and a RotatingSubparts dummy), the animation controller, the material swapper, and the damage particles and humming sound of the BlockEffectsComponentDefinition.
