---
name: add-skybox
description: Use when the user wants their own skybox (space background) in the game.
---
Needs:
- cubemap: the skybox's .cubemap, imported into the mod (the Editor makes it from six images)
- folder: a mod folder for the override, e.g. Assets/Skybox

Plan:
- Find the environment configuration
- Override the environment configuration
- Set the skybox
- Show the cubemap

Steps:
1. find_content("EnvironmentResourcesConfiguration") finds the game's environment configuration and Vanilla's override of it.
2. create_partial(<Vanilla's EnvironmentResourcesConfigurationOverride>, "override", folder). This overrides the configuration the game uses; Vanilla's own override stays below the mod's.
3. write(override, "Skybox", cubemap)

Show:
show(cubemap). A cubemap has no Render View preview of its own: ask the user to pick it in the Render View's Skybox Settings (cloud icon).
