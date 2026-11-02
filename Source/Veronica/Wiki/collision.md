---
name: collision
title: Collision: a block's colliders, the collision gltf, limits and what colliders decide
source: SE2 source BlockModelComponentDefinition and its BlockModel validation (missing collider, collider count, collider vertices, bounds), the model asset (CollisionShapesAsset) and its GLTF collider loading, mount point generation from colliders, the paint tool's ray cast; the VRAGE Blender add-on's collider export and convex hull generator; vanilla block models under Assets/Blocks (.gltf and .hkt collision shapes)
description: Where a block's collision comes from (the model asset's CollisionShapesAsset), why a collision gltf holds no colliders or misses its .bin, which Blender shapes become colliders, the collider count, vertex and volume limits, and what colliders decide: occupied cells, mount points, room inside a block, what players can aim at.
---
Every block model needs colliders.
- The block's BlockModelComponentDefinition.Model points at a .model asset; that asset's CollisionShapesAsset points at the collision .gltf.
- With no collider, validation reports on Model: 'BlockModel is missing any collider. Please use model with Collider Shape set.'
- Model is the main model asset with the collision assigned, never a collision-only .gltf.
- Each model state (non-fractured, fractured) is its own .model with its own CollisionShapesAsset.

The collision .gltf:
- Colliders come only from Blender objects that carry a rigid body. The collider's shape is the rigid body's collision shape (box, sphere, capsule, cylinder, convex hull; mesh becomes a triangle mesh), not the object's geometry. COMPOUND exports nothing. Active or passive both work.
- A collision mesh without a rigid body exports no collider: 'Loading of colliders from file ... was not successful, meaning there are no colliders stored in the file.' The VRAGE add-on's convex hull generator adds a passive rigid body itself; a hand-made collision mesh needs one added.
- The .gltf names its .bin by a buffer uri, looked up literally in the .gltf's folder. A file name with spaces (stored as %20), or a .gltf/.bin renamed after export, fails with 'Missing file for GLTF Model: <uri>'. Export without spaces, do not rename the pair, keep both in one folder.

Limits (validation warnings, not errors; they grow with block size):
- Collider count and vertices: a 2.5 m block allows 7 colliders and 80 collider vertices, a 4 m block 120 vertices; fractured models allow more. Keep collision to a few simple shapes, not a copy of the visual mesh.
- Bounds: the colliders' bounds may hold at most 8000 m3; a block past it is made smaller.

What colliders decide:
- Occupied grid cells and generated mount points come from the model's collider, not the visual mesh: every cell a collider reaches into (less a small inward margin) is occupied, plus optional extra mount-point colliders picked by a name prefix. A box bigger than the model, or a gap to neighbours, is fixed in the collider's size and placement or the LOD0 offset; there is no separate bounding box property.
- Room for another block inside a block needs the outer block's collider hollow there.
- Aiming at a block (the paint tool) is a ray cast against colliders: a part of the mesh no collider covers cannot be targeted. Colliders should cover the whole mesh.
