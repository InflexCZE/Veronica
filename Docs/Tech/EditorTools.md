# Editor tools: content authoring

The first set of agent tools that act on the loaded project. The target is building a placeable block from a prefab template (a thruster) end to end: find content, create a folder, generate the prefab, then read and edit the generated definitions.

## Tool set

| tool | does |
|---|---|
| `find_content(query, folder?)` | finds content items by name or definition type |
| `find_references(target)` | lists the content items that point at an item, with the field that holds each reference |
| `list_folder(folder_path)` | lists a folder's direct children |
| `read(target, path?)` | shows a definition or cubemap, or one field or subtree of it |
| `contains(target, value, path?)` | lists where a definition holds a value, without reading its long lists, sets and dictionaries |
| `describe(subject, member?)` | explains a type from the game's own documentation: its members, relations and units |
| `write(target, path, value)` | sets a field: scalar, small struct, reference, or clear |
| `element_add(target, path, key?, type?, value?)` | adds an element to a list, dictionary or set |
| `element_remove(target, path)` | removes one list element, dictionary entry or set element |
| `create_folder(folder_path)` | creates a folder and any missing parents |
| `find_template(filter?)` | lists prefab templates |
| `create_prefab(template, name, folder)` | generates a prefab's definition set from a template |
| `create_partial(source, kind, folder, name?)` | creates a partial override or copy of a definition |
| `create_definition(type, name, folder)` | creates a new definition of a type, with its defaults |
| `create_asset(kind, folder, name?, inputs?)` | creates an asset from input files through the Editor's Create menu: a skybox from six face images |
| `component_add(prefab, type, definition?)` | adds a component to a block, on the sides its type belongs to, with its dependencies |
| `component_remove(prefab, component)` | removes a component from a block |
| `show(target)` | selects an item in the Editor and opens it in the Render View |
| `present_graph(nodes, edges)` | shows the user a graph of content items and their references on the Board |

`add(a, b)` is removed when these land.

## Shared rules

