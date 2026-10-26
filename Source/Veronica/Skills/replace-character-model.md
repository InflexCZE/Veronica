---
name: replace-character-model
description: Use when the user wants to replace the player character's model, skin materials or jetpack flame position.
---
Needs:
- model: the character's .model, imported into the mod with its materials
- which imported material replaces which part of the vanilla skin (parts are named after the mesh materials: UpSuit, BottomSuit, Helmet, Gloves, Backpack, ...)
- folder: a mod folder for the overrides, e.g. Assets/Character

Each change below is an override of a vanilla definition: create_partial(<vanilla>, "override", folder), then write to the override.

Steps:
1. Materials: the character's imported materials render skinned. On each, write(material, "DefaultState", "PBR Skinned").
2. ModelCharacter (ModelComponentDefinition): write(override, "Model", model)
3. ModelMiroslavSokol (RenderModelDefinition): write(override, "ModelHandle", model)
4. CharacterKeenSkin (RenderSkinDefinition): read(override, "MaterialPairing") lists the parts; write(override, "MaterialPairing[<part>]", <material>) for each part the user maps.
5. Jetpack flame position, when the new body moves the backpack: PrefabCharacterClient_CharacterMovementEffectsComponentDefinition, write(override, "JetpackThrusts[i].ThrustBoneOffset", {X: .., Y: .., Z: ..}) for each jetpack thrust.

Show:
show(model) previews the new character in the Render View.
