---
name: subpart-animation
description: Use when a part of a block should move, such as a ring or barrel that spins, a piece that slides back and forth, or a lid that moves to a position and back, always or only while the block works, shoots or is in another state; the part must already be a subpart (block-subpart).
---
Needs:
- the part: its subpart prefab
- how it moves: spin, slide back and forth, or go to a position and back; around or along which axis, how fast, how far
- when it moves: always, or while the block is in a state the user names (works, shoots, is armed, ...)

Plan:
- Read the part and the block
- Choose the motion and learn its definition
- Get from the user the motion's values and when it moves
- Add the motion to the part with the user's values
- Choose what drives the motion and add it to the block
- Show the block

Steps:
1. read(part) and read(block).
2. Motion: describe("RigidAnimationComponent") lists the motions a part can have. Pick the one that matches how the user wants the part to move (ask when their wish does not say), then describe its definition type for the fields and their units. A part has one motion; a second motion needs a second part.
3. Ask the user, with ask_user, the definition's values (axis, how fast, how far, how quickly it starts and stops) and when it moves. Offer values read from one vanilla part that moves the same way (find_content(<the definition type>)), in the definition's units.
4. create_definition(<the definition type>, ...), write the user's values, component_add(part, <the motion component>, <it>). A speed-up of 0 never starts the motion.
5. Driver: describe("IRigidAnimationControllerComponent") lists the components that switch the motions of a block's parts on and off, e.g. FunctionalBlockAnimationControllerComponent while the block works. Pick the one that matches when the part moves; describe it when unsure what it reacts to. component_add(block, <it>), with a definition when it takes one. One driver per block, and it drives all its parts. With no driver, a spin or slide runs always and a move to a position and back never happens.
   Speed following a thruster's thrust or a wind turbine's wind, on a spinning part: also component_add(block, "ThrusterAnimationOverrideComponent") or "WindTurbineAnimationOverrideComponent" (no definition), and write the spin definition's VariableAngularInterpolatedSpeed, the speed at full; AngularSpeed is then the speed at none.

Show:
show(the block). The motion is seen in game, once its driver switches it on.
