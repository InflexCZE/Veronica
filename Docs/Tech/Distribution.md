# Distribution

How Veronica reaches a modder's machine: the release, `Veronica.exe`, the install folder, settings, providers and models, hooking Veronica into a mod, and updates.

## Release

- One GitHub release of this repo per version. `Veronica.exe` takes `/releases/latest` only; pre-releases are ignored.
- Versions are SemVer. The release tag is `vX.Y.Z`, and the build stamps `X.Y.Z` as the assembly version of `Veronica.dll` and `Veronica.exe`. Every version comparison uses these.
- Assets: `Veronica.exe` and `Veronica.zip`.
- `Veronica.zip` holds the plugin, WebQwen, the skills and the wiki. They version as one unit, so the protocol between `Veronica.dll` and `WebQwen.exe` needs no version check.
- `WebQwen.exe` is a framework-dependent single-file publish: its managed code is folded into the exe. `wq.dll` and `webgpu_dawn.dll` stay beside it. Packed into the exe, .NET would extract them to `%TEMP%` on the first start of every version.
- `dxcompiler.dll` and `dxil.dll` are not shipped. `Veronica.dll` copies them from the Editor's folder next to `WebQwen.exe` before it starts it, when they are missing or older.
- Model files are not in the release. They come from Hugging Face.
- Nothing is code-signed yet; Windows SmartScreen warns on the first start of a downloaded `Veronica.exe`.

## Veronica.exe

- One self-contained .NET exe (the runtime is bundled) with an Avalonia window. It downloads `Veronica.zip` at install and update time.
- The user puts it in any folder. That folder is the install root. When the folder holds files that are not Veronica's, it warns and lets the user go on.
- First run: Welcome, provider selection, the chosen provider's setup, then a wait until the provider's check reports all good.
- Afterwards it opens in app mode: the provider, the mods Veronica is hooked into, and versions (updates and their conflicts).

## Install folder

| entry | holds | on update |
|---|---|---|
| `Runtime` | `Veronica.dll` and its private DLLs, `WebQwen.exe` with its native DLLs and its `manifest.json` | replaced |
| `Skills`, `Wiki` | the shipped Markdown, plus anything the user adds | merged, see Updates |
| `Models` | one `<model id>` folder per installed model | per the manifest |
| `Downloads` | partial downloads | scratch |
| `Veronica.settings.json` | the settings, see Settings | kept |
| `Veronica.installed.json` | the installed release version and the hash of every skill and wiki file it installed; read by `Veronica.exe` only | rewritten |
| `Veronica.log` | `Veronica.exe`'s log | appended |

- An installed `Veronica.dll` finds everything relative to itself: `..\Skills`, `..\Wiki`, `..\Models`, `..\Veronica.settings.json`, `WebQwen.exe` beside it. A development build uses the paths written into it at build time when no install layout surrounds it.

## Settings

- Settings are flat string key/value pairs. Each key is a `const string` on one static `Settings` class whose value is its own name (`VERONICA_PROVIDER = nameof(VERONICA_PROVIDER)`), documented with what it is for and its values.
- The store is filled from `Veronica.settings.json`, then from environment variables of the same names, which win. The plugin reads settings only from the store, once when the panel is created; a change takes effect at the next Editor start.
- `Veronica.exe` writes the file: the provider, its settings and the active model.
- An API key is stored encrypted with Windows DPAPI for the current user, so a copied install folder does not carry it in the clear.

## Providers

- Provider selection offers WebQwen (local), Claude (`claude -p`) and an OpenAI-compatible server.
- Each provider's setup ends with a cheap check, no prompt and no model load:
  - WebQwen: the chosen model's files are present and verified;
  - Claude: `claude` is found and signed in;
  - OpenAI-compatible: the server at the URL lists the chosen model.

## Models

- The WebQwen manifest lists the models and pins each one's weights (a Hugging Face commit) and library (sha256). `Veronica.exe` downloads exactly those revisions.
- It measures the GPU: `WebQwen.exe`'s requirements check (features, limits, VRAM) runs against every model in the manifest. All models are shown with their size and result, and one that fits is recommended. A model fits by total VRAM, since the Editor's VRAM is evicted during turns; low free VRAM is shown as a warning that other applications will slow it. A failed requirement is shown with its reason and can be overridden.
- Several models may be installed, each with a Delete button. The settings name the active one; the plugin runs it and offers no choice.
- A download lands in `Downloads`, resumes after an interruption, is verified against the Hugging Face hashes and the manifest's library sha256, and only then moves into `Models`. Everything is on disk before WebQwen loads a model, so the engine stays offline.

## Hooking into a mod

- `Veronica.exe` lists the local mods from the HUB's mod index (`%APPDATA%\SpaceEngineers2\Temp\LocalMods\ModIndex`, project GUID to folder), each with a checkbox. Which mods are hooked is read from their `.vrgproj` files, not recorded.
- Checking a mod adds `Veronica.dll` to its `.vrgproj`'s `EditorExtensionAssemblies` and the absolute `Runtime` path to `EditorExtensionAssembliesSearchPaths`. Unchecking removes both.
- A `.vrgproj` is edited only while the Editor is closed, since an open Editor may write the project back over the edit.
- A mod whose Veronica entry points at another `Runtime` (the install folder was moved) is found on start and offered a repoint to this one.
- The Editor refuses to load a project whose extension assembly it cannot find. A collaborator without Veronica at the same path therefore cannot open the mod. This cost is accepted.

## Updates

- Veronica checks the latest release once per Editor start. The panel shows a newer version with a button that starts `Veronica.exe`, which waits for the Editor to exit, since the Editor keeps `Runtime`'s DLLs locked.
- `Veronica.exe` updates itself first. When the release carries a newer one, it renames its running exe to `.old` (Windows allows renaming a running exe, not overwriting it), moves the new one in and restarts. The new instance deletes the `.old` file.
- `Runtime` is replaced.
- An installed model whose pinned revision changed is replaced without asking. The old revision is deleted once the new one is verified.
- `Skills` and `Wiki` are merged against `Veronica.installed.json`:
  - a file the user did not change takes the new version, or is deleted when the release drops it;
  - a file the user changed stays, and the new version is written beside it as `name.new.md` (empty when the release drops the file). These conflicts are listed with [Keep mine], [Use new version] and [Open], which opens both files in Notepad. Unresolved conflicts are listed again on the next run;
  - a file the user added is left alone.
- The skill and wiki loaders skip `*.new.md`.

## Testing

- Unit tests touch no web and no disk. The decisions are pure: the skill and wiki merge plan (installed record, disk snapshot and new release in, actions out), `.vrgproj` add, remove and repoint as text, the `ModIndex` parse, version comparison, the settings layering over a given environment, model fit and recommendation.
- Effects sit behind seams with in-memory test versions: a file interface `Veronica.exe` owns, `HttpClient` over a test `HttpMessageHandler` (serving release and model bytes, honouring `Range`, cutting a stream short for resume and verification), the Editor-running check. Zips are read from memory.
- Recorded GitHub and Hugging Face responses and sample `.vrgproj` and `ModIndex` files are inline strings in the tests.
- The UI is not tested.
- Before each release, by hand on a real machine:
  - a fresh install from the GitHub release: provider setup, a model download with an interruption, the provider check;
  - hooking and unhooking a mod, and repointing it after moving the install folder;
  - an update with a changed skill: the conflict list and each of its buttons;
  - a self-update of `Veronica.exe`;
  - the panel's update notice and its button;
  - the API key surviving a restart, and unreadable in the settings file.
