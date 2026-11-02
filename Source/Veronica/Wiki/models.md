---
name: models
title: Block models: model assets, LOD files, colliders, transformations, grid alignment, the size limit, the block's model fields
source: SE2 source BlockModelComponentDefinition, BlockRenderComponent, ArmorBlockComponent, the model asset (GeometryAssets, Transformations, MaterialAssignments, Collision Shape, SuggestedModelSize) and its import and BlockModel validation; vanilla block models under Assets/Blocks
description: How a block's model is made from imported files: the .model asset with its LOD FBXs and glTF collider, UV and material rules, the per-LOD transformations, aligning a model to the 25 cm grid, the size limit and the import warnings that point at a wrong scale, the block's model fields and their shared origin, and what draws a non-armor block.
---
The model asset:
- A block's model fields take a model asset (.model), which holds the model with its collider and LODs. A raw exported file in a model field is refused (invalid asset extension). Armor blocks use a .comparm instead (the armor-blocks page).
- The model asset is generated in the Editor from FBX files, one per LOD, LOD0 named <name>.fbx (a name without the _LOD suffix counts as LOD0), the others <name>_LOD<n>.fbx; LOD0 is required. Importing is the user's job: files only copied into the mod folder have no model asset until the Editor generates one.
- The collider is a separate .gltf file (the model asset's Collision Shape) with its .bin next to it; in Blender collider objects carry a Rigid Body.
- The .gltf names its .bin by a path used as written: a space in the export name becomes %20 and fails with 'Missing file for GLTF Model'. Export without spaces, or make the buffer uri in the .gltf match the .bin name; renaming the files afterwards does not help.
- A .model asset opens on its own in the Editor's viewport, to check an import before it is in a block.

Meshes and materials:
- The importer triangulates every polygon; triangulating in Blender only controls how faces are split.
- Every mesh needs a UV map: without one the import fails with 'TexCoords data for mesh <name> is null'.
- Materials are assigned per model asset (MaterialAssignments), by the FBX material name. One mesh in two looks needs two model assets; blocks with the same look can share one.

Transformations:
- Each LOD entry in the model asset's GeometryAssets has Transformations: Translation, Rotation (degrees, X then Y then Z) and Scale.
- LOD0's Transformations apply to every LOD, after the LOD's own: a model is moved, turned or scaled by changing LOD0 only.
- The deformed and fracture models need the same LOD0 transformation as the standard model; validation notes when their bounding boxes differ.

Grid alignment:
- The origin is a cell centre. A model centred on the origin with an even number of cells along an axis sits half a cell off and needs a 0.125 m shift on that axis. The model asset's 'Snap model to SE2 grid' action writes this into LOD0 Transformations.Translation; setting that Translation directly does the same.

Size limit and import warnings:
- The collider bounds may hold at most 8000 m3, whatever the shape inside; past it validation reports 'BlockModel bounds are too large'. Stray collider parts count.
- The material count and LOD count limits ('Model of size <n>m has too many materials', 'should have exactly <n> LODs') depend on the model's measured size. A size far off the intended one means a wrong export scale (unit scale or unapplied transforms), fixed by re-exporting.
- SuggestedModelSize, when set, replaces the measured size in these limits (the model-lods page); 0,0,0 breaks them.

The block's model fields (BlockModelComponentDefinition):
- Model, DamagedModel (Editor label Deformed Model), FractureModel and BuildProgressModels all take model assets; how they break and build stages are on the breakable-blocks page.
- A stage, deformed or fracture model replaces the main model with no offset of its own, so it needs the same origin as the main model in its source file; one with another origin jumps aside in the preview.

Drawing the block:
- A non-armor block is drawn by a BlockRenderComponent on the client; without it the block shows while placing and is invisible once placed. Armor blocks have an ArmorBlockComponent instead and are drawn by the grid.
