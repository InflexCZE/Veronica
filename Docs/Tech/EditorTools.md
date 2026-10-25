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

`add(a, b)` is removed when these land.

## Shared rules

- **Content items** are named by project-relative path, or by bare file name with the extension optional. A name resolves over the loaded mod and its dependencies. An ambiguous name is an error that lists the candidate paths. The model never sees GUIDs.
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
- Read and write reach visible fields only.

## read

- `read(target)` dumps the whole definition, and `read(target, path)` dumps one field or subtree.
- The output is indented text with one line per field: `Dummies[0].Name = "Nozzle" (String)`. References print the target's path, never a GUID.
- Dumps leave out read-only fields as computed noise. Reading a read-only field's exact path still shows it.
- A value type with at most 4 visible fields, all plain scalars, prints on one line: `Scale = {X: 0.1, Y: 0.1, Z: 0.1} (Vector3)`.
- An empty reference prints `none`. A null object prints `= null`.
- Reading an enum field by its exact path also lists the enum's values.
- Limits:
  - A list shows its first 5 elements, then `... (+N more, read "Dummies[5]" and on)`.
  - A dictionary shows its first 5 entries, then the remaining keys (up to 30).
  - Objects more than 3 levels below the requested node collapse to their field names, with a read hint.
  - A response stops at 150 lines with an explicit truncation notice.

## write

- `value` is a JSON value. A string is coerced to the field's declared type: numbers, bool, enum by name, StringId, localization key, fixed point.
- **Small structs:** a value type that read prints inline accepts the same form: `{X: 0.1, Y: 0.1, Z: 0.1}`. Fields left out keep their value.
- **References** (definitions, models, images): the value is a content item name or path. It is resolved like a target, among items compatible with the field's type only.
- **Clear:** `null` resets the field to its type's default: null for references and nullables, a fresh default for plain value types. It never removes an object or a list that is not nullable.
- **Null parents:** objects that are null along the path are created with their defaults first, like `create_folder` creates missing parents. If the write then fails, they are removed again. An abstract type cannot be created this way; the error names its concrete types. A null nullable struct also takes the one-line form directly.
- Numbers use a dot for decimals; a comma is refused, because invariant parsing would read it as a thousands separator.
- The reply is the path and the change: `UIData.Name: "ThrusterPlaceholder" -> "Modded Thruster"`, plus `(created Missing)` when parents were created.
- A field is writable the way the inspector allows: it has a setter, is not `[ReadOnlyInInspector]`, and no read-only struct contains it. Read-only fields are rejected. Read marks them `read-only`. A read-only object stays in dumps, because its fields can still be written.

## element_add and element_remove

- `element_add(target, path, key?, type?)` adds a default element and returns its path, for example `Dummies[2]` or `ThrustClasses[Water]`, followed by the element's fields. The model fills it with `write`.
  - A list gets the element at its end. A dictionary, or a keyed list, needs `key`; an existing key is an error that points at the entry to write instead.
  - A null collection, and null objects on its path, are created first.
  - An element of a definition-reference collection starts empty (`none`); `write` points it at an item.
  - `type` matters only for a collection whose element type is abstract. Without `type`, the error lists the types the inspector's add button offers for it (at most 50). `type` matches by short name, or by full name when short names collide.
- `element_remove(target, path)` removes the element at a path as read prints it: `Dummies[1]`, `ThrustClasses[Ion]` or `ThrustClasses.Ion`. The reply says that list elements after it moved up one index.
- Both go through the engine's own collection operations, so on a `.partialdef` they are recorded as overrides like any write.
- A collection without a setter is still edited in place; one marked `[ReadOnlyInInspector]` is not.

## find_content and list_folder

- `find_content` matches when every word of `query` is part of the file name or the definition type name (case-insensitive; spaces, `/`, `\`, `_` and `-` separate words), across the mod and its dependencies. "particle configuration" finds `ParticleEffectsBudgetConfiguration.def`.
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
- `find_template(filter?)` lists the templates whose path contains every word of `filter`. Without a filter it lists all of them. Capped at about 50.
- `create_prefab` resolves `template` in tiers and stops at the first tier with any match:
  1. the exact full path;
  2. the exact last segment ("Ion Thrusters");
  3. every word of `template` appears in the path.

  One match generates the prefab. Several matches are an error that lists them.
- `name` names the generated files: it answers the name the template asks for, and the template's own name check applies. A template that never asks (empty prefabs, single-file templates) has its files' shared name prefix replaced by `name`.
- `folder` is created like `create_folder` when missing; created folders stay even when generation then fails. Folders under `AssetSources` are refused, as in the Editor.
- Generation stops before writing any file when one of the generated file names already exists in the folder. (Generators dodge an existing name with ` (2)`; the tool refuses that instead.)
- After writing, generated assets whose processing failed are reprocessed, as the Editor's own create does: some need the files written after them.
- The reply is one line per generated file, with its project-relative path and definition type: `Assets/MyThruster/MyThruster_IonThrusterDefinition.partialdef (ThrusterDefinition)`. Each path is a valid target.

## Assemblies

| assembly | holds | tested by |
|---|---|---|
| `Veronica.Editing.dll` | path parsing, field walking, visibility, dump, value coercion, write, list-add, template tier matching, content name matching | `Veronica.Tests`, on engine types such as `ModelDummy` and on test types |
| `Veronica.dll` | the project layer (target and reference resolution over the project, read-only checks, saving, folder and prefab creation) and the `ITool` wrappers | by hand in the Editor: a full thruster build on the dev mod, reverted with git |

- `Veronica.Editing.dll` references only `VRage.Library` and `VRage.UI.Shared` from the Editor folder, and runs without the Editor. The project side of references (names to items, definition placeholders) comes in through an interface that `Veronica.dll` implements.
- `Veronica.Agent.dll` stays free of SE2 references.
- `Veronica.Tests` needs the Editor folder to build:
  - It copies the engine assemblies it references and the native `Optick.dll` the engine's type indexers start.
  - It sets `Keen.VRage.Library.DefaultMetadataContextAssemblies` to itself, because the engine indexes types from the entry assembly, which is the test host.
  - Test types are `[Serialize]` classes compiled by the serialization generator the Editor ships (`VRage.Library.Generator.dll`).
