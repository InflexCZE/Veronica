---
name: skyboxes
title: Skyboxes and environment lighting: the cubemap, its format, the preview, the sun, planet night brightness
source: SE2 source CubemapModel, CubemapProcessor, RenderViewModel (skybox selection), FlareDefinition, PostProcessSettings; vanilla skyboxes (Assets/Environment/Skybox), the sun flare (Assets/Environment/Flares/Sun), Verdure's atmosphere and FrameSettingsOverrideDefinition (Assets/Environment/Planets/Verdure/Atmosphere)
description: How a skybox is made (a .cubemap asset of Category Skybox built from six face textures, which slot is which face and why faces look too bright), how the Render View previews it, what the sun is and what of it a mod changes, and which planet definitions hold ambient and night brightness.
---
The skybox:
- A skybox is a cubemap asset (.cubemap) of Category Skybox with six face texture slots: +X, -X, +Y, -Y, +Z, -Z. The slot decides the face, not the file name. The Editor builds the cube from the faces.
- Vanilla skyboxes: Assets/Environment/Skybox/<name> (e.g. Default, its Starfield.cubemap and six face textures).
- Faces are read as linear unless the output format is an sRGB one, so ordinary sRGB images look too bright under the default HDR format. The format comes from CompositeOutputFormat on the face textures, or the Skybox category's own override.

Preview:
- The Render View shows one skybox picked from a list, remembered per project.

Sun and lighting:
- The sun is a FlareDefinition (Assets/Environment/Flares/Sun): its flare is the part a mod changes.
- A planet's ambient and night brightness live in its atmosphere definition (AmbientIntensity, NightFogIntensity, NightFogColor and more) and its FrameSettingsOverrideDefinition (DirLightIntensity, the IndirectLight factors); vanilla Verdure's are in Assets/Environment/Planets/Verdure/Atmosphere.
- Exposure and tonemapping are engine render settings, not definitions: a mod cannot change them.
