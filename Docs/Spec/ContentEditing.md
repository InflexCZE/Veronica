# Content editing

What the assistant can do to the content of the mod open in the Editor.

## Reading

- Find content items by name or type, list folders, find what refers to an item, search inside a definition.
- Read definitions and other assets with fields, whole or one field at a time, as the Property Inspector shows them.
- Explain a type from the game's own documentation: its fields, relations and units.
- The mod's dependencies, such as Vanilla, are read and referenced like the mod's own content.

## Changing

- Edit fields and collections of the mod's definitions.
- Add and remove a block's components.
- Upgrade a block's component to a more specific type, such as making a cargo container draw power, keeping every value the block had. The values only the new type has come from a template, block or definition the assistant picks as the closest fit.
- Every change saves the way an inspector edit does; an open Property Inspector shows the new value.
- A value outside the game's validation range is written anyway, with a warning in the reply.

## Creating

- Folders.
- Blocks from the Editor's prefab templates. Where a vanilla template itself fails validation, the block comes out fixed.
- Blocks of a vanilla family the Editor has no template for, such as atmospheric thrusters, offered among the templates as if the Editor had one.
- Partial overrides of a definition (changes it everywhere) and copies (a new thing).
- New definitions of a type, with its defaults.
- Skyboxes from six face images.

## Limits

- Read-only projects, Vanilla among them, are never written. A dependency's definition is changed through an override or a copy inside the mod.
- Changes are not Editor undo steps. The modder reverts with version control.

## Out of scope

- Importing files, art and audio, and testing in game: left to the modder.

## Not yet

- Child entities of a prefab ([PrefabChildEntities](../TODOs/PrefabChildEntities.md)).
- Partial prefabs ([PartialPrefabs](../TODOs/PartialPrefabs.md)).
- Moving a tag other components depend on ([PrefabTagMove](../TODOs/PrefabTagMove.md)).
- One query over a field across many items ([ContentQuery](../TODOs/ContentQuery.md)).
- Asset kinds beyond definitions, cubemaps and skyboxes: kinds join as each is tested in the Editor.
