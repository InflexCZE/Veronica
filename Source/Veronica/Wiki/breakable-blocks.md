---
name: breakable-blocks
title: Breakable blocks: fractured and deformed models, fracture pieces and colliders, critical fractures, build stages
source: SE2 source BreakableBlockComponent, BreakableBlockDefinition (CriticalFractures), BlockModelComponentDefinition (Model, FractureModel, DamagedModel, FractureReplacementModels, DamagedReplacementModels, BreakableCollider, BuildProgressModels and Regenerate BuildProgressModels) and its breakable validation, the model import of fracture groups and colliders (group and ColliderMeshGroups custom properties, the fracture connectivity graph, bounding box checks), the Model Calculator, dummy SupportingFracture, CubeBlockDefinition MaxHealth, CubeBlockFragilityDefinition; vanilla Assets/Blocks/Shared/Fragility, the NonBreakable block templates
description: How a block breaks into pieces: the whole, fractured and deformed models and their replacement maps for build stage models, what the fractured model file must hold (fracture groups, one collider per piece, touching colliders, piece count, matching bounds) and its import errors, critical fractures and dummies tied to a piece, why a light block never shows its pieces, build stages generated from the fractured model, and what the breakable-block validation messages mean.
---
Models:
- A breakable block has a BreakableBlockComponent (with a BreakableBlockDefinition) and three models on its BlockModelComponentDefinition: Model (the whole block), FractureModel (Inspector label Fractured Model: the block cut into pieces) and DamagedModel (Inspector label Deformed Model: the dented block).
- FractureModel is taken as the fractured version of Model; DamagedModel as the damaged version of both Model and FractureModel. DamagedModel may be the fractured model itself or a dented copy of it. Leaving either unset gives 'The model does not have a fracture replacement model' (or damaged); set both even when FractureModel is fine.
- Any other model of the block (each build progress model) needs its own entries in FractureReplacementModels and DamagedReplacementModels, which map a model to its fractured and damaged version; a missing entry gives the same message. A model whose own collider already holds more than one fracture needs none.
- The block's BreakableCollider comes from the FractureModel's collision, or from Model's own if that holds fracture colliders. A stand-in FractureModel without fracture colliders leaves it empty, so the main model cannot stand in: 'Breakable blocks must have a breakable collider.' Fracture colliders on Model itself give the info 'Breakable collider on optimised model. Use an optimised collider instead.'
- A breakable block also needs the tags IBlockPhysicsColliderProvider and IMountPointModifier on some component; 'Missing tag on any component when BreakableBlockComponent is present: <tag>' names the missing one. A vanilla breakable block's components show which carry them.
- A block that need not break drops its BreakableBlockComponent; every breakable check below is then skipped. NonBreakable templates have none.

The fractured model file (the user's art work) must hold:
- Fracture groups: each mesh of a piece carries the custom property group (case-insensitive) with its fracture name, e.g. Fracture_01, in every LOD. Fracture parts are not merged. FractureGroupName is not read.
- File names do not matter, fracture names do. All pieces of a LOD go in that LOD's file.
- Exactly one collider per fracture, all in the model's single collision file, named like its fracture group; the collider's ColliderMeshGroups lists the fracture groups whose meshes belong to it. All colliders in the file are fractures, or none.
- Unique fracture names. A repeated one fails import with 'An item with the same key has already been added. Key: <fracture>'; Blender duplicate suffixes (.001) and extra number suffixes (Fracture_01_03) cause such clashes.
- At least 2 fractures, at most 16. The Model Calculator suggests fewer for small blocks (1 at 0.25 m up to 15 at 2.5 m and larger); that is guidance, not a validation error.
- One connected set of colliders: each lies within 0.1 of another (touching or overlapping), with no isolated piece and no separate cluster. Otherwise import fails with 'Connectivity graph should have at least one connection' (or isolated / disconnected nodes). Separate parts need a connecting piece, which may be invisible.
- Matching bounds: FractureModel's bounding box matches Model's within 0.05 at its corners, DamagedModel's within 0.1 (same extent and origin offset). A mismatch gives the info 'The bounding box of ... doesn't match standard model ... use same transformation offset'; it does not block use. The model asset's Snap model to SE2 grid sets the LOD0 transformation.

Critical fractures and dummies:
- BreakableBlockDefinition.CriticalFractures lists fracture names; when the block loses any of them it stops functioning. Critical fractures also take a larger share of the block's health.
- The Inspector suggests fracture names only when the block's model has a breakable collider; otherwise it says 'Did not find any breakable collider on models'.
- A dummy's SupportingFracture names the piece it sits on: the dummy counts as gone once that piece breaks off (conveyor ports, solar sensors, inventory and cell slots use it). SupportingFracture on a dummy of a block that is not breakable is a validation error.

Health and seeing the pieces:
- MaxHealth = the Fragility's MaxHPMassMultiplier x the block's mass (from Density and its cells). Fragility points at a CubeBlockFragilityDefinition (vanilla ones in Assets/Blocks/Shared/Fragility).
- A light block can go from whole to destroyed in one hit and never show its fractures; a higher Density or Fragility makes the pieces show.

Build stages:
- BuildProgressModels is generated by the BlockModelComponentDefinition's Inspector action Regenerate BuildProgressModels, from construction attributes (ConstructionMesh* custom properties) baked into the model source file: the FractureModel's if set, with Model as the finished stage, else the Model's.
- 0 steps means the model has no construction attributes.
- Regenerate after the models change, e.g. on a copied vanilla BlockModelComponentDefinition.
