# Editor tools: content authoring

The first set of agent tools that act on the loaded project. The target is building a placeable block from a prefab template (a thruster) end to end: find content, create a folder, generate the prefab, then read and edit the generated definitions.

## Tool set

| tool | does |
|---|---|
| `find_content(query, folder?)` | finds content items by name or definition type |
| `list_folder(folder_path)` | lists a folder's direct children |
| `read(target, path?)` | shows a definition, or one field or subtree of it |
| `write(target, path, value)` | sets a field: scalar, small struct, reference, or clear |
| `element_add(target, path, key?, type?)` | adds a default element to a list or dictionary |
| `element_remove(target, path)` | removes one list element or dictionary entry |
| `create_folder(folder_path)` | creates a folder and any missing parents |
| `find_template(filter?)` | lists prefab templates |
| `create_prefab(template, name, folder)` | generates a prefab's definition set from a template |
| `create_partial(source, kind, folder, name?)` | creates a partial override or copy of a definition |
| `create_definition(type, name, folder)` | creates a new definition of a type, with its defaults |
| `show(target)` | selects an item in the Editor and opens it in the Render View |

`add(a, b)` is removed when these land.

## Shared rules

- **Content items** are named by project-relative path, or by bare file name with the extension optional. A name resolves over the loaded mod and its dependencies. An ambiguous name is an error that lists the candidates with their projects. When the mod and a dependency hold the same path, the error says to lead the path with the project name, which picks one. The model never sees GUIDs.
- **Shortened paths:** a name is tried in this order, and the first step that finds anything decides: an exact path or bare name in any project; then, when the first segment names a loaded project (`Vanilla/Assets/...`, or `Vanilla:Assets/...` as models also write it), the rest exactly or as a path ending in that project; then the name as the end of an item's path on whole segments in any project: `Blocks/Ion/100/Model` and `Models/Model` name `Assets/Blocks/Ion/100/Models/Model.model`. An exact path always wins, so a project named like a folder never turns a full path into a guess. One match is taken, several are the usual ambiguity error. Small models drop the `Assets` prefix that find_content prints, or put the project in front, and repeated not-found errors sent them off building copies instead of reading the original. Replies and read headers always print the full path.
- **Folders** that must already exist (list_folder, find_content's folder) resolve the same way. find_content's folder also keeps the project it was found in, so `Vanilla/Assets/Blocks` searches Vanilla only. A folder to create (create_folder and the creating tools' folder) is taken exactly as given, since a shortened path there would put new files into some other folder. A project name alone (`Vanilla`, `Vanilla/`) names that project's root, but only after every step above found nothing, so a project named like a folder never hides that folder.
- **Not found:** the error names up to three items, or folders, whose last segment is spelled almost the same, ignoring case and the separators `.`, `_`, `-` and space, the mod's first on ties: `No content item named 'X.Client'; nearest: Assets\X\X_Client.partialdef in MyMod`. Nothing is named when nothing is close, and a name of one or two letters gets no hint, since every short name is near it. A target's hints are only items a target can be, never source files. A small model that rebuilt a name from memory, `X.Client` for `X_Client`, kept retrying the same wrong name after find_content had printed the right one.
- **Edits** go through the Editor's own definition data layer (`ObjectManipulator` / `MemberReference`), the same layer the Property Inspector uses. No UI is automated. For a `.partialdef`, reads see the merged definition and writes are recorded as overrides automatically.
- **Saving:** every successful change saves the way an inspector edit does, and an open Property Inspector shows the new value. There is no save tool. As with inspector edits, a failed disk write only reaches the Editor log.
- **No undo:** edits are not Ctrl-Z steps. The user reverts with version control.
- **Read-only:** the tools use the Editor's own flag, `ExplorerItem.CurrentReadOnly`. Edits check it on the target file and creation checks it on the target folder. When it is set, the error quotes its `Reason` (for example "Project read-only").
- **Busy files:** a file of the writable mod is locked only while the Editor works on it, for example for a few seconds after `create_prefab` wrote it ("File in use"). A change that finds its file locked retries every half second, waiting off the UI thread, and fails only after 15 s. Listings never show this passing lock.
- **Project:** every call uses the currently loaded project, reached through `VRageCore.Instance.Engine`. With none loaded, the tool says so.
- **Stop:** a call the user stopped before it reached the UI thread changes nothing.
- **Single items** outside a listing (the `read` header, error candidates) name their project when it is a read-only dependency: `path (Type) in Vanilla (read-only)`.
- **Errors** are short, name the bad part, and list the valid alternatives where there are any: sibling field names, candidate paths, enum names, templates, concrete types.
- **Threading:** tools run on a worker thread and do their project work on the UI thread.

## Field paths

- Dots for fields, brackets for collection elements: `UIData.Name`, `Dummies[0].Transform.Transform.Position.X`.
- A dictionary entry is `[key]` or `.key`: `ThrustClasses[Ion]` and `ThrustClasses.Ion` are the same entry. Output always prints `[key]`.
- A leading `$Root.` is accepted, so the Editor's own member paths can be pasted back as they are.
- Field names also match ignoring case, when that is unambiguous. Output prints the real name.

## Visible fields

- A field is visible when the Property Inspector would show it, as decided by the inspector's own `DefaultObjectInspectorVisibilityStrategy.ShouldGenerate` (HideInInspector, NoSerialize, dev-mode gating, public accessor).
- Fields marked `[SelectedItemOf]` are always hidden: they are UI selection mirrors and read as empty structs outside the live inspector.
- A short hardcoded list of fields is always visible: data the inspector hides because the Editor edits it in a dedicated window, which the model otherwise cannot see at all (a particle effect's emitters and lights).
- Read and write reach visible fields only.

## read

- `read(target)` dumps the whole definition, and `read(target, path)` dumps one field or subtree.
- The output is indented text with one line per field: `Dummies[0].Name = "Nozzle" (String)`. References print the target's path, never a GUID.
- Dumps leave out read-only fields as computed noise. Reading a read-only field's exact path still shows it.
- A value type with at most 4 visible fields, all plain scalars, prints on one line: `Scale = {X: 0.1, Y: 0.1, Z: 0.1} (Vector3)`.
- A transform (a struct of a position and a quaternion, such as a dummy's `Transform.Transform`) prints on one line too, as the inspector shows it: the position and Euler angles in degrees, never the quaternion: `{Position: {X: 0.125, Y: 0.125, Z: 0.646}, Euler: {X: 0, Y: 0, Z: 0}}`. A pointer to read further reads to a small model as a transform with no position; the quaternion beside Euler angles would say the same rotation twice. Euler angles near X = +-90 degrees are in gimbal lock, where Y and Z can show as 180 instead of 0, as in the inspector; writing only some axes keeps the others as shown. A transform cannot be written `null`, since its default holds a zero quaternion.
- A wrapper of one transform and its Euler hint, such as a dummy's `Transform`, is the same transform: it prints, writes and takes `.Euler` and `.Position` as the transform inside it does, and a dump shows only the wrapper's line, one line per dummy. Mirroring a dummy is one write to `Dummies[i].Transform`; refused as a whole, the wrapper cost a model rounds of field-by-field writes. The inner `...Transform.Transform` path still works.
- The Euler angles are the stored Euler hint while it still matches the quaternion, else angles derived from the quaternion, the same rule as the inspector's. Euler hint fields themselves are hidden, as the inspector hides them.
- A colour prints as its channels, `{R: 255, G: 0, B: 0, A: 255} (ColorSRGB)`, and its packed value is hidden: shown beside the channels, a model wrote both and got the packing wrong.
- An empty reference prints `none`. A null object prints `= null`.
- A path never walks into a reference: `Effect.ParticleLight` on a field that points at another definition is refused with a pointer to read that item. Through the reference sits the engine's runtime object, not the file, and walking it gave placeholders and raw exceptions.
- A prefab binding (the item a prefab is named by) shows no fields of its own, so reading it lists the block's definitions instead, in `create_prefab`'s form: its prefabs, their compositions and the component definitions those point at, from the binding's own project only, since components shared with other blocks are not part of this one. Reading a field path on it, or writing or editing elements on it, fails with the same list. An empty definition reads to a small model as the place to look for the block's fields.
- Reading an enum field by its exact path also lists the enum's values.
- Limits:
  - A list shows its first 5 elements, then `... (+N more, read "Dummies[5]" and on)`.
  - A dictionary shows its first 5 entries, then the remaining keys (up to 30).
  - Objects more than 3 levels below the requested node collapse to their field names, with a read hint.
  - A response stops at 150 lines with an explicit truncation notice.

## write

- `value` is a JSON value. A string is coerced to the field's declared type: numbers, bool, enum by name, StringId, localization key, fixed point.
- **Small structs:** a value type that read prints inline accepts the same form: `{X: 0.1, Y: 0.1, Z: 0.1}`. Fields left out keep their value. A colour takes `{R, G, B, A}` the same way, so a left-out alpha is kept.
- **Transforms** take the printed form, `{Position: {...}, Euler: {...}}`; either part, and any axis, may be left out and keeps its value. The Euler angles set the quaternion, and the Euler hint beside it is updated as the inspector does, so the inspector shows the angles that were written. The position's own path (`...Transform.Position.Z`) still writes one value.
- **Euler path:** `...Transform.Euler` and `...Transform.Euler.X` read and write the angles in degrees like the `Euler` part of the one-line form. They are not fields; the path ends at the transform and names its angles.
- **The quaternion is not addressable:** read and write refuse its path, and a transform's field list is `Position, Euler`. A raw quaternion written as if it were angles is unnormalized and turns the rotation into something else; only the angles reach it.
- **Reference values** point at an existing item, in the mod or in a read-only project; nothing is copied or created. The tool description says so, because a small model otherwise made its own copies of an item it could point at.
- **References** (definitions, models, images): the value is a content item name or path. It is resolved like a target, among items compatible with the field's type only.
- **Clear:** `null` resets the field to its type's default: null for references and nullables, a fresh default for plain value types. It never removes an object or a list that is not nullable.
- **Null parents:** objects that are null along the path are created with their defaults first, like `create_folder` creates missing parents. If the write then fails, they are removed again. An abstract type cannot be created this way; the error names its concrete types. A null nullable struct also takes the one-line form directly.
- Numbers use a dot for decimals; a comma is refused, because invariant parsing would read it as a thousands separator.
- The reply is the path and the change: `UIData.Name: "ThrusterPlaceholder" -> "Modded Thruster"`, plus `(created Missing)` when parents were created.
- A field is writable the way the inspector allows: it has a setter, is not `[ReadOnlyInInspector]`, and no read-only struct contains it. Read-only fields are rejected. Read marks them `read-only`. A read-only object stays in dumps, because its fields can still be written.

## element_add and element_remove

- `element_add(target, path, key?, type?)` adds a default element and returns its path, for example `Dummies[2]` or `ThrustClasses[Water]`, followed by the element's fields. The model fills it with `write`.
  - A list gets the element at its end. A dictionary, or a keyed list, needs `key`; an existing key is an error that points at the entry to write instead.
  - A mergeable list (`MergeableList`, how definitions such as `Dummies` store their lists) keys its elements with new ids itself, as the inspector's add button relies on, so it takes no key; one that is given is ignored.
  - An `ImmutableMergeableList` field is read, written and grown through the list it wraps, as the inspector shows it: it looks like a plain list.
  - A null collection, and null objects on its path, are created first.
  - An element of a definition-reference collection starts empty (`none`); `write` points it at an item.
  - `type` matters only for a collection whose element type is abstract. Without `type`, the error lists the types the inspector's add button offers for it (at most 50). `type` matches by short name, or by full name when short names collide.
- `element_remove(target, path)` removes the element at a path as read prints it: `Dummies[1]`, `ThrustClasses[Ion]` or `ThrustClasses.Ion`. The reply says that list elements after it moved up one index.
- Both go through the engine's own collection operations, so on a `.partialdef` they are recorded as overrides like any write.
- A collection without a setter is still edited in place; one marked `[ReadOnlyInInspector]` is not.

## find_content and list_folder

- `find_content` matches when every word of `query` is part of the file name or the definition type name (case-insensitive; spaces, `/`, `\`, `_` and `-` separate words), across the mod and its dependencies. "particle configuration" finds `ParticleEffectsBudgetConfiguration.def`.
  - The definition type's base types match too, so `CubeBlockDefinition` with a block's folder finds its block definition, whatever derived type it is. A skill that said "the type ending in BlockDefinition" matched two of a block's definitions and cost a model a dozen rounds of searching.
  - `folder` limits the search to that folder's subtree.
  - Output is grouped by project, the mod first and then its dependencies in load order. Each group has one header naming the project and whether it is read-only, then `path (DefinitionType)` lines. Capped at 50 items with a truncation marker.
- `list_folder` lists direct children only.
  - When the path exists in several projects (the mod and Vanilla), there is one section per project.
  - Subfolders print as `Name\ (N items)` and files as `Name (DefinitionType)`.
  - Capped at about 100 lines.

## create_folder

Creates every missing level of `folder_path` inside the mod. An existing folder is a success ("already exists"). The project root itself is read-only in the Editor, so new folders go under its top folders, such as `Assets`.

## Templates

- A template is an entry of the Editor's Create Prefab menu, named by its menu path below the menu's own top group, for example `Blocks/Breakable/Ion Thrusters`. The list is read from the generator that builds the menu, so it is exactly what the menu offers, empty prefabs included.
- Matching is case-insensitive, and `/`, `\` and runs of spaces separate words.
- `find_template(filter?)` lists the templates whose path contains every word of `filter` as a substring ("thrust" finds "Ion Thrusters"). Without a filter it lists all of them. Capped at about 50.
- `create_prefab` resolves `template` in tiers and stops at the first tier with any match:
  1. the exact full path;
  2. the exact last segment ("Ion Thrusters");
  3. every word of `template` is a whole word of the path (no guessing at plurals when creating).

  One match generates the prefab. Several matches are an error that lists them.
- `name` names the generated files: it answers the name the template asks for, and the template's own name check applies. A template that never asks (empty prefabs, single-file templates) has its files' shared name prefix replaced by `name`.
- `folder` is created like `create_folder` when missing; created folders stay even when generation then fails. Folders under `AssetSources` are refused, as in the Editor.
- Generation stops before writing any file when one of the generated file names already exists in the folder. (Generators dodge an existing name with ` (2)`; the tool refuses that instead.)
- After writing, generated assets whose processing failed are reprocessed, as the Editor's own create does: some need the files written after them.
- The reply is one line per generated file, with its project-relative path and definition type: `Assets/MyThruster/MyThruster_IonThrusterDefinition.partialdef (ThrusterDefinition)`. Each path is a valid target.

## create_partial

- The Editor's "Create Partial Definition" menu, through the Editor's own generator, so the Editor's rules apply and come back as its errors: an override only of a read-only dependency's definition, one override per definition, no copies of configurations.
  - **Override** changes the source wherever it is used.
  - **Copy** is a new definition that starts as the source.
  - In both, unchanged values follow the source.
  - The description says a copy belongs to no prefab and nothing uses it until something references it, so a new block's values go on the definitions `create_prefab` listed: a small model copies a block's definitions and edits the copies, which the block never uses.
- Prefabs are refused (`Docs/TODOs/PartialPrefabs.md`).
- `name` is optional: the default is the Editor's own suggestion (`Override of <source>`).
- A name already used in the folder is an error rather than the Editor's rename prompt.

## create_definition

- The Editor's "Create Definition" menu, with the type picker and the rename replaced by `type` and `name`. It offers the menu's types and builds the definition as the menu does.
- `type` is never guessed: an unknown type lists the types that contain its words.
- The inspector's "Create New Definition" on a reference field is `create_definition` followed by `write`, so the model learns no extra syntax.

## show

- What a click and a double-click on the item do: select it in the Project Explorer and Property Inspector, and open it in the Render View.
- The selection and the open are internal to the Editor, so the tool reaches them by reflection.
- An item without a preview of its own is opened through the asset that owns it, as a prefab is through its prefab binding. The model names any file of the thing and the tool finds the preview.
- An item with no preview at all is only selected, and the reply gives the Editor's reason.
- Items that open their own editor or an external app (FMOD projects, particle effects, ...) are only selected; opening them is the user's call.

## Assemblies

| assembly | holds | tested by |
|---|---|---|
| `Veronica.Editing.dll` | path parsing, field walking, visibility, dump, value coercion, write, list-add, template tier matching, content name matching, definition type matching | `Veronica.Tests`, on engine types such as `ModelDummy` and on test types |
| `Veronica.dll` | the project layer (target and reference resolution over the project, read-only checks, saving, folder and prefab creation) and the `ITool` wrappers | by hand in the Editor: a full thruster build on the dev mod, reverted with git |

- `Veronica.Editing.dll` references only `VRage.Library` and `VRage.UI.Shared` from the Editor folder, and runs without the Editor. The project side of references (names to items, definition placeholders) comes in through an interface that `Veronica.dll` implements.
- `Veronica.Agent.dll` stays free of SE2 references.
- `Veronica.Tests` needs the Editor folder to build:
  - It copies the engine assemblies it references and the native `Optick.dll` the engine's type indexers start.
  - It sets `Keen.VRage.Library.DefaultMetadataContextAssemblies` to itself, because the engine indexes types from the entry assembly, which is the test host.
  - Test types are `[Serialize]` classes compiled by the serialization generator the Editor ships (`VRage.Library.Generator.dll`).
