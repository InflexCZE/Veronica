---
name: skyboxes
title: Skyboxes and environment lighting: the cubemap, its faces and format, which configuration picks it, the preview, the sun, planet night brightness
source: SE2 source CubemapModel, CubemapGeneratorProvider and SkyboxGeneratorProvider, EnvironmentResourcesConfiguration, FrameSettingsConfiguration, RenderViewModel (skybox selection), FlareDefinition; vanilla skyboxes (Assets/Environment/Skybox), Vanilla's EnvironmentResourcesConfigurationOverride (Assets/Environment), the sun flare (Assets/Environment/Flares/Sun), Verdure's atmosphere and FrameSettingsOverrideDefinition (Assets/Environment/Planets/Verdure/Atmosphere); tools create_asset, read, write
description: How a skybox is made (a .cubemap of Category Skybox built from six face images, which name ending is which face, which image types work, why faces look too bright), which configuration the game takes it from, how the Render View previews it, what the sun is and what of it a mod changes, and which planet definitions hold ambient and night brightness.
---
The skybox:
- A skybox is a cubemap asset (.cubemap) of Category Skybox with six face texture slots: PositiveXTexture, NegativeXTexture, PositiveYTexture, NegativeYTexture, PositiveZTexture, NegativeZTexture (+X .. -Z). Each slot's Texture points at one face image. All six must be set, or the cubemap does not build.
- create_asset("skybox", folder, inputs: six images) makes one: the name endings _px, _nx, _py, _ny, _pz, _nz pick the slots (+X, -X, +Y, -Y, +Z, -Z), so the file name decides the face when it is made; after that each face is a field of the .cubemap that read and write change. Six images are needed, each with a different ending. Without inputs it makes an empty skybox.
- Face images can be DDS, PNG, JPG, BMP, TGA or TIF, or a composite texture. EXR, HDR and a single panorama image are not accepted.
- Category Skybox gives an HDR cubemap and lists it in the Render View's skybox list; Category None gives a low-range cubemap that is not listed.
- Faces are read as linear unless the output format is an sRGB one, so ordinary sRGB images look too bright under the Skybox format.
- Vanilla skyboxes: Assets/Environment/Skybox/<name> (e.g. Default, its Starfield.cubemap and six face textures).

Which skybox the game shows:
- The Engine's EnvironmentResourcesConfiguration holds Skybox (the .cubemap) and SunFlare. Vanilla overrides it (EnvironmentResourcesConfigurationOverride) with the Starfield skybox; a mod's override of Vanilla's override wins. No planet or world has its own skybox.
- Reflections and ambient light are made from the skybox; nothing else needs to change with it.
- Skybox brightness and rotation, exposure and tonemapping are engine render settings, not something a mod can change.

Preview:
- The Render View shows one skybox picked from a list in its Skybox Settings (cloud icon), remembered per project. Its Default entry follows the environment configuration, so a mod's override shows there as soon as it is written.

Sun and lighting:
- The sun is a FlareDefinition (Assets/Environment/Flares/Sun): its flare is the part a mod changes.
- A planet's ambient and night brightness live in its atmosphere definition (AmbientIntensity, NightFogIntensity, NightFogColor and more) and its FrameSettingsOverrideDefinition (DirLightIntensity, the IndirectLight factors); vanilla Verdure's are in Assets/Environment/Planets/Verdure/Atmosphere.
