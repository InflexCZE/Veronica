---
name: materials
title: Materials: model material slots, PBR and composite textures, colourable, glowing, blinking, glass, cutout and decal materials, tiled armour, swapping while a block works
source: SE2 source MaterialDefinition, PBRMaterialDefinition, PBRTransparentMaterialDefinition, PBRParallaxMaterialDefinition, ParallaxDecalMaterialDefinition, PBREmissiveMaterialDefinition, ColorIndicatorMaterialDefinition, TilingArmorMaterialDefinition, BlockMaterialSwapperComponent and BlockMaterialSwapperDefinition and its validation, ColoringMaterialSwapperComponent, the model asset (MaterialAssignments) and its material warning, the composite texture (.comptex) asset, its engine texture presets and the PNG import; vanilla Assets/BlockMaterials (PaintedMetalColorable, EmissiveColorable, ClearGlass, Chrome_TwoSided, Constructions/Parallax, Decals/Atlases, Decals/Trims), Assets/ArmorSkins/LightArmor, Assets/System/Configurations/PaintToolConfiguration.def, Antenna1250_BlockMaterialSwapperDefinition
description: How a block's look is made of materials: how a model's material names bind to material definitions, the material definition types, the PBR material's three packed textures and their composite textures, texture file-name suffixes, making a material colourable, glowing, blinking, glass, cutout or a decal, one- and two-sided faces, tiled armour, and swapping materials while a block works.
---
How a model gets its materials:
- A model asset (.model) has MaterialAssignments: each material name in the exported mesh (one per material slot) maps to a material definition. The FBX carries only the names and UVs; the modelling tool's shader setup is not used.
- When a new material name appears in the model's LODs, the Editor assigns the material definition in the project with exactly that name. A name with a suffix (.001, _LOD2) does not match, and a material created after the import is not picked up; assign either in MaterialAssignments. Any slot can be reassigned there to any material.
- A slot with no material gives the warning "The mesh part '<name>' does not have any material asset assigned to it"; assign one.
- Vanilla block materials: Assets/BlockMaterials (e.g. PaintedMetalColorable); decal atlases under BlockMaterials/Decals/Atlases and Decals/Trims. A vanilla block's .model lists the materials it uses; its own LOD materials sit in its LODMaterial folder.
- A custom picture (screen, sign) needs its own material slot in the model and its own material with its own textures. An LCD's screens are the Surfaces of its LcdMultiPanelDefinition, each naming the screen's MeshPartName with Resolution and AspectRatio (vanilla Assets/Blocks/LCDPanels).
- Materials follow the mesh UVs; only the tiled armour material maps by world size.

Material definition types:
- PBRMaterialDefinition: the usual block material, opaque. Its default state picks opaque, alpha cutout or decal (below).
- PBRTransparentMaterialDefinition: glass.
- PBRParallaxMaterialDefinition: adds DisplacementTexture (height in R, 128 neutral) and HeightRatio (0.01 to 0.1, default 0.05; lower it if the relief is too strong); vanilla BlockMaterials/Constructions/Parallax. ParallaxDecalMaterialDefinition gives decals real depth.
- PBREmissiveMaterialDefinition: EmissiveColor and EmissiveColorStrength, coloured at runtime.
- ColorIndicatorMaterialDefinition: a glowing Color, Emissivity (0 to 500) and blinking.
- TilingArmorMaterialDefinition: armour.
- Glow and the colour mask need the ExtensionsTexture, which only the PBR and emissive types have.
- No material has a normal-strength value: edit the normal map.

PBR textures:
- PBRMaterialDefinition has three texture slots, each a composite texture (.comptex) built from an engine texture preset, plus EmissivityMultiplier (0 to 500):
  - ColorMetalTexture (_cm, preset PBRMaterialTemplate_ColorMetalTexture): colour RGB, metalness A.
  - NormalRoughnessTexture (_nr, PBRMaterialTemplate_NormalRoughnessTexture): normal RGB, roughness A.
  - ExtensionsTexture (_add, PBRMaterialTemplate_ExtensionsTexture): AO R, emissive G, colour mask B, transparency A.
