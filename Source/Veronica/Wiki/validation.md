---
name: validation
title: Validation: errors and warnings, what common messages mean, checking a block in the Editor
source: SE2 source NullableValidator, PendingContentDependencyResolver, AssetRemovalTracker, CubeBlockComponent (OccupiedCellsDebug debug draw), BlockOccupancyGenerator
description: What the Editor's validation messages mean and how to clear them: errors versus warnings, an empty required field, an unknown dependency, a deleted file or definition still in use, deleting files for good, and seeing which grid cells a block occupies.
---
Severity:
- An error fails the asset: a model or definition with an error does not load in game. A warning does not stop it.
- Example: a model's LOD count off from what is expected is a warning; a breakable block without a fracture model is an error.

Common messages:
- 'The member was declared as non-nullable but has the value null: <Member>': a required field is empty. The message names the field (e.g. BlockKind on a block definition); give it a value.
- 'Unknown dependency: <guid>. Asset was not found in content nor in current batch' (a warning): the asset references a GUID that is not loaded, and stays pending until it is.
  - While the Editor is still reprocessing files it is transient and clears by itself.
  - If it stays, the reference is really missing: the target was deleted, moved outside the Editor, or has errors of its own. Fix the target or point the reference elsewhere.
- 'Uses of resource pending removal: <file>. Please clear or update asset references.': a deleted file (e.g. a .png) is still referenced by an icon, texture, material or model field. The removal waits until each reference is cleared or pointed at another file; restarting does not clear it while references remain. 'Uses of definition pending removal' is the same for a deleted definition.
- Deleting a file or folder in the project explorer is permanent: Editor undo covers definition edits, not deleted files.

Checking a block in the Editor:
- The OccupiedCells debug draw in the Editor preview (Preview Settings) shows which 0.25 m grid cells a block's model takes up.
