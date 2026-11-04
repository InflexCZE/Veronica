---
name: doors
title: Doors: the frame, the leaf subparts, opening, the doorway detector, the use spots and collision
source: SE2 source DoorServerComponent, DoorServerDefinition, DoorSegmentTransformSLERPDefinition, ConditionalSubPartsDefinition, ConditionalSubPartsDoorPartsProviderDefinition, DoorBlockColliderComponent, DoorBlockColliderDefinition, BoxShapeEntityDetectorComponent, BoxShapeEntityDetectorDefinition, UseToggleDoorAction, BlockMaterialSwapperDefinition, PowerableBlockDefinition; vanilla SlideDoorNarrow (Assets/Blocks/Doors/SlideDoorNarrow/250), UseActionBindings.def, UseToggleDoorDefinition.def, the UseDoor and DoorwayObstacleDetectorDummy dummy types
description: How a door block is built and works: the frame model and its dummies, the door leaves as subpart prefabs and how a leaf moves between its closed and open spots, the opening time, the doorway obstacle box that stops it closing, the UseDoor spots players open it from, why an open door can or cannot be walked through (frame and leaf colliders), and what its glow follows.
---
Parts:
- A door block is a base model (the frame, the block's ModelDefinition) plus door leaves; each leaf is a subpart prefab, bound like a block (a binding with a client and a server prefab), unlike a spinning part made from the single-prefab Subpart template. Vanilla example: Assets/Blocks/Doors/SlideDoorNarrow/250; its files show every part a door needs.
- The frame shows its leaves through ConditionalSubParts: a ConditionalSubPartsDefinition on the client whose SubParts map a closed-position dummy type on the frame's model to a leaf prefab.
- A ConditionalSubPartsDoorPartsProviderDefinition points at that ConditionalSubPartsDefinition; DoorBlockColliderDefinition.SubPartsProvider points at the provider, its ModelDefinition at the frame's model.
- The frame also carries DoorServerComponent, the doorway detector (below), DoorBlockColliderComponent and a PowerableBlockDefinition.

Leaves:
- Each leaf prefab has Client and Server compositions, each with a ModelComponentDefinition (the leaf model) and a DoorSegmentTransformSLERPDefinition.
- DoorSegmentTransformSLERPDefinition: ClosedPositionDummy and OpenPositionDummy name dummy types on the frame's model; the leaf moves smoothly between the two spots as the door opens. Vanilla gives each spot a dummy type of its own, in the door's folder (LeftDoorClosedDummy and so on). Doors do not use TranslationAnimation.
- Leaves are not hidden or scaled when open: to clear the doorway, put the open spot where the leaf is out of the way (slid or turned into the frame).

Opening:
- DoorServerDefinition.OpeningDuration: how long the door takes to open, in seconds (default 1; vanilla 0.5).
- Players open and close the door from dummies of Type UseDoor in the frame's BlockModelComponentDefinition, bound to the toggle-door use action (UseToggleDoor). Vanilla sliding doors have one on each side (TerminalFront01, TerminalBack01). Their position and Scale make the spot; no definition of the block's own and no subpart is needed. A UseDoor dummy on a leaf subpart works too; pointing at it highlights all leaves.
- The spot's glow is the dummy's HighlightSection, a mesh section of the exported model; without it the prompt still works.

Doorway detector:
- DoorServerComponent needs a component named DoorwayEntityDetector: in vanilla a BoxShapeEntityDetectorComponent with a BoxShapeEntityDetectorDefinition (<door>_DoorwayObstacleEntityDetectorDefinition). While anything is inside its box, the door does not close. Without it: Unsatisfied dependency 'DoorwayEntityDetector' on DoorServerComponent; copies of older vanilla doors lack it, so compare with the current vanilla door.
- The box's size is QueryHalfExtents: half the box's size per axis, in metres, each above 0. A box reaching into the frame or the neighbouring blocks keeps the door open; keep it to the inside of the doorway. Read the vanilla door's detector definition for a starting value.
- Vanilla doors also carry a DoorwayObstructionDetector dummy (type DoorwayObstacleDetectorDummy); the box's size comes from QueryHalfExtents, not that dummy's Scale.

Collision (DoorBlockColliderComponent switches the block's collider with the door's state):
- Open (or not working): the frame's collider only. It needs an opening where players walk through, made of several colliders around the passage; a frame collider without one blocks the open door.
- Closed: the frame's collider plus each leaf model's collider, placed at its closed-position dummy (looked up in DoorBlockColliderDefinition's ModelDefinition first). It does not follow the leaves while they move. A leaf model without a collider leaves the closed doorway walkable.

Looks: the vanilla door's glow is a BlockMaterialSwapperDefinition, which follows only whether the block works, not whether the door is open or closed.
