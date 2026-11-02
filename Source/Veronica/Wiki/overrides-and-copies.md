---
name: overrides-and-copies
title: Partial definitions: overrides and copies
source: Veronica's create_partial tool (Docs/Tech/EditorTools.md); SE2 source PartialDefinitionHelper, ProjectDefinitionSerializationContext, SharedPartialDefinitionGraph, DefinitionLoadingData (PartialDefinitionKind), Definition, DefinitionLoader, ResourceHandleValidator, BlockModelComponentDefinition, ModelAssetData; vanilla Assets/Blocks/RSS_Scanner/100 (OreDetector100 partialdefs)
description: How a mod changes a definition it does not own: an override versus a copy, what values follow the original, how overrides from several mods combine, overriding a shared base definition versus a block's own copy, a new block or new looks from a vanilla one, an override that already exists, and load errors after copying definition files.
---
Vanilla and other read-only projects are never written. A mod changes their definitions through a partial definition, made with create_partial(def, kind, folder):
- override: changes the original everywhere it is used. Use it when the user wants the existing thing changed.
- copy: a new thing next to the original. It changes nothing until something references it. Use it when the user wants something new that starts from the original.
- A new block from a vanilla one: copies of only the definitions whose values change. An override would change the vanilla block too.

In both, values left alone keep following the original. Write only the values that change.
An override holds only the values it changes, so overrides of one definition from several mods all apply. When two set the same value, the one applied last wins; mods apply in project dependency order.
Vanilla definitions are often copies of a shared base definition (e.g. a vanilla ore detector's OreDetectorComponentDefinition is a copy that sets only Range). An override of the base reaches every copy that leaves that value alone; an override of the block's own copy reaches only that block.

A mod holds at most one override of a definition. When one exists, create_partial's error names its file; write to that file instead.

A block's looks: Model, FractureModel and DamagedModel of its BlockModelComponentDefinition point at model assets. Pointing them at another model asset (e.g. one with the user's material) in an override changes the vanilla block everywhere; in a copy, only the new block. A scaled model is a new model asset from the same FBX with its Transformations.Scale set.

Load errors:
- 'Cannot locate value for definition <guid>': something references a definition the game did not load, e.g. a definition file copied into the mod without the definitions it references.
- 'Can't translate a resource handle <guid> to file handle': a reference (often FractureModel or DamagedModel) points at a file that does not exist; point it at an existing asset.
- 'The object builder's guid X should match the guid Y': a definition file copied or renamed outside the Editor keeps its old inner Guid. Definition files are copied, renamed and moved in the Editor only.
