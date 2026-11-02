---
name: mount-points
title: Mount points: where other blocks attach, how the game generates them, setting them by hand
source: SE2 source CubeBlockDefinition (MountPointGeneratorSettings, MountPointsGroupsPerDirection), MountPointsGroupData, BlockMountPointsGenerator, GeneratedBlockDataExtractor.MountPoints, RaycastMountPointSampler, the model asset's collider loading (mountpoints and occupancy collider prefixes), ArmorSide asset; vanilla AtlasDecal materials
description: What mount points are and where a block keeps them, how the game generates them from the model and its collision (the half-cell rule), why a side gets none or the block will not place (red preview) and the model fixes for it, mountpoints and occupancy colliders, and turning generation off to set MountPointsGroupsPerDirection by hand.
---
Mount points are the faces of a block's 25 cm cells where other blocks attach.
- A side without them takes no neighbour, and the block cannot be placed against a surface with that side: the placement preview stays red.
- They live on the block's own *BlockDefinition (PowerableBlockDefinition or another CubeBlockDefinition type): MountPointGeneratorSettings and MountPointsGroupsPerDirection.

Generated (MountPointGeneratorSettings.Enabled true, the default):
- The game generates them from the model; the result replaces MountPointsGroupsPerDirection at runtime. Nothing has to be authored in Blender or in the definition.
- Occupied cells come from the model's collision. Mount points are picked only on the outer faces of those cells.
- A cell face becomes a mount point when at least 4 of 8 sample rays in a ring around its centre hit the model's render mesh lying flat on the face plane: within 2 cm of it, normal within 5 degrees. So roughly half of each 25 cm cell face must be covered by flat geometry on the block boundary.
- The rule is per cell face, not per whole side. Slanted surfaces, faces set back more than 2 cm, and parts smaller than about half a cell get none.
- Directions limits the sides that get mount points (default All). Sampler is read-only.
- Collider meshes named with the prefix mountpoints (any case, e.g. mountpoints_top) are extra geometry for this sampling only. They are not physical, so a real collider must never be named so. Colliders prefixed occupancy add occupied cells only.
- Armor blocks get collision and mount points generated from their .armorside meshes instead.

Why a side has none (the fixes are in the model, the user's work; Veronica cannot see or change geometry):
- The geometry does not reach the cell face or is not flat on it. Faces must be flush with the block boundary, the model aligned to the 25 cm grid at its origin.
- The collision is smaller than or offset from the intended block: the occupied cells, and so the boundary, come from it. A collision larger than meant adds a cell.
- Too little contact (thin letters, rails, small details): add a thin flat piece on that face. Either a mountpoints collider (exported only if it has a rigid body, with the other collisions), or a render mesh with a fully transparent material (e.g. a vanilla AtlasDecal material UV-mapped to an empty part of the atlas; it costs some rendering).
- Or set the mount points by hand (below).

By hand:
- Write MountPointGeneratorSettings.Enabled false, then fill MountPointsGroupsPerDirection. Its keys are Forward, Backward, Left, Right, Up, Down; each holds a list of MountPointsGroupData.
- GroupBoundingBox: Min and Max in the block's 25 cm cells, both inclusive (Min = Max is one cell). PrimaryMountPointBox and Fracture are optional.
- Groups on a side are used in list order.
- Which cells a side should span depends on the model: the user's call. Show the block so the user can check.
