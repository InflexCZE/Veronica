# Editor tools: overview

The contract every Editor tool shares with the model, and the layer the tools edit through. The tools themselves are in `Tools_Readers.md`, `Tools_Writers.md`, `Tools_Creators.md` and `Board.md`.

## Through the Editor's own layers

- Definitions are read and edited through the Editor's definition data layer (`ObjectManipulator` / `MemberReference`), the layer behind the Property Inspector. Blocks go through the Editor's prefab layer behind the inspector's prefab view, and creation through the Editor's own Create menu generators. No UI is automated.
- Why: the Editor's rules then apply as they do for a modder (partial overrides recorded automatically, client/server pairing, dependency filling, name checks, saving) and come back as the Editor's own errors, instead of being reimplemented and drifting.
- **Saving:** every successful change ends where an inspector edit ends: marked modified, auto-saved, an open inspector refreshed. There is no save tool. A failed disk write reaches only the Editor log, as for inspector edits.
- **Read-only:** the Editor's own flag decides, on the target file for edits and on the target folder for creation. The error quotes the Editor's reason.
- **Busy files:** the Editor locks a file of the mod briefly while it works on it, for example right after a prefab is generated. A change that meets the lock waits and retries off the UI thread with a bounded wait instead of failing; listings never show the passing lock.
- **No undo:** edits are not Editor undo steps (`Docs/TODOs/EditorUndo.md`).
- **Tool lines:** each tool returns a short line for the user beside the output the model reads, so the chat stays readable without showing raw tool output.
- **Threading:** tools run one at a time on a worker thread (`Agent.md`) and do their project work on the UI thread. A call stopped before it reached the UI thread changes nothing.
- Every call works on the currently loaded project.

## Naming content

- The model names content items by project-relative path or by file name, extension optional. It never sees GUIDs.
- Names resolve over the mod and its dependencies. Replies print the full path, and an item outside a listing names its project when it is a read-only dependency.
- **Tolerant names:** a shortened path (a dropped `Assets` prefix, a project name in front) resolves when it is unambiguous, and an exact path always wins. Small models shorten the paths listings print, and repeated not-found errors sent them off building copies instead of reading the original.
- **Exact folders to create:** a folder that a tool creates is taken exactly as given, since a shortened path would put new files into some other folder.
- **Near names:** a not-found error suggests items spelled almost the same. Small models rebuild a name from memory and keep retrying it after a listing printed the right one.
- **Tolerant targets:** where a target the model gives can only mean one thing, the tool takes that thing instead of refusing: any of a block's files names the block, and a reference given as an override is written as its original. A refusal there costs rounds and teaches the model nothing.

## Errors

- Every failure is a tool result the model can act on: short, naming the bad part, listing the valid alternatives where there are any (field names, candidate paths, enum values, templates, concrete types).
- Nothing is guessed where it creates or points: an unknown type, template or reference lists candidates instead of picking one.

## Fields

- **Visibility** is the Property Inspector's: its own visibility strategy decides, and inside a component's object builder the inspector's stricter prefab rule applies on top. Read and write reach visible fields only, and a field is writable exactly when the inspector lets the user edit it.
  - `[SelectedItemOf]` fields are always hidden: they are UI selection mirrors and read as empty outside the live inspector.
  - A short hand-kept list makes visible the data the Editor edits in dedicated windows (a particle effect's emitters and lights), which the model could not reach otherwise.
- **One path syntax** for every tool that takes a field: what any tool prints is a path every other tool takes. The Editor's own member paths (`$Root.`...) are accepted as they are.
- **Values in the inspector's form:**
  - A transform is a position and Euler angles in degrees; the quaternion is never addressable. A quaternion written as if it held angles is unnormalized and turns the rotation into something else.
  - A colour is its channels; its packed value is hidden, since a model shown both wrote both and got the packing wrong.
  - A struct that stands for one plain value (`FixedPoint`) is that value, never its storage: a model that saw the raw field wrote values a million times too large.
- A path never walks through a reference into the referenced definition: behind it sits the engine's runtime object, not the file. The model reads the referenced item instead.
- **Assets with fields:** besides definitions, a short list of asset kinds whose data is plain serialized members (cubemaps so far). Kinds join as each is tested in the Editor.

## Blocks

- A block is a prefab binding that pairs a client prefab with a server composition. Each side has a composition (the component list) and a prefab (each component's object builder); one component on both sides shares its Guid.
- **The block is the unit:** the tools act on the whole block through the Editor's prefab layer, so pairing and sides follow the inspector's rules. Any of its files names it, and a prefab without a binding is a one-sided block.
- A block reads as its component list. Components are named by type, by tag when a type repeats, and by a Guid prefix as the last resort; read prints the name every tool takes.
- Only the root entity is reached (`Docs/TODOs/PrefabChildEntities.md`). Side toggles and overrides of a dependency's prefab are not tool operations.

## Hand-kept corrections

Where the Editor or the game is wrong about something the tools rely on (a component's side, a member's unit or doc, a vanilla template that fails validation), Veronica keeps a short hand-kept correction in code. Vanilla content is never changed. Entries join when a defect is confirmed in the game code and leave when the game is fixed; the skill and wiki lines that worked around the defect go with it.

## Assemblies

- `Veronica.Editing.dll` holds the rules that need no running Editor: paths, field walking, visibility, value coercion, reads, writes, searches, name and template matching, component naming, graph validation and layout. It references only the engine's library assemblies, so it runs and is tested without the Editor; the project side (names to items, definition placeholders) comes in through an interface. The prefab visibility rule therefore recognises the prefab root by its type name.
- `Veronica.dll` holds what needs the loaded project: resolution over the project, read-only checks, saving, creation, blocks through the prefab layer, the type catalog, the tool wrappers and the Board.
