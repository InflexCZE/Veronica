# Editor tools: creators

Tools that create content: `create_folder`, `find_template`, `create_prefab`, `create_partial`, `create_definition`, `create_asset`, `component_add`. Each one is an Editor Create menu entry or the inspector's add button, run through the Editor's own generator, so the Editor's rules apply and come back as its errors. The shared contract is in `Tools_Overview.md`.

## Folders and names

- `create_folder` creates every missing level inside the mod; an existing folder is a success. New folders go under the mod's top folders, since the project root is read-only in the Editor.
- Definitions are not created under `AssetSources`, as in the Editor.
- A name already used in the target folder is an error, never the Editor's rename prompt or a generator's numbered alternative. Names collide by stem, as in the Editor's own check: two files of one stem would collide on processing.

## Prefab templates

- A template is an entry of the Editor's Create Prefab menu, read from the generator that builds the menu, so the list is exactly what the menu offers.
- `create_prefab` matches a template by its menu path in tiers from exact to word match, and never guesses at plurals: one match generates, several are listed.
- The name answers the name the template asks for, under the template's own name check.
- Nothing is written when a generated file would collide. After writing, assets whose processing failed are reprocessed, as the Editor's own create does, since some need files written after them.
- The reply lists every generated file with its type; each is a valid target.

## Template repairs

- Some vanilla templates fail the game's validation or make a block that cannot work, so every block made from them would carry an error the model did not cause. `create_prefab` puts the new block right after generating it, the way vanilla blocks of that family have it.
- General rules for whole kinds of defects, for example: a component definition that points at an abstract definition gets a copy of it in the prefab's folder, as vanilla blocks do; a mechanical block whose model lacks its pivot dummy gets one, because the game's check for it raises a modal assert in the Editor that holds validation.
- A per-template list in code covers what no rule fits; it runs before the general rules, so a better vanilla value wins.
- The reply lists files a repair created among the generated files and never mentions the repair: the model can do nothing with it, and the skills describe the repaired block as what the template brings.
- A defect found later follows the same recipe (`Tools_Overview.md`, Hand-kept corrections).

## Synthetic templates

- A vanilla family the Editor has no template for, whose vanilla blocks are copies of another template, gets a template that exists only in Veronica's tools.
- It is the source template's generator followed by a hand-kept recipe in the template repairs, after the source template's own repairs. The recipe sets only what every vanilla block of the family shares; values that differ per size stay as the source template has them, as in the Editor's own templates.
- Generated files named after the source template's family get the family's own vanilla names before they are written: a model reading the file list would take the source family's name for the block's.
- It is listed, matched and named like an Editor template, with no marker: the model could do nothing different with one. `component_upgrade` takes it as a base standing for its source template.
- Parts that work only with a model of their own are left out; the skills add them once the mod has the model.

## create_partial

- The Editor's Create Partial Definition: an **override** changes its source wherever it is used; a **copy** is a new definition that starts as its source. Unchanged values follow the source in both.
- The description says a copy belongs to no prefab and nothing uses it until something references it: a small model copied a block's definitions and edited the copies, which the block never used.
- Prefabs are refused (`Docs/TODOs/PartialPrefabs.md`).

## create_definition

- The Editor's Create Definition, with the type picker and the rename replaced by arguments. The type is never guessed: an unknown one lists candidates.
- The inspector's "Create New Definition" on a reference field is `create_definition` followed by `write`, so the model learns no extra syntax.

## create_asset

- The Editor's Create menu entries for assets made from input files. The kinds offered grow as each is tested in the Editor: a skybox so far.

## component_add

The inspector's Add Component on a block.

- **Offered types** are the inspector's, plus `HierarchyComponent`, which the inspector offers in developer mode only and vanilla subparts, door leaves and blocks with subparts carry.
- **Sides** follow the type, as in the Editor, with a hand-kept list of side corrections where the Editor gets a type's side wrong. The list also corrects the dependencies the Editor fills, since its filling sides a type by its own scope.
- **One component per role:** a type whose default tag another component on its sides already holds is refused, naming the holder, and `component_upgrade` when the type derives from the holder's. The Editor would move the tag and leave two components in one role, which crashed the Editor's post-processing.
- **Mismatched holder:** a dependency whose tag another component holds with a type that does not fit (a flame needing a PowerableBlockComponent on a block whose block component is a FunctionalBlockComponent) is named in the reply, with `component_upgrade` when the needed type derives from the holder's. The game's entity builder fails such a block at load even for an optional dependency. The add is not refused, since a refusal could deadlock the model.
- **Dependencies** are filled as the inspector fills them. A tag the filling gives to a component already on the block is named in the reply, since the model did not add it.
- **Companions:** a hand-kept list adds components that game validation, not a declared dependency, requires beside a type, so the Editor's filling never adds them (block permissions beside an inventory or a seat; without them the server refuses every player action on it).
- **Interface dependencies**, which the Editor's filling skips: a render parent dependency gets its adapter, since a block is always a child entity; any other is named in the reply with the components that provide it.
- **Automatic tag:** a component that comes in without a tag gets one. Systems find some components only by tag (a block's inventory in the conveyor graph), and a type allowed several times per entity has no default tag.
- A definition given with the add points at an existing item; nothing is created. The reply names every added component still lacking the definition its type needs, with the fix, since such a block fails validation.
