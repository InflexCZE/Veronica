---
name: armor-blocks
title: Armor blocks: the composite model, armor sides, size rules, skins and processing errors
source: SE2 source ArmorBlockDefinition, CompositeArmorBlockModel, ArmorBlockSide, ArmorBlockSideGenerator, ArmorProcessingConstants, Triangulation2D, ArmorSkinDefinition, CubeRotation; vanilla Assets/Armors (Shared sides, .comparm files per shape and size), Assets/ArmorSkins
description: How an armor block is built: its ArmorBlockDefinition's Model (a .comparm) and Skin, armor sides (.armorside) and how they are placed and rotated, the size limit and centred origin, why all sides must be the same size, physics generation, watertightness, and what the armor processing errors mean.
---
Parts:
- ArmorBlockDefinition. Its Model is a composite armor model (.comparm, CompositeArmorBlockModel), not a .model: a .model in that field is refused with 'Invalid Asset Extension'. Its Skin (an ArmorSkinDefinition) is the look; BuildProgressSkins lists the skins per build stage.
- The material of an armor block is in its skins, not on a model asset.
- A full 3D model cannot be an armor block; a shape that is not built from flat sides is made as an ordinary block.

The composite model (.comparm):
- SideMeshes: one entry per side, each with SideMesh (an .armorside) and Rotation (a CubeRotation: per axis X, Y, Z one of Rotate0, Rotate90, Rotate180, Rotate270, so 90 degree steps only). Vanilla entries also carry PlanarOffset.
- One .armorside can be reused on several faces with different rotations.
- No mirror setting exists: a mirrored side is its own .armorside made from a mirrored mesh.
- An entry with no SideMesh is skipped without an error.
- SkipPhysicsShapeGeneration: while on, the block processes but has no physics shapes and cannot be used in game ('Physics shapes for Armor Block were not generated ... Uncheck SkipPhysicsShapeGeneration option'). It must be off in the finished block.
- Vanilla .comparm files are in Assets/Armors/<shape>/<size>.

Armor sides (.armorside):
- One FBX per side, each with its own .armorside. A whole block mesh used as one side does not work; the .comparm puts the sides together.
- Properties: Model Asset (the FBX), Translation, Rotation, Scale, Material Variant, UV Locking (LockedUVs). Offset and scale of a side are set here, not in the .comparm entry.
- Texture mapping is procedural and made for flat panels; UV Locking (default None) turns it off along U and/or V.
- Vanilla sides to reuse are in Assets/Armors/Shared (e.g. ArmorPlate_Plane_250cm).

Size rules:
- A side's size is measured from the model origin: twice its largest coordinate, in 0.25 m cells, rounded up to a power of two. It must stay under 64 cells, so the mesh must fit within 4 m of the origin in every direction (8 m across) and the origin belongs at the centre, not a corner. Too large gives 'Too large model inserted.'; a mesh exported in centimetres is 100 times too large.
- All sides of one .comparm must come out at the same size in cells, so every side's furthest point must reach the same distance from the block centre at the same scale; sides of different block sizes go into separate armor blocks.
- Otherwise processing fails with 'has side of different sizes, expected N, found M' (or 'Sides of block do not have same size'). N and M are cell sizes; the message does not name the side.

Other processing errors:
- 'Armor Block is not watertight ... Avoid edges smaller then 1mm': the sides merged together must close the block. It fails while any face is missing or a side is turned wrong, and with edges under 1 mm.
- 'Could not triangulate polygon. The polygon is probably self-intersecting.': a side mesh's outline, cut at the block face, is self-intersecting or open (overlapping faces, open or non-manifold edges). The side mesh needs clean, closed topology.

Skins:
- The look is the ArmorBlockDefinition's Skin. A new look is a new ArmorSkinDefinition (DefaultMaterial, SideMaterials) set as Skin; no new block type is needed.
- Vanilla skins are in Assets/ArmorSkins (the light one is spelled LigthArmorSkin).
