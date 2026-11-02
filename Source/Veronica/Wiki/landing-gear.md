---
name: landing-gear
title: Landing gear: the lock box, the release box, how locking and auto-lock work
source: SE2 source MagneticBlockComponent, MagneticBlockDetectionComponent, DummyShapedEntityDetectorComponent, ShapeEntityDetectorComponent, DetectionBasedCooldownComponent, ModelComponentDefinition, DetectionArgs, PowerableBlockDefinition, PlacementDefinition, MagneticBlockIndicatorComponent; vanilla landing gears (Assets/Blocks/LandingGear/LandingGear), the Landing Gear template
description: How a landing gear block locks a ship to a surface: its components, the LockArea and CooldownRange dummies and what their position, rotation and scale mean, which face is placed against the surface, the flatness limit, auto-lock and its release wait, power, what the template brings, and the parts that are only looks.
---
Parts:
- MagneticBlockComponent: locks and unlocks, joining the block's grid to the target with a fixed physics constraint (the SharedMotionPhysicsConstraintComponent). Its MagneticBlockDefinition holds DetectionArgs, how often (Frequency, in ticks) and how the target is searched.
- MagneticBlockDetectionComponent (server): picks the lockable target from what the lock detector finds. Its MagneticBlockDetectionDefinition's MaxAlignmentAngle, in degrees, is how far the surface may tilt from the lock box's forward axis: 0 needs a perfectly flat contact, 90 accepts any surface.
- Two DummyShapedEntityDetectorComponents, each with a DummyShapedEntityDetectorDefinition naming its QueryDummy type:
  - the lock detector (tag LockingEntityDetectorProvider, server): LockArea;
  - the release detector (tag DetectionBasedCooldownDetector): CooldownRange, used by the DetectionBasedCooldownComponent.
- PowerableBlockComponent as the block component.

The two boxes:
- A detector measures a box placed by its dummy: the dummy's position and rotation place the box, its Scale is the box's full size in metres.
- The lock box (LockArea dummy) sits just outside the pad. The surface's tilt is measured against the box's forward axis (the dummy's Z, either direction), so the box's thin side faces away from the pad. Vanilla turns the dummy so its forward points out of the pad.
- The face players place against a surface is the PlacementDefinition's AutoRotateFace; without one the placer picks a face from the mount points. The lock box has to sit outside that face.
- The release box (CooldownRange dummy) is larger and around the lock box.
- A block with either dummy missing fails when it is built: its detector looks the dummy up and gets nothing.

Locking:
- A target inside the lock box and flat enough is ready to lock; players lock and unlock it, also with the cockpit's parking key.
- Auto-lock locks to a ready target by itself. It is each built block's own setting, switched by players in the terminal; a mod does not set it on the block.
- After an unlock, auto-lock waits until nothing but the block's own grid is inside the release box, so the gear does not lock straight back to the surface it left.
- Locking and unlocking need the block to work: built, on and powered. Its PowerableBlockDefinition's ConsumedResource Amount is 0 in the template and vanilla: it draws nothing but needs power on the grid.

Vanilla landing gears: Assets/Blocks/LandingGear/LandingGear/<size> (100, 500).

The Landing Gear template: a block made from it gets its own copies of the lock detector, magnetic and alignment definitions and its PowerableBlockDefinition, in the vanilla landing gears' G-Screen family; the release detector and the cooldown keep the template's shared definitions, which hold no values a block varies. Its dummies list is empty: no lock box and no release box until they are added.

Looks only, not needed to lock: the lock state indicator (MagneticBlockIndicatorDefinition swaps IndicatorMeshParts materials per lock state; copy a vanilla gear's), the effects, the damage particles (on a Particles dummy in vanilla) and the construction particles.
