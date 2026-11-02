---
name: placement
title: Placement: the grid cell, snapping, mirroring and orientation
source: SE2 source PlacementDefinition, PlacementDefinition.BlockSymmetriesDefinition, PlacementDefinitionValidator, CubeBlockDefinition (PlacerDefinition), BlockPlacerEntityComponentDefinition (SnapSizeVariants, DefaultPlacementDefinition, GetBestSnapSizeIndex), BlockPlacerConfiguration (SizeGroupDefinition, DefaultBlockSymmetry); vanilla Assets/System/BlockPlacer/BlockPlacer.def, Antenna_PlacementDefinition (Assets/Blocks/Antennas), WedgeBlockSymmetries.def and CornerBlockSymmetries.def (Assets/Blocks)
description: How blocks sit on the grid and are placed: the one 0.25 m cell grid, the optional PlacementDefinition and its snapping fields (snap size, mount point alignment, auto-rotate face, snap alignment, size group), how symmetry mode turns a mirrored block through a BlockSymmetriesDefinition, why a left/right variant needs a block of its own, and where a block placed upside down is fixed.
---
Grid: there is one grid with a 0.25 m cell; there are no separate small and large grids. Block sizes are whole cells.

The block definition's PlacerDefinition points at a PlacementDefinition (its own file, e.g. Antenna_PlacementDefinition in vanilla) that changes how the block is placed.
- It is optional: without one, the snap size and the face placed against the target are picked automatically from the block's size and mount points. Add one, or copy another size's, only when the automatic placement is wrong.
- A PlacementDefinition may set just one field; the rest keep their defaults.

Snapping fields (PlacementDefinition):
- BestSnapSize: the snap size in cells, not cm. Empty: the block's smallest bounding box side in cells. The placer only snaps to its SnapSizeVariants (Assets/System/BlockPlacer/BlockPlacer.def); a value not in the list uses the next larger variant.
- AlignWithMountPoints (true by default): snap by the mount points; false snaps by the block's bounding box.
- AllowSquareSnapping (true by default): on square faces the block keeps its position when rotated.
- AutoRotateFace: the face placed against the target block, e.g. the back of a thruster or an interior light. Empty: picked from the mount points.
- SnapAlignment: which edges the block snaps towards instead of the centre, e.g. a window to the outer edge. Two opposite directions are a validation error.
- SizeGroup: overrides the block's size group (a named block size in the placer's BlockPlacerConfiguration), for specially shaped blocks.
- AlignWithMountPoints and SnapAlignment also move where the hologram sits.

Mirroring (symmetry mode):
- PlacementDefinition.OverrideSymmetries points at a BlockSymmetriesDefinition. Its XAxis, YAxis and ZAxis are the rotation applied when the block is mirrored across the X, Y or Z plane, as Vector3I turns of 90 degrees (0, 1, -1, 2; 2 = 180 degrees).
- With none the block mirrors like a cube. A block that turns the wrong way in symmetry mode needs one.
- Vanilla WedgeBlockSymmetries.def and CornerBlockSymmetries.def (Assets/Blocks) fit blocks shaped like a wedge or a corner; find_content("BlockSymmetriesDefinition") lists the others.
- Symmetry only turns the same block; it never swaps in a mirrored counterpart. A left/right pair (a part on one side only) needs a separately modelled mirrored block, which players place themselves.

Orientation: the model definitions (ModelDefinition, BlockModelDefinition, BlockModelComponentDefinition) have no rotation field. A block placed upside down is fixed in its model file by re-orienting the mesh; its dummies must be turned to match.
