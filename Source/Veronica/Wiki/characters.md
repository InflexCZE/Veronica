---
name: characters
title: Characters: the player model, skins, suit colours, animations and hand tool poses
source: SE2 source ModelComponentDefinition, RenderModelDefinition, RenderSkinDefinition, SkinComponentDefinition, UseSkinColorChangeAction, DLCDefinition, MaterialDefinition, AnimationControllerComponentDefinition, AnimationControllerDefinition, AnimationDefinition, ToolComponentDefinition; vanilla Assets/Characters/System (ModelCharacter, ModelMiroslavSokol, ModelPresetsMiroslavSokol, Skins), Assets/Characters/MiroslavSokol (Models/Skeletons, Materials/Default), Assets/Characters/Shared/AnimationControllers, Assets/MainMenuData/DLC, Assets/CharacterTools/DebugGun; Mod SDK ExampleMod1/CharacterModel
description: How the player character's look is changed: replacing the default character model, the skeleton rule, a selectable skin (RenderSkinDefinition) and its fields, recolouring suit parts through materials, DLC-locked skins, character animations and controllers, and how a hand tool picks its hold pose and orientation.
---
Replacing the default character:
- The default character's model is set in two vanilla definitions in Assets/Characters/System: ModelCharacter (ModelComponentDefinition, Model) and ModelMiroslavSokol (RenderModelDefinition, ModelHandle). An Override partial of each, pointing to the new model, replaces the character for everyone; players cannot pick it (a look players pick is a skin, below).
- 'Model does not contain part '...' that is hidden by a preset': the presets (ModelPresetsMiroslavSokol) name vanilla parts the new model lacks. Warnings only.
- The Mod SDK's ExampleMod1/CharacterModel is a full example character: ModdingGuideCharacter.fbx with its _LOD1.._LOD4 files and per-part PNG textures.

Skeleton:
- A character or skin model uses the vanilla skeleton: same bones, same names. It is Assets/Characters/MiroslavSokol/Models/Skeletons/MiroslavSokol.fbx, with _LOD1.._LOD4 files (LOD0 has no suffix).
- A mesh bone missing from the skeleton fails model processing.
- The vanilla animations play on the vanilla bones; a different skeleton needs its own animation controller (Animations, below).

Skins:
- A look players pick is a RenderSkinDefinition; vanilla ones are in Assets/Characters/System/Skins (e.g. PioneerSkin).
- Fields: Model (optional model asset, on the character's skeleton), MaterialPairing, DisplayName, Icon, HoverIcon, UsableByPlayer (default on), MovementEffectsOverride, BackpackPrefab, DLC.
- Players pick a skin in the skin menu opened by a UseSkinColorChangeAction interaction.
- SkinComponentDefinition's DefaultSkin is the starting skin; with PersistentSkin off the choice resets to DefaultSkin.
- DLC: a DLCDefinition (vanilla ones in Assets/MainMenuData/DLC) limits the skin to owners of that DLC; empty means everyone.

Suit colours:
- MaterialPairing maps each part of the character model, by part name, to a MaterialDefinition. Textures belong to the materials, not to the skin.
- A recolour needs no model change: copy the part's vanilla material, point the copy at a recoloured colour texture, and pair it to the part in the skin.
- Vanilla character materials are in Assets/Characters/MiroslavSokol/Materials/Default (e.g. MiroslavSokol_HelmetVisor, a PBR material with ColorMetalTexture, NormalRoughnessTexture and ExtensionsTexture).
- A part without a MaterialPairing entry does not get the new material: pair every part of the model. Vanilla CharacterKeenSkin's MaterialPairing lists the part names.

Animations:
- A character's animations come from the AnimationControllerDefinition of its AnimationControllerComponentDefinition. Vanilla controllers are in Assets/Characters/Shared/AnimationControllers (Controller for third person, ControllerFirstPerson).
- A different skeleton or a new animation set needs a complete new AnimationControllerDefinition, not an override of the vanilla one.
- An .animation asset wraps a source animation file (.mwa or .vra). An AnimationDefinition plays it: Animation, optional FirstPersonAnimation, Loop, BonesMask, blend durations. The controller's layers play AnimationDefinitions.
- Vanilla seat, tool and cockpit poses are reused without new animations.

Hand tools:
- ToolComponentDefinition's Name is the key the animation controller uses to pick the hold pose (blackboard key EquippedHandTool). A name the controller does not know leaves the tool held at the character's side.
- Valid names are the EquippedHandTool target values in vanilla Controller; copy the Name of a vanilla tool held the same way (vanilla DebugGun uses HandWeapon).
- A hand tool's orientation and size in the hand come from its model asset's LOD0 Transformations (Translation, Rotation, Scale). The vanilla DebugGun model (Assets/CharacterTools/DebugGun) is a working example.
