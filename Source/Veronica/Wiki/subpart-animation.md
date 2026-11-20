---
name: subpart-animation
title: Subpart animation: the motions a part can make, what switches them on and off, speed that follows thrust or wind
source: SE2 source RigidAnimationComponent, ToggleRigidAnimationComponent, InfiniteRotationAnimationComponent/Definition, InfiniteTranslationAnimationComponent/Definition, RotationAnimationComponent/Definition, TranslationAnimationComponent/Definition, RigidAnimationsValidator, RigidAnimationEntityExtensions, IRigidAnimationControllerComponent, FunctionalBlockAnimationControllerComponent, ToolStatusBasedAnimationControllerComponent, ToolAnimatorComponent/Definition, FixedProjectileWeaponAnimatorComponent, ExplosiveBlockEffectsComponent/Definition, ThrusterAnimationOverrideComponent, WindTurbineAnimationOverrideComponent, IVariableAnimation, DoorSegmentTransformSLERPComponent; vanilla gyroscopes, Assembler500, Refinery750, warheads, GatlingCannon200, drills, atmospheric thrusters, hand drill/grinder
description: How a block's subparts move: the four motions (endless spin, endless back-and-forth slide, turn to an angle and back, move to an offset and back) and their fields and units, one motion per part, the controller components that switch motions on and off (block working, tool active, gun firing, warhead armed) and what happens without one, speed that follows thrust or wind, why a part never starts or never stops, speed limits, vanilla examples of each.
---
Motions are visual only: a component and its definition on the subpart prefab, moving the part relative to its spot. They do not move collision.

The motions:
- InfiniteRotationAnimation: spins forever. RotationAxis (in the part's own axes), AngularSpeed (degrees per second), AngularSpeedup and AngularSlowdown (degrees per second gained or lost each second). Vanilla: gyroscope rings, fans of atmospheric thrusters, drill heads, the gatling barrel.
- InfiniteTranslationAnimation: slides back and forth forever along one line. Velocity: its direction is the line, its length the top speed in m/s. Distance: the stroke length in m. Speedup and Slowdown: m/s gained or lost each second. Vanilla: the Assembler500 and Refinery750 side devices.
- RotationAnimation: when switched on, turns by RotationAngle (degrees) about RotationAxis (a unit vector) at AngularSpeed (degrees per second); when switched off, turns back. No vanilla block uses it.
- TranslationAnimation: when switched on, moves by Translation (a vector in m) at Speed (m/s); when switched off, moves back. Vanilla: the warhead lids.
- GenericAnimation is driven only by game code; content cannot use it.

Rules:
- One motion per part. A part that should spin and slide needs a part inside a part: the outer part slides, the inner one spins.
- Never on the block itself: validation fails with "Rigid animation components are not allowed on root".
- A part spins about its own origin, so centre the part's model on the point it turns about.

Switching on and off:
- A controller is a component on the block. It switches every motion on every part below the block, at any depth. Nothing else links them: no dummy, tag or definition.
- Without a controller, the two endless motions run all the time. The turn-and-back and move-and-back motions never move, since nothing switches them on.
- With a controller, it decides. Which controller fits is the question of when the part should move:
  - FunctionalBlockAnimationControllerComponent: while the block works (built, on, powered). No definition. Vanilla: gyroscopes, assembler, refinery, reactor, gravity generator, atmospheric thrusters.
  - FixedProjectileWeaponAnimatorComponent: while a fixed gun shoots. No definition. Vanilla: GatlingCannon200 (its barrel).
  - ExplosiveBlockEffectsComponent: per warhead state; its ExplosiveBlockEffectsDefinition's Armed and Disarmed each say PlayRigidAnimations. Vanilla: warheads.
  - ToolAnimatorComponent (vanilla files name it DrillAnimatorComponent): while a ship tool works (drill, grinder). Its ToolAnimatorDefinition names an AnimationDummy, a dummy type the block's model must hold, or validation fails.
  - ToolStatusBasedAnimationControllerComponent: while a hand tool is active. Vanilla: hand drill, hand grinder, paint gun.
- Every controller implements IRigidAnimationControllerComponent; describing that interface lists them.

When a part never starts or never stops:
- The endless motions start from rest and gain speed by their speedup each second, so a speedup of 0 never moves the part, with no error. A slowdown of 0 never slows it, so it keeps moving after it is switched off.
- The endless motions start at a random angle or a random point of the stroke, so parts of one kind are not in step.

Speed that follows thrust or wind:
- ThrusterAnimationOverrideComponent (on a thruster, by thrust in its direction) and WindTurbineAnimationOverrideComponent (on a wind turbine, by its efficiency); no definition. They change only the speed of InfiniteRotation parts: from AngularSpeed at 0 to VariableAngularInterpolatedSpeed at full. With VariableAngularInterpolatedSpeed left empty the speed never changes.
- They are not controllers: they do not stop a part. Vanilla atmospheric thrusters carry FunctionalBlockAnimationControllerComponent as well, so the fan stops when the thruster does not work.

Limits (validation): speed up to 1000 m/s, angular speed up to 3600 degrees per second, angular speedup or slowdown up to 7200 degrees per second each second.

Door leaves are not moved by these motions: a door leaf blends between two dummies of the frame.