- The material itself has no colour, metalness, roughness or gloss numbers; they are channels of its textures. There is no gloss or specular map: roughness is the inverse of gloss. Dull: low metalness, high roughness. Mirror: metalness 1, low roughness.
- Each comptex source is a texture or a constant (0 to 1), so a flat colour or a fixed metalness or roughness needs no image.
- Sources are PNG; the pipeline converts them. No hand-made DDS, no TIFF. A raw image in a material slot instead of a comptex renders black or wrong, and is stored uncompressed where a comptex is block-compressed.
- The Editor builds comptex assets from selected PNGs by file-name suffix (case does not matter, the word must): _albedo, _metal, _alphamask -> _cm; _normal, _roughness -> _nr; _ao, _emissive, _colormask -> _add; _height -> displacement. _Color, _Metalness, _NormalGL, _occlusion are not recognised.
- At runtime file names bind nothing: the material's slots reference the comptex assets.
- To retexture an existing material (terrain, grass), override its definition and point the slot at your own comptex.
- A custom material may reference vanilla textures and comptex assets; reusing them saves mod size and video memory.

Colourable (takes the player's paint):
- The colour mask (ExtensionsTexture B): white takes the paint, black keeps the material's own colour. Its default is 0, so a new material is not paintable: set the source to constant 1 for all of it, or an image for parts. Vanilla examples: PaintedMetalColorable, EmissiveColorable.
- Paint replaces the colour under the mask, so a part with a fixed colour needs mask 0 there (or a non-colourable material).
- The paint tool's starting palette: DefaultColors (ColorHSV entries) in Assets/System/Configurations/PaintToolConfiguration.def; palettes players already saved keep their colours.

Glow and blinking:
- Glow is a greyscale image or constant in the Emissive source of the ExtensionsTexture (G), scaled by EmissivityMultiplier. Brightness is per material: parts glowing differently need separate materials.
- Blinking: ColorIndicatorMaterialDefinition (state ColorIndicator) with Color, Emissivity and AnimationParameters: BlinkingRate (seconds per cycle), OnOffRatio (1 always on, 0 always off), OffColor. Different rhythms need separate materials.
- No texture animation exists (scrolling, flipbook, video). Use a blinking material, an animated subpart or particle effects.

Glass, cutout, decals and sides:
- Alpha does nothing on an opaque material. Glass: PBRTransparentMaterialDefinition (vanilla e.g. ClearGlass), opacity from the ExtensionsTexture alpha, image or constant.
- Alpha cutout (holes; pixels with alpha mask under 0.5 are not drawn): PBRMaterialDefinition with state PBRAlphaCutout or PBRAlphaCutoutNoShadow, mask from the preset PBRMaterialTemplate_ExtensionsAlphaCutoutTexture.
- Decal or sticker on a surface: state PBRAlphaCutoutSingleSided. It is drawn on top of other geometry, so it needs an opaque surface behind it. No reflective hologram material exists; a decal is the closest.
- Sides: the material's state decides whether back faces are drawn; read the vanilla states (engine Assets/Materials/States) for which are two-sided. On a one-sided state a face seen from both sides needs a duplicated flipped face, or a *_TwoSided vanilla material (e.g. Chrome_TwoSided).

Tiled armour: TilingArmorMaterialDefinition (vanilla Assets/ArmorSkins/LightArmor) maps by world size, not UVs; override it to change the pattern.
- Tiles (up to 16 entries of MaterialIndex 0 or 1 and a Transform), TilesCount (columns x rows, matching Tiles) and TileSize (one tile, in metres).

Swapping materials while a block works:
- BlockMaterialSwapperComponent with a BlockMaterialSwapperDefinition: while the block works (built, on, powered) it replaces materials, and restores them when it stops. The model's own assignments are the not-working look. No other state drives it: not damage, not a door's open or closed.
- WorkingReplacementModels: the models it applies to (the main model and its fractured and deformed models).
- WorkingReplacementsMeshParts: key = a material name from the model's MaterialAssignments (exact, e.g. EmissiveOff), value = the material while working. A fractured model's parts are keyed Fracture_NN-<material>.
- With only one of the two filled nothing swaps; validation says "Meshparts are set, but no model" or "Model is set, but no meshparts". Vanilla example: Antenna1250_BlockMaterialSwapperDefinition.
- ColoringMaterialSwapperComponent is the light block's variant (needs a light component and emissive replacement materials); for a plain swap use BlockMaterialSwapperComponent.