- **Content items** are named by project-relative path, or by bare file name with the extension optional. A name resolves over the loaded mod and its dependencies. An ambiguous name is an error that lists the candidates with their projects. When the mod and a dependency hold the same path, the error says to lead the path with the project name, which picks one. The model never sees GUIDs.
- **Shortened paths:** a name is tried in this order, and the first step that finds anything decides: an exact path or bare name in any project; then, when the first segment names a loaded project (`Vanilla/Assets/...`, or `Vanilla:Assets/...` as models also write it), the rest exactly or as a path ending in that project; then the name as the end of an item's path on whole segments in any project: `Blocks/Ion/100/Model` and `Models/Model` name `Assets/Blocks/Ion/100/Models/Model.model`. An exact path always wins, so a project named like a folder never turns a full path into a guess. One match is taken, several are the usual ambiguity error. Small models drop the `Assets` prefix that find_content prints, or put the project in front, and repeated not-found errors sent them off building copies instead of reading the original. Replies and read headers always print the full path.
- **Folders** that must already exist (list_folder, find_content's folder) resolve the same way. find_content's folder also keeps the project it was found in, so `Vanilla/Assets/Blocks` searches Vanilla only. A folder to create (create_folder and the creating tools' folder) is taken exactly as given, since a shortened path there would put new files into some other folder. A project name alone (`Vanilla`, `Vanilla/`) names that project's root, but only after every step above found nothing, so a project named like a folder never hides that folder.
- **Not found:** the error names up to three items, or folders, whose last segment is spelled almost the same, ignoring case and the separators `.`, `_`, `-` and space, the mod's first on ties: `No content item named 'X.Client'; nearest: Assets\X\X_Client.partialdef in MyMod`. Nothing is named when nothing is close, and a name of one or two letters gets no hint, since every short name is near it. A target's hints are only items a target can be, never source files. A small model that rebuilt a name from memory, `X.Client` for `X_Client`, kept retrying the same wrong name after find_content had printed the right one.
- **Assets with fields:** read, write, element_add, element_remove, contains, describe and find_references take definitions and a short list of other asset kinds whose data is plain serialized members, edited through the same `ObjectManipulator` the inspector uses: cubemaps so far. Kinds join the list as each is tested in the Editor; anything else is refused with the list. Plain images have no fields.
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
- A dictionary keyed by a definition (the camera system maps a block's client composition to its cameras) takes the definition's name as key, resolved like a reference value, and prints its path: `CompositionToCameraPrefabs[Assets\MyCockpit\MyCockpit_ClientComposition.partialdef]`. Paths hold dots, so only the bracket form reaches such an entry. Engine definitions compare by reference, so an existing key is matched by its id.
- A leading `$Root.` is accepted, so the Editor's own member paths can be pasted back as they are.
- Field names also match ignoring case, when that is unambiguous. Output prints the real name.

## Visible fields

- A field is visible when the Property Inspector would show it, as decided by the inspector's own `DefaultObjectInspectorVisibilityStrategy.ShouldGenerate` (HideInInspector, NoSerialize, dev-mode gating, public accessor).
- Fields marked `[SelectedItemOf]` are always hidden: they are UI selection mirrors and read as empty structs outside the live inspector.
- A short hardcoded list of fields is always visible: data the inspector hides because the Editor edits it in a dedicated window, which the model otherwise cannot see at all (a particle effect's emitters and lights).
- Inside a component's object builder the inspector's stricter prefab rule applies on top: a field is visible only when `[ForceShowInInspector]` sits on it, its type or an object above it, with no member lacking a public setter in between. Everything else there is internal engine data.
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
- A prefab, its binding and its compositions read as the block's component list (see Prefab components).
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
- **Game validation hint:** the written field's own validation attributes run through the engine's validation, and what they report is added to the reply: `MaxBroadcastRange: 5000 -> 0 (game validation: Value 0 is outside of the expected range (0, 50000].)`. The value is written all the same: two fields can bound each other (a battery's charge rate below its discharge rate), and refusing either write would deadlock. A value in range adds nothing, since a report after every write would confuse a small model.
  - Checked: number ranges, string lengths and unit vectors, including bounds a block component takes from its definition. Time ranges are not, as the game reports them in ticks. A field inside a struct is checked with the struct, so a unit vector written one axis at a time is caught.
  - A bound held by another field is named with that field's current value, so the model knows which value to correct: `... [0, MaxDischargeRate). MaxDischargeRate is 3.` A bound on a component's definition says so (`MaxAffectDistance on the component's definition is 50.`), and a value just written to that definition counts at once.
  - Only the written field is checked, not fields whose bounds name it.
- A field is writable the way the inspector allows: it has a setter, is not `[ReadOnlyInInspector]`, and no read-only struct contains it. Read-only fields are rejected. Read marks them `read-only`. A read-only object stays in dumps, because its fields can still be written.

## element_add and element_remove

- `element_add(target, path, key?, type?, value?)` adds a default element and returns its path, for example `Dummies[2]` or `ThrustClasses[Water]`, followed by the element's fields. The model fills it with `write`.
  - A list gets the element at its end. A dictionary, or a keyed list, needs `key`; an existing key is an error that points at the entry to write instead.
  - A mergeable list (`MergeableList`, how definitions such as `Dummies` store their lists) keys its elements with new ids itself, as the inspector's add button relies on, so it takes no key; one that is given is ignored.
  - An `ImmutableMergeableList` field is read, written and grown through the list it wraps, as the inspector shows it: it looks like a plain list.
  - A null collection, and null objects on its path, are created first.
  - An element of a definition-reference collection starts empty (`none`); `write` points it at an item.
  - `value` is written to the new list or dictionary element at once, so a single-value entry (a dictionary from item to definition) takes one call; a value that does not fit removes the element again, and the error says nothing was added: told only how to write the element's fields, a model took the element as kept.
  - A set's element is the element itself: given as `key` or as `value`, whichever the model passes, and parsed like a dictionary key of the same type, so a definition is named by its path or name. Set elements are addressed like dictionary keys, `Blocks[<path>]`; `read` lists them, and an element already there is an error naming it. A set element cannot be written: it is removed and the new one added.
  - `type` matters only for a collection whose element type is abstract. Without `type`, the error lists the types the inspector's add button offers for it (at most 50). `type` matches by short name, or by full name when short names collide.
- `element_remove(target, path)` removes the element at a path as read prints it: `Dummies[1]`, `ThrustClasses[Ion]`, `ThrustClasses.Ion` or a set's `Blocks[<path>]`. The reply says that list elements after it moved up one index.
- Both go through the engine's own collection operations, so on a `.partialdef` they are recorded as overrides like any write. A set on a partial is stored as what was added and removed, so adding to an override's set keeps the base's elements.
- A collection without a setter is still edited in place; one marked `[ReadOnlyInInspector]` is not.

## Prefab components

A block is a prefab binding that pairs a client prefab with a server composition. Each side is a composition (the component list: type, component definition, tags) and a prefab (each component's object builder), joined by one Guid per component. A component with the same Guid on both sides is one component on both sides.

- **The block is the unit.** The tools act on the block through the Editor's own prefab layer (`PrefabManipulator.LoadBinding`, `BoundEntitiesNode`), the layer behind the inspector's prefab view, so client/server pairing, dependency filling and saving follow the inspector's rules.
- **Tolerant target:** any of the block's files names the block: the binding, either prefab, either composition. A prefab or composition with a binding is replaced by its binding; replies print the binding's path. A prefab with no binding is edited the same way, as a one-sided block. The model is not expected to work on single prefabs, and a target it gets wrong this way would otherwise cost rounds.
- **Naming a component:** by its type's short name, `Components[ThrusterComponent]`. A type present more than once is named by its tag; a component with no tag of its own is named by the first 8 hex digits of its Guid, `Components[#1a2b3c4d]`, longer only when two share a prefix. Read prints the name every tool takes.
- **Component paths:** `Components[X].Definition` is the component's definition reference; the component's visible object builder fields sit beside it, `Components[X].Field`. `Components[X].Tags` is the component's tags (see Tags). If an object builder has its own field named `Definition` or `Tags`, the path still means the definition reference or the tags. `read`, `write`, `element_add` and `element_remove` take these paths on the block. Which side's value a component on both sides shows, and how an edit reaches both sides, is the prefab layer's, as in the inspector.
- **read(block)** lists the components and nothing else, one line each: type, sides, tags, definition.
  ```
  ThrusterComponent (Client and Server) tags: Thruster  definition: Assets/X/X_IonThrusterDefinition.partialdef
  ThrusterFlameComponent (Client) tags: Flame  needs ThrusterFlameDefinition
  ```
  The sides print as `Client`, `Server` or `Client and Server`. A path on the block that is not under `Components` fails with the same list.
- **component_add(prefab, type, definition?)** is the inspector's Add Component:
  - The offered types are the inspector's: concrete component types whose side fits the block and whose object builders serialize, minus types already present unless they allow several. `HierarchyComponent`, which the inspector offers in developer mode only, is offered too: vanilla subparts, door leaves and blocks with subparts carry it. An unknown type lists the offered types containing its words.
  - The type's side decides where it goes: `[ServerOnly]` to the server, a client assembly's type to the client, anything else to both.
  - **Side overrides:** a short hand-kept list in Veronica corrects the side of types the Editor gets wrong, an Editor bug: `InventoryComponent` goes to the server only, as in every vanilla block, though the Editor would put it on both. Types join the list as they are found. It applies to dependencies too: the Editor's dependency filling sides a listed type by its own scope, so it would add one on both sides or make a server-only one shared (adding `InventoryBlockDestructionExplosionComponent` did that to the inventory); the other side's half is dropped again. Unlisted dependencies keep the Editor's sides.
  - **One component per role:** a type whose default tags another component on its sides already holds is refused, naming the holder. The Editor would move the tag to the new component and leave two components in one role: a PowerableBlockComponent added next to a FunctionalBlockComponent took GridBlockComponent, and the two block definitions crashed the Editor's breakable post-processing. Replacing a block's component is not a tool operation (`Docs/TODOs/ReplaceBlockComponent.md`).
  - Missing required dependencies are added with it, on their own sides, as the inspector does. A tag the filling gives a component already on the block is named in the reply, since the model did not add it.
  - **Companions:** a short hand-kept list adds components that game validation, not a declared dependency, requires next to a type, so the Editor's filling never adds them. A block with an `InventoryComponent` or a `SeatComponent` and no `BlockPermissionsComponent` gets one on the server, named among the added dependencies: two validators reject such a block, and the server refuses every inventory or seat action players take on it. `BlockPermissionsComponent` is on the side-override list as server only, as in every vanilla block.
  - **Interface dependencies:** the Editor's filling skips a dependency declared as an interface, which several components may provide. On a block, always a child entity, an `IRenderParent` dependency gets `ParentRenderAdapterComponent`; any other unfilled one is named in the reply with the components that provide it.
  - **Automatic tag:** a component that comes in with no tag of its own gets one: its type's name without the `Component` suffix and the lowest number not taken on its sides, `Inventory1`. Systems find some components only by tag, such as a block's inventory in the conveyor graph, and a type that allows several per entity has no default tag.
  - `definition` points the component at an existing item, resolved like a reference write among items of the component's definition type. Nothing is created.
  - The reply names the added component, its sides, the dependencies added with it, and every component of them still without a definition its type needs, with the fix: create one with `create_definition` and write it to `Components[X].Definition`. A component without its definition fails the block's validation.
- **Tags:** `Components[X].Tags` reads as a list of the component's tags. `element_add(target, "Components[X].Tags", key)` adds a tag and `element_remove(target, "Components[X].Tags[T]")` removes one; there is no array write. Both are refused when they would break the block, and the error names the component involved: adding a tag another component on the same side holds would move it away, since a side keeps one component per tag; removing a tag another component requires would break that dependency at load. Moving a tag from one component to another is not possible while the old holder's dependents need it (`Docs/TODOs/PrefabTagMove.md`).
- **component_remove(prefab, component)** removes the component from every side it is on.
  - It is refused when another component on the same side has a required dependency on a tag the component fills; the error names those components. Removing it would break the block at load.
  - The component's definition file is left in place. When nothing else in the project references it any more, the reply says it is unused.
- **Scope:** the root entity only (`Docs/TODOs/PrefabChildEntities.md`). A block's side toggles are not tool operations. Overrides of a dependency's prefab are not made by the tools (`Docs/TODOs/PartialPrefabs.md`).

## find_content and list_folder

- `find_content` matches when every word of `query` is part of the file name or the definition type name (case-insensitive; spaces, `/`, `\`, `_` and `-` separate words), across the mod and its dependencies. "particle configuration" finds `ParticleEffectsBudgetConfiguration.def`.
  - The definition type's base types match too, so `CubeBlockDefinition` with a block's folder finds its block definition, whatever derived type it is. A skill that said "the type ending in BlockDefinition" matched two of a block's definitions and cost a model a dozen rounds of searching.
  - `folder` limits the search to that folder's subtree.
  - Output is grouped by project, the mod first and then its dependencies in load order. Each group has one header naming the project and whether it is read-only, then `path (DefinitionType)` lines. Capped at 50 items with a truncation marker.
- `list_folder` lists direct children only.
  - When the path exists in several projects (the mod and Vanilla), there is one section per project.
  - Subfolders print as `Name\ (N items)` and files as `Name (DefinitionType)`.
  - Capped at about 100 lines.

## find_references

Lists what points at one item, one step: "which effects use this emitter", "which blocks use this model". Without it, research into what an override would also change swept folder after folder: 15 to 34 calls, 14K to 33K of context and up to 9 minutes per question. The sweeps then claimed "nothing else uses it" after covering only the thruster folders.

- The referrers come from the Editor's own reference index, the one behind the Content Browser's referencing search. It covers definitions, assets, partial copies and overrides, and the mod with its dependencies. So the count is complete, and the description says that a short list means nothing else uses the item. An empty or disabled index is an error, never an empty list.
- The index names files, not fields. Each referrer is then walked as `read` walks a definition, without read's cuts on elements, depth and lines, and every field or definition key that points at the target is listed by the path `read` and `write` take. A partial whose base is the target says "copy of it" or "override of it". A referrer whose field the walk does not find still gets its row ("refers to it"), so the count stays true.
- A block's files (binding, prefabs, compositions) make one row for the block, with component paths such as `Components[ThrusterEffectsComponent].Definition`, as `read` shows a block.
- A partial's link to its base is not always in the index, so the target's own copies and overrides are added from the Editor's list of partials. A set's elements have no path of their own, so a reference among them names the set. Read-only reference fields are walked too: the index counts them.
- An override as the target is searched as its original, since every reference to the original gets the override's values; the reply says so. A block as the target is searched through all its files.
- One step only: the model follows a chain by calling it on a referrer. A chain in one call would dump whole dependency trees.
- Output as find_content's: grouped by project, the mod first, each row `path (DefinitionType): fields`. The header counts every row; the first 50 are shown.
- Read-only, so the researcher has it too.

## contains

Whether one definition holds a value: the survival unlock configuration's Blocks set holds dozens of prefabs, and read shows a set's first 5 elements and names at most 30 more, so a model reading it to look for one block reads far past what it needs, or stops early.

- Walks the definition, or the subtree at `path`, as find_references walks a referrer: without read's cuts on elements, depth and lines. A `.partialdef` is searched merged, as read shows it. Without `path`, the whole definition.
- Candidates are list and set elements, dictionary keys and values, and the fields of elements, inline structs' fields included. Each match prints as read prints it, `Classes[Hydrogen].Speed = 2` or a set's `Blocks[<path>]`, so a match is a path write and element_remove take.
- A value that names content items, by exact path or name as a target is resolved, matches every reference to them. A block matches through all its files: the binding, both prefabs and their compositions. The reply lists only what is there; the skill that uses it says which file counts, since survival unlocks take the server prefab only.
- Text matches whole values ignoring case; a reference also by its file name, with or without extension. With no whole match, the values that contain the text are listed under `No exact match; similar:`. A model that half-remembers a name (`GatlingCannon` for `GatlingCannonBlockKind`) gets it in one call instead of a not-found.
- Nothing found says what was searched and how many values, so an empty answer reads as a search that ran, not one that failed.
- A result is never cut without a word. At most 20 matches are listed; the reply then counts the rest and tells the model to narrow the search with a path or a more exact value. A value naming more than 10 items is matched as text only, and the reply says so and asks for the item's path or full file name: a vague name would otherwise match every reference in the definition.
- Read-only, so the researcher has it too.

## describe

What a field or type means, from the game's own documentation, from the two sources the Property Inspector uses:
- the `<summary>` doc comments the serialization generator bakes into every serializable type's `AccessibleTypeInfo`, shown as field tooltips. They need no doc files. Other doc tags (`remarks`, `seealso`) are dropped by the generator.
- the XML doc files beside the game's assemblies, read through the Editor's `DocsService` for the inspector's Documentation section of a component. A type summary comes from them when none is baked in: a component's own class is not serializable, so only the files carry its doc.

- **Subject:** a type name or a content item.
  - A type is any type the inspector can show (it has an `AccessibleTypeInfo`), or a component type. A short name is enough; an ambiguous one lists the full names and an unknown one lists the types containing its words, as `create_definition` does.
  - A definition or component type is shown under its own name with the fields of its object builder, the fields `read` and `write` take. An object builder's name resolves to the same type.
  - A component's fields are listed as the inspector shows them inside a prefab, as `read` does on a block.
  - The type names come from a scan of the engine's and game's assemblies: the attribute index misses plain serializable types such as `ModelDummy`.
  - A definition item describes its definition type. A block (any of its files) lists its components, each with its type's summary and the definition type it needs.
  - The header names the type the subject resolved to, so the model learns the name for later calls.
- **member:** a field path in `read`'s format, indexes included, so the model never meets a second path syntax.
  - On a content item the path walks the values that are there, as `read` does: an element of a polymorphic list is described as the type it actually holds, and a missing index is `read`'s error.
  - On a type name there is no value, so a step walks the declared type, and an index walks to the declared element type.
  - On a block, `Components[X]` is that component's type, its object builder fields and the definition type it needs.
  - A transform is its `Position` and `Euler` angles, as in `read`; `...Euler` and `...Euler.X` are described as angles in degrees, and the quaternion is never listed.
  - A dictionary entry is `[key]` or `.key`. A step through a reference, an element that is a reference included, is refused with a pointer to describe the referenced type.
- **Without member:** the type's summary, its relations and its member list. Only member names are listed, never their summaries: details come from a member query, so a listing does not flood the context.
  - One line per visible field: `ThrustPower: float, N*s`, ` (read-only)` where `read` marks it, a reference as `Effect: ref ParticleEffectDefinition`. Visibility is `read`'s.
  - The type's own fields first, then one group per base type, nearest first. No character budget, since lines are short; `read`'s 150-line cap applies.
- **Relations:** the base types; the concrete derived types; for a component, the definition type it takes; for a definition type, the components that take it. Further relations (tag dependencies, ranges) wait for eval evidence.
- **Units:** the unit a field declares (`IUnitAttribute`), as its symbol. A unit is a label only: nothing in the Editor converts values, so the stored value is in that unit. Power and resource amounts mostly declare none.
- **Overrides:** a short hand-kept list in Veronica corrects what the game declares wrongly or leaves out about a member, a game bug: a unit attribute that names another quantity or is missing where the game's own screens show one, or a doc comment that describes another field. An entry gives the unit to show (or none) and the summary to show instead. Entries join as they are confirmed in the game code, and leave when the game is fixed.
- **With member:** the member's line, its full summary, then the listing of the member's type: the element type for a list or dictionary, the referenced definition type for a reference. One call drills one level. A scalar stops after its summary; an enum also lists its values.
- **One-value structs:** a struct that `read` and `write` take as one plain value is described as that value, as a type subject and after a member of its type, never by its storage. `FixedPoint` is a decimal number in the unit of the field holding it; its own doc and `RawValue` field led a model to take the number `read` shows for a raw count and write it a million times too large.
- **Abstract types:** a type subject or a declared element type that is abstract has no fields of its own to list; the reply lists its concrete types to describe next.
- **Doc markup** is rendered as plain text by the inspector's own rules (`XmlToRichTextConverter`): a `<see cref>` becomes its short name, `<br/>` a new line, other tags their text. Links to web documents (`<see href>`, `<seealso href>`) are left out: the model cannot open them, and their titles alone read as stray lines. A member with no doc gets no summary line.
- Read-only, so the researcher and MCP clients have it too.

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
- Generation stops before writing any file when a file in the folder already has the name of one of the generated files, extension aside (the Editor compares stems: `Leaf.def` and `Leaf.partialdef` would collide on processing). (Generators dodge an existing name with ` (2)`; the tool refuses that instead.)
- After writing, generated assets whose processing failed are reprocessed, as the Editor's own create does: some need the files written after them.
- **Template repairs:** some vanilla templates fail the game's validation as they are, or make a block that cannot work, so every block made from them would carry an error the model did not cause. Veronica cannot change vanilla; create_prefab puts the new block right after generating it, the way vanilla's own blocks of that family have it. The reply lists any file a repair creates among the generated files and never mentions the repair: the model can do nothing with that information, and the skills describe the repaired block as what the template brings.
  - **General rule:** a component definition that points at a definition flagged Abstract gets a Copy partial of it in the prefab's folder, named `<name>_<the base's file name>`, and points at the copy, as vanilla blocks do (Landing Gear's cooldown and its detector).
  - **Mechanical blocks:** a block with a MechanicalBlockComponent (Connectors, Hinges, Pistons, Rotors and their heads) whose model definition has no Pivot dummy gets one at the default transform; the model places it. The game's check for that dummy raises a modal assert dialog in the Editor instead of a validation error, and the dialog holds validation until someone answers it.
  - **Per-template list,** applied before the rule, for what has no rule: a value vanilla blocks of the family use, or a component vanilla removes. Sensor: the client's BlockToolbarControllerComponent is removed, as vanilla sensors remove it. Reactors and Tanks: the explosion definition gets the Explosion of Reactor250 and HydrogenTank500. Reactors also get their own copy of the shared definition their FunctionalBlockDestructionExplosionComponent points at, with Reactor250's Explosion, as vanilla reactors have: the shared one is every template reactor's blast, and the generated explosion definition is used by nothing. Drills: the effects component points at Drill350's effects instead of the general rule's empty copy. Doors: SlidingDoorNarrow250's opening time and doorway box, a DoorClientDefinition copied from its base and tied to the block's DoorServerDefinition, and a parts provider on the block's ConditionalSubPartsDefinition, set on the door collider with the block's model definition.
  - Vanilla items are named by Guid; a step whose item is missing is skipped.
  - A defect found later follows the same recipe: the general rule when one fits the whole kind, otherwise an entry in the list; the skill and wiki lines that worked around it go.
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

## create_asset

- The Editor's Create menu entries for assets made from input files, through the Editor's own generators as `create_partial` does, so the Editor's rules come back as its errors. `kind` names the menu entry; the kinds offered are a short list that grows as each is tested: `skybox`.
- **skybox:** a cubemap of Category Skybox. `inputs` are six images whose name endings `_px`, `_nx`, `_py`, `_ny`, `_pz`, `_nz` pick their faces, or none for an empty skybox whose faces are then written. The Editor refuses a wrong count, a missing or repeated ending, or an image type it does not take.
- `name` defaults to the inputs' shared name before the endings.
- A name already used in the folder is an error, as in `create_partial`.

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

## present_graph

Explains how content items relate by drawing them, in the user's own visual language: each node is the item's Content Browser tile, so the user recognises it there afterwards. The Board that shows it is in `EditorUi.md`.

- **Arguments:**
  - `nodes`: `[{id, item, note?}]`. `id` is a short moniker the model picks (`prefab`, `flame_emitter`), so it never numbers nodes. Ids match ignoring case. `item` is a content item resolved like a target. `note` is optional text shown under the tile, at most ~300 characters.
  - `edges`: `[{from, to, label?}]`. `from` and `to` are node ids. `label` is optional, at most ~30 characters, typically the field that holds the reference (`Effect`, `Emitters[0]`).
- **Direction:** an edge always points from the item that references to the item referenced. The description says so: one rule a small model keeps.
- **Nodes are content items only**, with the target rules: no source files, folders or free concepts. The model has no tool that reads source files, so links to them would be guesses. Field-level nodes and concept nodes are possible extensions.
- **Validation:** the whole call is refused, and nothing reaches the user, when:
  - an item does not resolve (the usual not-found and ambiguity errors, with nearest names);
  - an edge names an unknown id (the error lists the ids);
  - two nodes share an id or an item (the error names the id that holds it); two tiles of one file would read as two files;
  - an edge is repeated or goes from a node to itself;
  - a note or label is over its length, or there are more than 25 nodes or 30 edges. The limits keep a model from dumping a whole dependency tree. The node refusal says how many to drop: a model told only the count and the limit planned a removal and sent the same nodes again.
  A wrong item on the board would mislead the user; a refusal costs the model one round.
- Nodes without edges are allowed.
- **Reply:** `Shown to the user.` The chat shows the call as any tool call.
- The model explains in text as well; the graph supports the answer, it does not replace it.

## Assemblies

| assembly | holds | tested by |
|---|---|---|
| `Veronica.Editing.dll` | path parsing, field walking, visibility, dump, value search, value coercion, write, list-add, template tier matching, content name matching, definition type matching, component naming, graph argument validation and layout, describe's type walk, listings and doc text | `Veronica.Tests`, on engine types such as `ModelDummy` and on test types |
| `Veronica.dll` | the project layer (target and reference resolution over the project, read-only checks, saving, folder and prefab creation, blocks through the Editor's prefab layer), the type catalog behind describe (type names, component relations), the `ITool` wrappers and the Board | by hand in the Editor: a full thruster build on the dev mod, adding, editing and removing a component on its block, reverted afterwards, and a graph on the Board |

- `Veronica.Editing.dll` references only `VRage.Library` and `VRage.UI.Shared` from the Editor folder, and runs without the Editor. The prefab visibility rule therefore recognises the prefab root by its type name. The project side of references (names to items, definition placeholders) comes in through an interface that `Veronica.dll` implements.
- `Veronica.Agent.dll` stays free of SE2 references.
- `Veronica.Tests` needs the Editor folder to build:
  - It copies the engine assemblies it references and the native `Optick.dll` the engine's type indexers start.
  - It sets `Keen.VRage.Library.DefaultMetadataContextAssemblies` to itself, because the engine indexes types from the entry assembly, which is the test host.
  - Test types are `[Serialize]` classes compiled by the serialization generator the Editor ships (`VRage.Library.Generator.dll`).
