---
name: subpart-spin
description: Use when a part of a block should spin continuously, such as a ring or fan, while the block works or faster with a thruster's thrust; the part must already be a subpart (block-subpart).
---
Needs:
- the part: its subpart prefab
- the axis it spins around, and how fast: the user's own, or as a vanilla part (e.g. a gyroscope's rings, Assets/Blocks/Gyroscopes; a thruster's fan, Assets/Blocks/Thrusters/Atmospheric/<size>/Subparts)
- what drives it: spinning while the block works, or speed following a thruster's thrust

Plan:
- Read the part and the block
- Get from the user the axis, the speed and what drives it
- Add the spin to the part
- Add what starts it on the block
- Show the block

Steps:
1. read(part) and read(block).
2. Ask the user, with ask_user, the axis, the speed, how quickly it spins up and slows down, and what drives it; offer a vanilla part's values as a suggestion.
3. create_definition("InfiniteRotationAnimationDefinition", ...) and write RotationAxis (a unit vector in the part's own axes), AngularSpeed (degrees per second), AngularSpeedup and AngularSlowdown (degrees per second, each second). With AngularSpeedup 0 the part never starts turning. component_add(part, "InfiniteRotationAnimationComponent", <it>).
4. On the block: spinning while it works: component_add(block, "FunctionalBlockAnimationControllerComponent"). Speed following thrust, on a thruster: component_add(block, "ThrusterAnimationOverrideComponent") (it takes no definition) and write the definition's VariableAngularInterpolatedSpeed, the speed at full thrust; AngularSpeed is then the speed with no thrust.

Show:
show(the block). Spinning is seen in game, on a working block.
