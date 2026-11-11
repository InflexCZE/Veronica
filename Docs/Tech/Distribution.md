# Distribution

How Veronica reaches a modder's machine and stays current: the release, the Launcher, the install folder, settings, and updates.

## Release

- One GitHub release of this repo per version; only `/releases/latest` counts, pre-releases are ignored. The releases URL is a setting, and Gitea answers in the same shape, so a test release can come from a local Gitea.
- Versions are SemVer. The tag `vX.Y.Z` and the assembly version of the plugin and the Launcher carry the same number, and every version comparison uses them.
- Three assets: the Launcher exe, a zip with the plugin, the runtime, the skills and the wiki, and the third-party notices. The zip versions as one unit, so the plugin-runtime protocol needs no version check.
- The notices cover what the exe and the zip ship, so they stay standalone beside them on the release page and the Launcher does not download them. `Build/THIRD-PARTY-NOTICES.txt` changes with every added or bumped dependency.
- `WebQwen.exe` is a framework-dependent single-file publish with `wq.dll` and `webgpu_dawn.dll` beside it: packed inside, .NET would extract them to `%TEMP%` on the first start of every version.
- `dxcompiler.dll` and `dxil.dll` are not shipped. The plugin copies them from the Editor's folder beside `WebQwen.exe` before starting it, when missing or older.
- Model files are not in the release; they come from Hugging Face.
- `Build/Publish.ps1` builds the assets; it creates no release. Releases are unsigned (`Docs/TODOs/CodeSigning.md`).

## Public wiki

- The GitHub wiki is generated, never edited: on a push to GitHub that touches its sources, an Action rebuilds it from `/Wiki` (hand-written pages, `Home.md` the front page), the shipped skills and wiki, and the eval tasks, and replaces the whole wiki repo. Editing on GitHub is restricted to collaborators, since a web edit is lost at the next publish.
- Visitors read skills, game knowledge and eval tasks without digging through the code repo; the repo stays the only source, reviewed with the code.
- Wiki URLs are flat whatever the folder, so generated pages carry a section prefix (`Skill-`, `Knowledge-`, `Eval-`) and the build fails on any clash.
- Frontmatter becomes the page's description; a wiki page's `source` is dropped, as provenance for maintainers. Eval asset folders are not published.
- The sidebar is generated; `.github/scripts/publish_wiki.py <dir>` previews the pages locally.

## Launcher

- `Source/Veronica.Setup`: one self-contained .NET exe with an Avalonia window. It ships the public Avalonia, unlike the plugin, which uses the Editor's fork. It shares the settings and release code with the plugin through `Veronica.Agent`.
- The install root is the folder the Launcher runs from. When started elsewhere, it copies itself into the chosen folder and restarts from the copy; the original stays where it was and, started again, shows a fresh first run.
- First run installs the latest release with the running Launcher; the Launcher updates itself at the next update.
- A Start menu or Desktop shortcut that fails does not stop the install.
- A running single-file exe loads its assemblies lazily from its own file, so it is never renamed while it still works: self-update renames it only right before it exits.
- One instance runs per install folder. A second start brings the first to the front and passes on its request, such as the panel's update.
- Nothing touches the chosen folder before the install starts. The install runs in the background while the modder sets up a provider; a provider's setup saves only once the install is done and its check passes.
- Each provider's check is cheap, with no prompt and no model load: the local model's files are present and verified, `claude` is found and signed in, the OpenAI-compatible server lists the chosen model.
- Names that come from a download (zip entries, Hugging Face file names, manifest model ids) must stay inside their folder; anything else fails the install or the download.
- Operations that need the Editor closed (model delete, runtime swap, `.vrgproj` edits) wait for it where they were started, cancellably.

## Providers

- The OpenAI-compatible address may be partial: a bare host, or nothing for this computer. The Launcher tries the schemes, the common local server ports and the usual API prefixes at once, takes the first server in a fixed order, and saves the full URL.
- The probes go without the API key; only a server that answers 401 or 403 is asked again with it.
- Self-signed certificates are accepted, as by the provider itself: local servers are typically self-signed.

## Install folder

- `Runtime` holds the plugin with its private DLLs and the runtime with its native DLLs and manifest. It is replaced as a whole on update.
- `Skills` and `Wiki` hold the shipped Markdown plus anything the user adds; they are merged on update.
- `Models` holds one folder per installed model; `Downloads` holds partial downloads.
- `Veronica.settings.json` holds the settings and survives updates. `Veronica.installed.json` is the Launcher's record of the install: the release version, the hash of every skill and wiki file it installed, each model's pin, and which waiting conflicts are drops.
- An installed plugin finds everything relative to itself, so an install folder can sit anywhere. A development build uses the paths its build wrote in (`EditorPlugin.md`).

