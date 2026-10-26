---
name: add-voxel-material
description: Use when the user wants a new voxel material (the stuff asteroids and terrain are made of) and asteroids made of it.
---
Needs:
- render material: the material .def the Editor made from the user's imported textures (a TriplanarSingleMaterialDefinition)
- name: the material's name as players see it
- physical behavior: which vanilla VoxelPhysicsMaterialDefinition it acts like (find_content("VoxelPhysicsMaterial") lists them, e.g. IceLikeMaterial)
- folder: a mod folder, e.g. Assets/VoxelMaterial

Steps:
1. Texture scale, when the user wants it: write(render material, "StandardTiling.BaseScale", n); vanilla asteroid rock uses 6.
2. create_definition("VoxelMaterialDefinition", <name>VoxelMaterial, folder)
   write(voxel, "Name", name); write(voxel, "PhysicsMaterial", <physics material>)
3. create_definition("VoxelRenderMaterialDefinition", <name>VoxelRenderMaterial, folder)
   write(render, "VoxelMaterial", voxel); write(render, "RenderMaterial", render material)
4. Asteroids of it: find_content("VoxelGeneratorDefinition") lists the asteroid generators; when several fit, ask the user which one. create_partial(generator, "override", folder), then read(override, "SurfaceMaterials") and write(override, "SurfaceMaterials[i].Material", voxel) on the entries to replace. element_remove the entries the user wants gone.

Show:
show(render material) previews it in the Render View.
