# Editor tools: readers

Tools that find and read content without changing it: `find_content`, `list_folder`, `find_references`, `read`, `contains`, `describe`, plus `find_template` (`Tools_Creators.md`). They are also the researcher's project tools (`Wiki.md`). The shared contract is in `Tools_Overview.md`.

## find_content and list_folder

- `find_content` matches words against file names and definition type names, base types included, so a base type name finds a block's definition whatever its derived type. A type named loosely matched several of a block's definitions and sent a model searching for many rounds.
- Results are grouped by project, the mod first, then dependencies in load order, each group saying whether it is read-only.
- `list_folder` lists direct children only; a folder present in several projects gets one section per project.

## read

- One line per field, printed as the path the writing tools take; references print the target's path.
- Dumps leave out read-only fields as computed noise; reading a read-only field's exact path still shows it.
- Long output is cut with a hint that says how to read on. Every cut says so: a silent cut reads to the model as the whole.
- A block reads as its component list (`Tools_Overview.md`, Blocks).

## find_references

- Lists what points at one item, one step. A model answering "what else uses this" otherwise swept folder after folder at great cost, and still claimed nothing else used the item after covering only part of the project.
- The referrers come from the Editor's own reference index, the one behind the Content Browser's referencing search. It covers definitions, assets, partials, the mod and its dependencies, so the list is complete and the description says a short list means nothing else uses the item. An empty or disabled index is an error, never an empty list.
- The index names files; each referrer is then walked to the fields that hold the reference, printed as paths. A referrer whose field the walk does not find still gets its row, so the count stays true. Partial copies and overrides of the target are added from the Editor's list of partials, since the index does not always link a partial to its base.
- A block's files make one row for the block. An override as the target is searched as its original, since every reference to the original gets the override's values.
- One step only: a chain in one call would dump whole dependency trees. The model follows a chain by calling it again on a referrer.

## contains

- Whether one definition holds a value, without read's cuts: a large set (the survival unlock configuration's blocks) is far past what read shows, and a model reading it to look for one entry reads too much or stops early.
- Each match prints as a path that write and element_remove take. A value that names content items matches every reference to them; a block matches through all its files, and the skill that uses it says which file counts.
- With no whole match, values that contain the text are listed, so a half-remembered name is found in one call instead of a not-found.
- An empty answer says what was searched, so it reads as a search that ran. A long result is cut with a count and a request to narrow; a vague value that would match many items is matched as text only, and the reply says so.

## describe

- What a type or a field means, from the game's own documentation through the two sources the Property Inspector uses: the doc summaries the serialization generator bakes into serializable types, and the XML doc files beside the game's assemblies (the only source for a component's own class, which is not serializable).
- **Subject:** a type the inspector can show or a component type, by short name, or a content item, which describes its type; a block lists its components with the definition types they need. The header names the resolved type, so the model learns the name for later calls.
- **Member:** a field path in read's syntax, so the model never meets a second syntax. On an item it walks the values that are there; on a type name it walks declared types.
- A listing gives member names only; a member query gives its summary and the listing of its type, one level per call, so a listing never floods the context.
- **Relations:** base types, concrete derived types, and for components and definitions the definition type a component takes and the components that take a definition type. Further relations wait for eval evidence.
- **Interfaces:** an interface is a subject that lists the components implementing it, and a component lists the interfaces it implements: some families share only an interface (animation controllers, render parents), and the interface is the one name that lists them all. Engine plumbing that nearly every component implements and no component needs from another is left out by a hand-kept list, since it would fill every component's description with names a modder never uses.
- **Leftovers:** types the game keeps only so old files still load (internal and hidden from the Editor's creation menu) are never offered, here or by the tools that take a type name: they share names with the real types and made them ambiguous. Hidden alone is not enough, since the game hides real base types too.
- **Units** are the unit a field declares, as a label: nothing converts values, so the stored value is in that unit.
- Wrong or missing units and docs are corrected by a hand-kept list (`Tools_Overview.md`, Hand-kept corrections).
- An abstract type lists its concrete types to describe next. Links to web documents are dropped: the model cannot open them.
