---
name: vanilla-layout
title: Where vanilla content lives and which definition holds what
source: vanilla Assets folders, read with list_folder and find_content
description: The vanilla folder layout (blocks, thrusters, G-Screen, environment) and which definition of a block holds which value (force, flame, sound, name, power, build cost).
---
Folders:
- Blocks: Assets/Blocks/<family>/<name>/<size in cm>. Thrusters: Assets/Blocks/Thrusters/<class>/<size in cm>; list_folder shows the sizes.
- G-Screen categories, subcategories, tiles and block kinds: Assets/UI/Screens/GScreen.
- The environment configuration (skybox and more): find_content("EnvironmentResourcesConfiguration") finds it and Vanilla's override of it.

A block has one definition per component. Which one holds a value:
- ThrusterDefinition: force and how the thruster behaves.
- ThrusterEffectsComponentDefinition: the flame.
- BlockEffectsComponentDefinition: sounds.
- PowerableBlockDefinition (or the block's other *BlockDefinition): name, power, build cost, mount points.
- BlockModelComponentDefinition: the models and the dummies.

find_content with words of the thing and of the definition type finds a definition, e.g. "IonThruster100 ThrusterEffects".
