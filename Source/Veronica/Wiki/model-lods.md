---
name: model-lods
title: Model LODs: how many, triangle and material budgets, LOD files, validation warnings
source: SE2 source ModelLimitsChecker (triangle, material, mesh part and LOD distance limits), ModelAssetProcessorValidator (LOD count and suggested bounding box checks), ModelSkeletonUtils (bones per LOD), the model asset (GeometryAssets, MaterialAssignments, SuggestedModelSize), BlockModelComponentDefinition (DamagedModel, FractureModel), the Editor's Model Calculator; vanilla Assets/Blocks/Windows/Flat/125/Models (NonFractured, Deformed)
description: How a block model's LODs work: how the LOD count and the triangle, material and mesh part budgets per LOD follow from model size, why the last LOD allows one material, the LOD files and their names, deformed and fractured models, bones in lower LODs, and which model validation messages are only warnings.
---
Warnings, not errors:
- The model validation messages on LOD count ('should have exactly <n> LODs'), triangles per LOD ('exceeded recommended number of triangles'), materials per LOD ('too many materials') and mesh parts per LOD are warnings: the block still builds and works. There is no hard triangle limit. Meeting them keeps a model in line with vanilla performance.
- 'Suggested bounding box differs too much': the model asset's SuggestedModelSize, when set, is more than 0.25 m off the real model size on some axis. A warning; set it close to the model's size or leave it unset.

How many LODs:
- LOD0 is the most detailed; higher numbers are simpler.
- The expected LOD count is computed per model from its size (surface area) and its real LOD0 triangle count, and the warning states it.
- LOD switch distances are computed from model size too, not authored per model, so they can look short on large blocks.

LOD files:
- The model asset lists one geometry file per LOD (GeometryAssets). Vanilla names the LOD0 file without a suffix and the lower LODs <name>_LOD1, _LOD2 and so on (e.g. Assets/Blocks/Windows/Flat/125/Models/NonFractured).
- A detail is left out of a LOD by leaving its mesh out of that LOD's file; there is no per-mesh LOD setting.
- Nothing requires each LOD to be a distinct mesh; only the count and the per-LOD budgets are checked.

Triangles per LOD:
- Each LOD has a triangle budget from model size and LOD index. The last LOD's budget is 32 triangles; the LOD0 budget tops out at 40000 for models of 5 m and bigger. Fractured models get about 1.8x more.
- A sparse LOD0 moves the model to a lower tier with fewer expected LODs; adding detail to LOD0 moves it up.

Materials and mesh parts per LOD:
- Materials allowed per LOD, by the model's longest edge: under 0.5 m 4, 3, 1; 0.5 to 2 m 8, 8, 6, 4, 1; over 2 m 18, 18, 12, 7, 1. Fractured models allow 32 on every LOD but the last.
- The last LOD always allows exactly 1 material, fractured models included; parts using LCD materials do not count. A model with only LOD0 has LOD0 as its last LOD, so it allows 1 material; adding LODs lifts that.
- Mesh parts are counted per distinct material in a LOD, not per mesh object: many meshes sharing materials are fine. The mesh part limit equals the material limit (256 for fractured models).
- 'does not have any material asset assigned': that mesh part's entry in the model asset's MaterialAssignments is empty; assign a material definition to it.

Deformed and fractured models:
- The deformed model (BlockModelComponentDefinition DamagedModel) is a model asset of its own, with its own LODs and budgets.
- A fracture group is a tag on meshes; meshes of any LOD with the same group tag belong to the same fracture. Fracture colliders belong to the whole model asset and match groups by name, so LODs need no colliders of their own.

Bones:
- 'Model bone invalid in LOD hierarchy' (Child is present at LOD<n>, but parent only on LOD<m>): a lower LOD file removed a bone but kept its children; remove the bone together with all its child bones.

Model Calculator:
- The Editor's Model Calculator window gives the budgets validation uses: enter the block size (m), Has Fractures and the LOD0 triangle count; it returns the LOD count and, per LOD, max triangles, max materials and switch distance, plus max colliders and collider vertices.
