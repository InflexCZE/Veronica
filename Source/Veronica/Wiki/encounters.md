---
name: encounters
title: Encounters: worlds and prefabs an encounter spawns, the Worlds folder
source: SE2 source EncounterSpawnerComponent (EncounterCandidate, EncounterSpawnerDefinition), WorldSaveDefinition, DefaultWorldsComponent, EncounterPCULimitConfiguration, CargoShipEncounterComponent, VRageProjectCommon (WORLDS_FOLDER_NAME), ProjectPublishHelper; vanilla worlds in GameData/Vanilla/Worlds/Encounters
description: How a mod adds an encounter such as a cargo ship or unknown signal: the encounter spawner's candidates and their weights, the saved world in the mod's Worlds folder and the files it holds, the WorldSaveDefinition that names it, the "does not exists" validation error, replacing a vanilla world, and the PCU limits of encounter prefabs.
---
What spawns:
- An EncounterSpawnerDefinition's Candidates list what an encounter can spawn. Each candidate has a Probability (a weight; the weights need not add up to 1) and either a World (a WorldSaveDefinition) or a Prefab (a PrefabDefinition).

The world:
- A game save made in game, e.g. an empty world holding only the ship. Its folder holds container-info, savegame.vrb, assetjournal.vrb, sessioncomponents.vrb, snapshots.vrb and thumb.jpg; the Backups folder is not needed.
- The user copies the save folder to Worlds/<WorldName> in the mod's root, next to Assets and Content. The folder must be named exactly Worlds. Publishing includes it.
- Vanilla's encounter worlds are in GameData/Vanilla/Worlds/Encounters, one folder per world (e.g. SpaceCargo_ArmedRenegadePathfinder).

The WorldSaveDefinition:
- World.SaveName is the world's folder name (case-insensitive).
- Validation error "World <name> does not exists": no loaded project has a Worlds folder of that name.
- A world of the same name in a later project replaces the earlier one, so a mod replaces a vanilla world by giving its world the vanilla folder name.
- Worlds are mounted when the project loads: after moving or renaming one, reload the project, or validation reports the world as not mounted.

PCU limits:
- EncounterPCULimitConfiguration: PCU limits of encounter prefabs.
- A prefab with a CargoShipEncounterComponent fails validation ("Cargo potential max PCU ... is above the limit of ...") above MaxCargoShipPCU.
- MaxTotalCargoShipPCU is a separate limit on all cargo ships generated together.
