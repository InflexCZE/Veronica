---
name: ore-detectors
title: Ore detectors: range, ores found, who sees them, HUD markers, power
source: SE2 source OreDetectorComponent and OreDetectorComponentDefinition (VRage.Voxels), OreDetectorBlockComponent, OreDetectionClientSessionComponent, OreDetectorLocalHUDComponent, OreDetectorHUDConfiguration, VoxelMaterialGameDefinition, PowerableBlockDefinition; vanilla Assets/UI/HUD/OreDetectorHUDConfiguration.def; vanilla ore detectors (Assets/Blocks/RSS_Scanner), the Ore Detector template
description: What an ore detector block does: its range and unit, which ores it finds, who sees what it finds, the icon, colour and name a found ore shows on the HUD and where a new ore's marker is set, power, what the template brings, and the parts that are only looks.
---
Parts:
- OreDetectorComponent, on both sides, with its OreDetectorComponentDefinition: Range, the most a player can set in the block's terminal (a built block starts at it); and DetectableOreMaterials.
- OreDetectorBlockServerComponent and OreDetectorBlockClientComponent, with no definition: the block's Broadcast switch, set by players.
- PowerableBlockComponent as the block component. Its PowerableBlockDefinition's ConsumedResource is Electricity; its Amount is the kW the detector draws while it is on.
- No dummy is needed for the detector to work.

What it does: it finds ore of its materials within Range metres of the block, in a box around it, while the block works (built, on and powered). The ore shows on the HUD of players controlling its ship, and of players connected to the ship by antenna while the block's Broadcast is on.

How a found ore shows on the HUD:
- Its icon and text colour come from its voxel material's entry in VoxelMaterialMarkers (VoxelMaterial, Icon, Color) of the OreDetectorHUDConfiguration (Assets/UI/HUD/OreDetectorHUDConfiguration.def). A material with no entry, or an entry without Icon or Color, gets the default GPS marker icon and colour, so a new ore needs an entry, added by an override of that configuration.
- Its name is the DisplayName of the item its VoxelMaterialGameDefinition names as YieldType. A wrong name on the HUD means a wrong YieldType, not a wrong marker entry.

Vanilla ore detectors: Assets/Blocks/RSS_Scanner/<size> (100, 500), each with its own OreDetectorComponentDefinition and PowerableBlockDefinition, finding the same ores as the template.

The Ore Detector template: a block made from it gets its own PowerableBlockDefinition (power Amount 0, in the vanilla ore detectors' G-Screen family) but keeps the template's shared OreDetectorComponentDefinition. A range of its own needs a definition of its own: a copy of the shared one, set on the block's OreDetectorComponent.

Looks only, not needed to find ore: the spinning dish of vanilla detectors (a ConditionalSubPartsComponent with subpart models and their dummies), and the damage particles and sounds of the BlockEffectsComponentDefinition.
