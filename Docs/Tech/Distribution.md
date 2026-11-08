# Distribution

How Veronica reaches a modder's machine: the release, `Veronica.exe`, the install folder, settings, providers and models, hooking Veronica into a mod, and updates.

## Release

- One GitHub release of this repo per version. `Veronica.exe` takes `/releases/latest` only; pre-releases are ignored.
- The releases/latest API URL is the setting `VERONICA_RELEASES_URL`, by default this repo's GitHub releases. Gitea's `/api/v1/repos/<owner>/<repo>/releases/latest` answers in the same shape, so a test release can come from a local Gitea.
- Versions are SemVer. The release tag is `vX.Y.Z`, and the build stamps `X.Y.Z` as the assembly version of `Veronica.dll` and `Veronica.exe`. Every version comparison uses these.
- Assets: `Veronica.exe` and `Veronica.zip`.
- `Veronica.zip` holds the plugin, WebQwen, the skills and the wiki. They version as one unit, so the protocol between `Veronica.dll` and `WebQwen.exe` needs no version check.
- `WebQwen.exe` is a framework-dependent single-file publish: its managed code is folded into the exe. `wq.dll` and `webgpu_dawn.dll` stay beside it. Packed into the exe, .NET would extract them to `%TEMP%` on the first start of every version.
- `dxcompiler.dll` and `dxil.dll` are not shipped. `Veronica.dll` copies them from the Editor's folder next to `WebQwen.exe` before it starts it, when they are missing or older.
- Model files are not in the release. They come from Hugging Face.
- `Build\Publish.ps1 -Version X.Y.Z` builds both assets into `bin\dist\<version>`; it creates no release.
- Nothing is code-signed yet; Windows SmartScreen warns on the first start of a downloaded `Veronica.exe`.

## Veronica.exe

