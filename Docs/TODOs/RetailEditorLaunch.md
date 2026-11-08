# Start Editor with a retail Editor

**Exists:** Start Editor on the Mods page starts the `VRage.Editor.exe` the user picked with `--project` and `--globalProjectDirs`. The `GameData` folders are found above the exe as a development checkout lays them out: the first folder holding both `Game2\GameData` and `VRage\GameData` (`EditorLaunch.GlobalProjectDirs`). With only `--project` the Editor fails to start: it cannot find the mod's dependencies (Vanilla, Engine).

**Missing:**
- Where a shipped Editor sits relative to the game's and the engine's `GameData`. The HUB install on the development machine (`%APPDATA%\VRAGE.HUB\Installs\SE2R`) holds `GameData\Vanilla` and `VRage\GameData\Engine` but no `VRage.Editor.exe`, so the HUB may pass folders from elsewhere. Until then a retail Editor gets the error that no folder above it holds both.
- Whether `--projectExecutableSearchPaths` (the game folder) is needed beyond starting, e.g. to play the mod from the Editor. The Editor started and loaded Veronica without it and without `--globalAssemblyDirs`.

**Start:** look at how the HUB starts a retail Editor (its command line, as the Editor logs it in `Environment.CommandLine`), then extend `EditorLaunch.GlobalProjectDirs`.