## Settings

- Flat string key/value pairs. Each key is a constant on one `Settings` class whose value is its own name, so the same name works in the file and as an environment variable.
- The store is filled from the settings file, then from environment variables, which win. An unreadable file is logged and skipped. The plugin reads the store once when its panel is created; a change takes effect at the next Editor start.
- The Launcher writes the file: the provider, its settings and the active model.
- The API key is encrypted with Windows DPAPI for the current user, so a copied install folder does not carry it in the clear.

## Models

- The runtime's manifest pins each model's weights (a Hugging Face commit) and library (sha256); the Launcher downloads exactly those revisions.
- Targets are 8 GB and 16 GB cards, and the shipped models are chosen to fit them.
- **Requirements check:** the runtime's own requirements check (features, limits, VRAM) runs against every model in the manifest before any weights download. Every model is shown with its size and result, and one that fits is recommended. A model fits by total VRAM, not free VRAM, since the Editor's VRAM is evicted during turns (`VramSharing.md`); low free VRAM is a warning that other applications will slow the model. A failed requirement is shown with its reason and can be overridden. Until the check exists, the list shows each model's download size and the manifest's VRAM need only (`Docs/TODOs/RequirementsCheck.md`).
- A download lands in `Downloads`, resumes after an interruption, is verified against the Hugging Face hashes and the manifest's sha256, and only then moves into `Models`. Everything is on disk before the runtime loads a model, so the engine stays offline.
- Every Launcher request rides out a failed connection or a temporary error status with growing waits, for a bounded time, before it fails; a `Retry-After` beyond that time fails it at once. Hugging Face answers 429 "maximum queue size reached" when its backend is overloaded, whatever the caller's quota, and a model download is hundreds of requests, so one such answer must not fail it. The wait is only logged, not shown.
- Several models may be installed. The settings name the active one; the plugin runs it and offers no choice.

## Enabling Veronica in a mod

- Local mods come from the mod HUB's mod index. Whether a mod has Veronica is read from its `.vrgproj`, never recorded separately, so there is one source of truth.
- Enabling adds `Veronica.dll` to the `.vrgproj`'s extension assemblies and this install's absolute `Runtime` path to its search paths. Disabling removes `Veronica.dll`, this install's path and any search path whose folder holds a `Veronica.dll`; the mod's other paths stay.
- A mod that loads `Veronica.dll` through any other path (a development build, another or a moved install) is another build: the Launcher offers only to remove it, never to enable over it.
- A `.vrgproj` is edited only while the Editor is closed, since an open Editor may write the project back over the edit.
- The Editor refuses a project whose extension assembly it cannot find, so a collaborator without Veronica at the same path cannot open the mod. This cost is accepted.
- **Starting the Editor:** the Launcher starts the Editor on a mod the way the retail mod HUB does, enabling Veronica first when the mod has none. The Editor is the Space Engineers 2 Mod SDK and the game is Space Engineers 2, both found once in Steam's own files (Steam need not run) and saved; without the SDK in Steam the modder picks the Editor exe. Beside a development Editor, the game is the checkout's own build. Without a game the Editor starts without one. Picking another Editor drops the saved game, which belonged to the old one.
- The Editor gets the HUB's arguments. `--globalAssemblyDirs` with the Editor's folder is required: the SDK keeps the game assemblies Vanilla references there, and without it the Editor fails to start. The `GameData` folders follow the Editor's layout, SDK or development checkout.
- Game and Editor versions are not compared; a mismatch is left to the Editor.

## Updates

- The plugin checks the latest release once per Editor start, and its update button starts the Launcher in update mode, which waits for the Editor to exit: the Editor keeps `Runtime`'s DLLs locked.
- **Self-update first:** Windows refuses to overwrite a running exe but lets it be renamed. The Launcher renames itself to `.old`, moves the new exe in and restarts, renaming back if the move fails. The restarted instance deletes the `.old` file and does not self-update again in that run, so an exe stamped lower than its tag cannot loop.
- **Runtime:** the zip unpacks into `Runtime.new`; right before the swap the Editor is checked closed, then `Runtime` becomes `Runtime.old` and `Runtime.new` becomes `Runtime`. A failed swap puts the old one back, so there is never a moment without a working runtime.
- **Models:** an installed model whose pin changed is replaced without asking; the old revision goes once the new one is verified.
- **Skills and wiki** merge against the installed record's hashes:
  - a file the user did not change takes the new version, or is deleted when the release drops it;
  - a file the user changed stays, and the new version waits beside it as `name.new.md` (empty when the release drops the file) until the user picks one; unresolved conflicts are listed again on the next run; a waiting file the disk has caught up with is removed;
  - a file the user added is left alone.
- The skill and wiki loaders skip `*.new.md`.
