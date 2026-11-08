---
name: add-skybox
description: Use when the user wants their own skybox (space background) in the game.
---
Needs:
- faces: the six face images, imported into the mod and named <name>_px, _nx, _py, _ny, _pz, _nz
- folder: a mod folder for the override, e.g. Assets/Skybox

Plan:
- Make the skybox
- Find the environment configuration
- Override the environment configuration
- Set the skybox
- Show the skybox

Steps:
1. create_asset("skybox", <the faces' folder>, inputs: the six faces) makes the skybox's .cubemap; each name ending picks its face. Once made, each face is a field of the .cubemap that read and write change.
2. find_content("EnvironmentResourcesConfiguration") finds the game's environment configuration and Vanilla's override of it.
3. create_partial(<Vanilla's EnvironmentResourcesConfigurationOverride>, "override", folder). This overrides the configuration the game uses; Vanilla's own override stays below the mod's.
4. write(override, "Skybox", <the new .cubemap>)

Show:
show(<the new .cubemap>). The Render View's Default skybox follows the environment configuration, so it shows the new skybox.
