---
name: mod-project
title: Mod projects: files, dependencies and what fails publish validation
source: SE2 source VRageProjectID, ProjectConfiguration (ProjectDependencies), LoadedProjectDAG.Loader, ProjectDAGLoadingResultExtensions, ProjectFileSystemProxy (MetafileNonexistent), PartialDefinitionKind, ProjectPublishHelper, ContentCache, ContentBlobDataBuilder, ValidateProjectItemsFeature, ValidatePngColorProfilesFeature, ValidateProcessingFeature, WorkshopComponent; vanilla GameData/Vanilla/Vanilla.vrgproj, Assets/contentcache_reference.vrb
description: What a mod project folder is made of (the .vrgproj, Assets, Content, .def, .partialdef and .meta files, generated .vrb files, the Workshop metadata file), why a project or its dependents fail to load, how a mod depends on another mod through ProjectDependencies, and which publish validation errors (empty folder, PNG colour profile, asset hash mismatch, file handle already registered) block publishing and how to clear them.
---
Files:
- A mod project is a folder <ModName> in the mods directory: <ModName>.vrgproj, Assets (the authored content), Content (processed output), an optional Worlds folder, thumb.jpg (the Workshop picture) and ModProject.modhub-metadata (holds the published Workshop id; publishing again updates that item only while the file is kept).
- Authored content is .def and .partialdef files; every asset has a .meta beside it. contentcache.vrb and the other .vrb files are generated (publish builds the content cache) and are never edited.
- Files enter a project through the Editor's import, which writes the .meta.
- Vanilla and dependency projects are changed only through partial definitions (override or copy), never by editing their files.
- An asset whose .meta no longer matches (e.g. a renamed file) is unreadable (MetafileNonexistent), and every project depending on it fails to load ("Project hierarchy loading failed").
- Icons: keep block icons small like vanilla's; full-size renders bloat the mod.

Dependencies:
- ProjectDependencies in the .vrgproj lists ProjectIDs (Guid, ModId, ServiceId). Each entry is an exact copy of the dependency's own ProjectID block from its .vrgproj; a local-only mod has ModId null and ServiceId "".
- Every mod lists Vanilla (Guid 56dacb76-e25e-468a-9432-21964a9a0569); that entry stays as it is.
- A mod references or overrides another mod's definitions only when that mod is a dependency. A dependency that cannot be found fails loading; cyclic dependencies are rejected.

Publish validation: errors block publishing; warnings (LOD count, LOD triangle counts, more than one material) do not.
- "Empty folder": remove the folder or put a file in it.
- "PNG Color Profile Validation ... color profile matches known incorrect": the PNG embeds an ICC profile on the validator's bad list (HP-Microsoft sRGB v2). Re-save the PNG without the embedded profile.
- Asset Processing Validator "hash does not match hash saved in metadata" (asset, dependency or output hash, or processor information): the asset or its processed output differs from what its .meta recorded.
- "File handle already registered": two assets register the same content file, e.g. a copied .meta. Remove the duplicate.