- One self-contained .NET exe (the runtime is bundled) with an Avalonia window. It downloads `Veronica.zip` at install and update time.
- Its project is `Source/Veronica.Setup`, assembly name `Veronica`. It shares the settings and release code with the plugin through `Veronica.Agent`.
- The install root is the folder `Veronica.exe` runs from. Welcome picks it: `%APPDATA%\Veronica` by default, changed with a folder dialog. When the chosen folder holds files that are not Veronica's, one line says Veronica's folders go beside them, and the user may go on.
- Unless it already is the chosen folder's `Veronica.exe`, it copies itself there as `Veronica.exe` and restarts from the copy, which starts the install and opens on the provider choice. The original exe stays where it was; started again, it shows a fresh first run. The panel's button and self-update look for `Veronica.exe`. A running single-file exe loads its assemblies from its own file, so it is never renamed while it still works; self-update renames it only right before it exits.
- Welcome offers a Start menu and a Desktop shortcut to the installed `Veronica.exe`, both checked. A shortcut that fails does not stop the install.
- One instance runs per install folder; a second start brings the first to the front, and passes on the panel's update request.
- Downloads (the release, a model) can be cancelled; a partial model stays in `Downloads` and resumes next time.
- Names that come from a download (zip entries, Hugging Face file names, the manifest's model ids) must stay inside their folder; anything else fails the install or download.
- Headlines and greetings speak as Veronica, in the first person. Information, status and technical lines are plain UI text: no "I" or "my", and the local model is "the local model", never Veronica herself.
- First run is three pages under a fixed header: Welcome, the provider choice, and the chosen provider's setup. The title bar shows a single Initial Setup tab in place of the app's tabs. Going back is always free; going forward only past the pages that are done.
- Welcome's start button begins the install in the background; nothing touches the folder before it. Once the install belongs to a folder, Welcome shows it without the dialog or the shortcuts. A strip at the bottom shows its progress or its error with a retry. First run installs the latest release with the running exe; the exe updates itself at the next update.
- A provider's setup page has one button, which needs the finished install: it runs the provider's check, shows a pass for a moment, then saves and moves on. A failed check stays on the page with its reason.
- Afterwards it opens in app mode, with tabs in the title bar: Mods first, then Brain (the provider choice and its setup, opened on the current provider's setup), then Updates (marked when an update or a conflict waits).
- An operation that needs the Editor closed shows the wait where it was started, with Cancel.

## Install folder

| entry | holds | on update |
|---|---|---|
| `Runtime` | `Veronica.dll` and its private DLLs, `WebQwen.exe` with its native DLLs and its `manifest.json` | replaced |
| `Skills`, `Wiki` | the shipped Markdown, plus anything the user adds | merged, see Updates |
| `Models` | one `<model id>` folder per installed model | per the manifest |
| `Downloads` | partial downloads | scratch |
| `Veronica.settings.json` | the settings, see Settings | kept |
| `Veronica.installed.json` | the installed release version, the hash of every skill and wiki file it installed, the pin each installed model was downloaded at, and which waiting conflicts are drops; read by `Veronica.exe` only | rewritten |
| `Runtime.new`, `Runtime.old` | the next and the previous `Runtime` during an update's swap | transient |
| `Veronica.log` | `Veronica.exe`'s log | appended |

- An installed `Veronica.dll` finds everything relative to itself: `..\Skills`, `..\Wiki`, `..\Models`, `..\Veronica.settings.json`, `WebQwen.exe` beside it. A development build uses the paths written into it at build time when no install layout surrounds it.

## Settings

- Settings are flat string key/value pairs. Each key is a `const string` on one static `Settings` class whose value is its own name (`VERONICA_PROVIDER = nameof(VERONICA_PROVIDER)`), documented with what it is for and its values.
- The store is filled from `Veronica.settings.json`, then from environment variables of the same names, which win. A settings file that cannot be read is logged and skipped, so environment variables alone apply. The plugin reads settings only from the store, once when the panel is created; a change takes effect at the next Editor start.
- `Veronica.exe` writes the file: the provider, its settings and the active model (`VERONICA_MODEL`).
- An API key is stored encrypted with Windows DPAPI for the current user, so a copied install folder does not carry it in the clear.

## Providers

- Provider selection offers WebQwen (local), Claude (`claude -p`) and an OpenAI-compatible server.
- Each provider's setup ends with a cheap check, no prompt and no model load:
  - WebQwen: the chosen model's files are present and verified;
  - Claude: `claude` is found and signed in;
  - OpenAI-compatible: the server at the URL lists the chosen model.
- The OpenAI-compatible address is worked out, not required in full. Without a scheme it tries https, then http; without a port, the scheme's default, then 8080, 1234, 11434 and 8000; without a path, none, then `/v1` and `/api/v1`. An empty address means this computer, on those ports. All tries run at once with a short timeout and without the API key; the first server in that order wins, and the full URL is written back into the field and saved. Only a server that answers 401 or 403 is asked again, with the key. Like the provider, the check accepts self-signed certificates.

## Models

- The WebQwen manifest lists the models and pins each one's weights (a Hugging Face commit) and library (sha256). `Veronica.exe` downloads exactly those revisions.
- It measures the GPU: `WebQwen.exe`'s requirements check (features, limits, VRAM) runs against every model in the manifest. All models are shown with their size and result, and one that fits is recommended. A model fits by total VRAM, since the Editor's VRAM is evicted during turns; low free VRAM is shown as a warning that other applications will slow it. A failed requirement is shown with its reason and can be overridden.
- Until that check exists, the list shows every model with its download size and the manifest's VRAM need, with no measurement, recommendation or override (`Docs/TODOs/RequirementsCheck.md`).
- The local model's setup shows its limits first (small model, heavy on the graphics card), and the model list stays disabled until the modder confirms they understand; that confirmation counts as given once any model is installed. Picking a model downloads it if needed.
- Several models may be installed, each with a Delete button, which waits for the Editor to close. The settings name the active one; the plugin runs it and offers no choice. An installed plugin with no active model says so in the chat and points at `Veronica.exe`.
- A download lands in `Downloads`, resumes after an interruption, is verified against the Hugging Face hashes and the manifest's library sha256, and only then moves into `Models`. Everything is on disk before WebQwen loads a model, so the engine stays offline.

## Hooking into a mod

- `Veronica.exe` lists the local mods from the HUB's mod index (`%APPDATA%\SpaceEngineers2\Temp\LocalMods\ModIndex`, project GUID to folder), each with Start Editor and Install or Uninstall. Which mods are hooked is read from their `.vrgproj` files, not recorded.
- Install adds `Veronica.dll` to the `.vrgproj`'s `EditorExtensionAssemblies` and this install's absolute `Runtime` path to `EditorExtensionAssembliesSearchPaths`. Uninstall removes `Veronica.dll`, this install's path and any search path whose folder holds a `Veronica.dll`; the mod's other paths stay.
- A mod that loads `Veronica.dll` with any other search path (a development build, another install, an install that was moved) is another build: it says so and offers Uninstall, never Install over it.
- Start Editor opens the mod in the Editor, installing Veronica first when the mod has none. The first use asks for `VRage.Editor.exe` and saves it (`VERONICA_EDITOR_EXE`); the Mods page shows it with Change, and a saved exe that is gone asks again.
- The Editor gets only the arguments it needs to start: `--project` and `--globalProjectDirs` with the game's and the engine's `GameData`. These are found above the exe the way a development checkout lays them out (`Game2\GameData`, `VRage\GameData`); a retail layout is open (`Docs/TODOs/RetailEditorLaunch.md`).
- A `.vrgproj` is edited only while the Editor is closed, since an open Editor may write the project back over the edit.
- The Editor refuses to load a project whose extension assembly it cannot find. A collaborator without Veronica at the same path therefore cannot open the mod. This cost is accepted.

## Updates

- Veronica checks the latest release once per Editor start. The panel shows a newer version with a button that starts `Veronica.exe` with `--update`: it starts the update at once and waits for the Editor to exit, since the Editor keeps `Runtime`'s DLLs locked.
- An update (not the first install) updates `Veronica.exe` itself first. When the release carries a newer one, it renames its running exe to `.old` (Windows allows renaming a running exe, not overwriting it), moves the new one in and restarts; if the move fails, the `.old` exe is renamed back. The new instance deletes the `.old` file and does not update itself again in that run, so an exe stamped lower than its tag cannot loop.
- `Runtime` is replaced without a window where none works: the zip is downloaded and unpacked into `Runtime.new`, the Editor is checked closed right before the swap, then `Runtime` becomes `Runtime.old` and `Runtime.new` becomes `Runtime`. A failed swap puts the old `Runtime` back.
- An installed model whose pinned revision changed is replaced without asking. The old revision is deleted once the new one is verified.
- `Skills` and `Wiki` are merged against `Veronica.installed.json`:
  - a file the user did not change takes the new version, or is deleted when the release drops it;
  - a file the user changed stays, and the new version is written beside it as `name.new.md` (empty when the release drops the file; `Veronica.installed.json` records which are drops). A `name.new.md` that the disk has since caught up with is removed. These conflicts are listed with [Keep mine], [Use new version] and [Open], which opens both files in Notepad. Unresolved conflicts are listed again on the next run;
  - a file the user added is left alone.
- The skill and wiki loaders skip `*.new.md`.

## Testing

- Unit tests touch no web and no disk. The decisions are pure: the skill and wiki merge plan (installed record, disk snapshot and new release in, actions out), `.vrgproj` add and remove as text, the `ModIndex` parse, version comparison, the settings layering over a given environment, model fit and recommendation.
- Effects sit behind seams with in-memory test versions: a file interface `Veronica.exe` owns, `HttpClient` over a test `HttpMessageHandler` (serving release and model bytes, honouring `Range`, cutting a stream short for resume and verification), the Editor-running check. Zips are read from memory.
- Recorded GitHub and Hugging Face responses and sample `.vrgproj` and `ModIndex` files are inline strings in the tests.
- The tests are `Veronica.Setup.Tests`, apart from `Veronica.Tests`, whose output carries the Editor's Avalonia build, which clashes with the public one `Veronica.exe` ships.
- The UI is not tested.
- Before each release, by hand on a real machine:
  - a fresh install from the GitHub release: provider setup, a model download with an interruption, the provider check;
  - first run from Downloads: the default and a changed install folder, the busy-folder line, the copy and restart landing on the provider choice with the install running, both shortcuts, the original exe left in place;
  - installing and uninstalling Veronica in a mod, also one hooked by a development build; Start Editor on a mod without Veronica, with the exe dialog on first use;
  - an update with a changed skill: the conflict list and each of its buttons;
  - a self-update of `Veronica.exe`, and a second start while one runs;
  - the panel's update notice and its button;
  - the API key surviving a restart, and unreadable in the settings file.
