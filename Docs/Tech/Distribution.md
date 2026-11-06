# Distribution

How Veronica reaches a modder's machine: the release, the setup tool, the install folder, hooking Veronica into a mod, the model download and updates.

## Release

- One GitHub release of this repo per version. Setup takes `/releases/latest` only; pre-releases are ignored.
- Assets: `Veronica.exe` and `Veronica.zip`.
- `Veronica.zip` holds the plugin, WebQwen, the skills and the wiki. They version as one unit, so the protocol between `Veronica.dll` and `WebQwen.exe` needs no version check.
- `WebQwen.exe` is a framework-dependent single-file publish: its managed code is folded into the exe. `wq.dll` and `webgpu_dawn.dll` stay beside it. Packed into the exe, .NET would extract them to `%TEMP%` on the first start of every version.
- `dxcompiler.dll` and `dxil.dll` are not shipped. `Veronica.dll` copies them from the Editor's folder next to `WebQwen.exe` before it starts it, when they are missing or older.
- Model files are not in the release. They come from Hugging Face.

## Setup tool

- `Veronica.exe` is one self-contained .NET exe (the runtime is bundled). It downloads `Veronica.zip` at install and update time.
- The user puts it in any folder. That folder is the install root.
- It installs, updates, hooks Veronica into mods, downloads models and updates itself.

## Install folder

| folder | holds | on update |
|---|---|---|
| `Runtime` | `Veronica.dll` and its private DLLs, `WebQwen.exe` with its native DLLs and its `manifest.json` | replaced |
| `Skills`, `Wiki` | the shipped Markdown, plus anything the user adds | merged, see Updates |
| `Models` | `<model id>` folders and a config naming the active model | per the manifest |
| `Downloads` | partial downloads | scratch |

- An installed `Veronica.dll` finds everything relative to itself: `..\Skills`, `..\Wiki`, `..\Models`, `WebQwen.exe` beside it. A development build uses the paths written into it at build time when no install layout surrounds it.

## Hooking into a mod

- Setup lists the local mods from the HUB's mod index (`%APPDATA%\SpaceEngineers2\Temp\LocalMods\ModIndex`, project GUID to folder), each with a checkbox.
- Checking a mod adds `Veronica.dll` to its `.vrgproj`'s `EditorExtensionAssemblies` and the absolute `Runtime` path to `EditorExtensionAssembliesSearchPaths`. Unchecking removes both.
- The Editor refuses to load a project whose extension assembly it cannot find. A collaborator without Veronica at the same path therefore cannot open the mod. This cost is accepted.

## Model

- The WebQwen manifest lists the models and pins each one's weights (a Hugging Face commit) and library (sha256). Setup downloads exactly those revisions.
- Setup measures the GPU: `WebQwen.exe`'s requirements check (features, limits, total and free VRAM) runs against every model in the manifest. Setup shows them all with their size and result, and recommends one that fits. A failed requirement is shown with its reason and can be overridden.
- The user may install several models. A config in `Models` names the active one. Setup sets it; the plugin runs it and offers no choice.
- A download lands in `Downloads`, resumes after an interruption, is verified against the Hugging Face hashes and the manifest's library sha256, and only then moves into `Models`. Everything is on disk before WebQwen loads a model, so the engine stays offline.

## Updates

- Veronica checks the latest release once per Editor start. The panel shows a newer version with a button that starts setup. Setup waits for the Editor to exit, since the Editor keeps `Runtime`'s DLLs locked.
- Setup updates itself first. When the release carries a newer setup, it renames its running exe to `.old` (Windows allows renaming a running exe, not overwriting it), moves the new one in and restarts. The new instance deletes the `.old` file.
- `Runtime` is replaced.
- An installed model whose pinned revision changed is replaced without asking. The old revision is deleted once the new one is verified.
- `Skills` and `Wiki` are merged. Setup records the version and hash of every file it installed and compares them with the disk:
  - a file the user did not change takes the new version, or is deleted when the release drops it;
  - a file the user changed stays, and the new version is written beside it as `name.new.md` (empty when the release drops the file). Setup lists these conflicts with [Keep mine], [Use new version] and [Open], which opens both files in Notepad. Unresolved conflicts are listed again on the next run;
  - a file the user added is left alone.
- The skill and wiki loaders skip `*.new.md`.
