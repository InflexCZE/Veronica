# Editor plugin: build and loading

`Veronica.dll` is an Editor project plugin: a `[ProjectPlugin]` class implementing `IProjectPlugin` (`VRage.Plugin.Editor`).

## Loading

- The Editor loads plugin assemblies named in a `.vrgproj`'s `EditorExtensionAssemblies`, looked up through `EditorExtensionAssembliesSearchPaths`. Every project in the loaded dependency graph contributes, Mod projects included.
- Veronica is declared in a Mod project, never in `Vanilla.vrgproj`. Keen's files stay untouched and syncing them never conflicts.
- Search paths understand only `{BuildConfiguration}` (the Editor's own configuration) and `{AssemblyName}`, with no environment variables. Relative paths resolve against the declaring `.vrgproj`'s folder.
- Assemblies load with `Assembly.LoadFrom` into the default load context. Veronica shares the Editor's copies of every library the Editor ships (Avalonia, VRage, ...). Private dependencies resolve from the plugin's own folder.
- An Editor assembly with the same name as one of ours wins over ours.
- The plugin object is created early in engine construction. `OnProjectLoaded` runs on the UI thread after the main window is shown and docking is initialized; it is where the plugin touches the UI.

## Discovery

The Editor finds the plugin class through two attributes that VRage's source generator normally emits: `[assembly: IndexMetadata]` (without it the assembly is skipped) and `[module: ModuleIndexedAttributes(typeof(ProjectPluginAttribute), ...)]` (lists the plugin types). Both are public, so Veronica writes them by hand and does not use the generator.

## Build

- Plain `Microsoft.NET.Sdk`, `net9.0-windows`. Keen's `VRage.Sdk`, its generators and the Editor source tree are not public, so the build depends only on the Editor's binary folder, which every modder has.
- Compile references are the Editor's shipped DLLs (`$(EditorDir)`, `Private=false`). Nothing the Editor ships is copied to our output.
- The Editor ships Keen's fork of Avalonia 11.0.10 with assembly version 10.1.11 and some public members removed. .NET never binds a reference to a lower loaded version, so a plugin compiled against the public Avalonia 11.0.10 (assembly 11.0.10.0) fails to load. The public `Avalonia` 11.0.10 package is referenced for its XAML compiler only (no compile or runtime assets); its compiler is identical to the fork's.
- Avalonia's build telemetry package is excluded.
- XAML bindings are compiled by default; binding errors fail the build.
- No `[Notify]` and other generated helpers: view models implement property notification by hand.

## Editor version

A plugin compiled against VRage build N does not load into an Editor with a lower build number. The plugin is compiled against the Editor in the local config.

## Machine config

`Directory.Build.local.props` (untracked, from the template) sets `SE2Root`, the Editor source checkout. `Directory.Build.props` derives `EditorDir`, `GameDir`, `GameDataDir` and `EngineDataDir` from it, using `SE2Config` (default `Debug`) for the Editor and game build folders. Each derived folder can be overridden on its own, for example to point `EditorDir` at a retail Editor install.

## Dependencies

- `Veronica.Agent.dll` (providers, tool loop, WebQwen client) is a private dependency and sits beside `Veronica.dll`.
- `System.Security.Cryptography.ProtectedData.dll` (NuGet, from `Veronica.Agent`) decrypts the API key in the settings file (`Distribution.md`) and is a private dependency beside `Veronica.dll`.
- `AutomaticGraphLayout.dll` (MSAGL, MIT, NuGet `AutomaticGraphLayout`) lays out the Board's graphs and is a private dependency beside `Veronica.dll`. `Nodify.Avalonia.dll` is the Editor's and referenced like its other DLLs.
- An installed plugin finds `WebQwen.exe`, the skills, the wiki and the models relative to itself (`Distribution.md`). A development build runs `WebQwen.exe` from this repo's build output (`Source/WebQwen/bin/x64/<Configuration>/net9.0`) with the models from `.deps/models`; the build writes these paths into `Veronica.dll` as assembly metadata.

## Dev mod

- `Assets/VeronicaDevMod` is a hand-written Mod project that depends on Vanilla by GUID and loads `Veronica.dll` from `Source/Veronica/bin/Debug`. It holds no machine paths.
- The Editor finds Vanilla and the game executable through the arguments the mod HUB passes: `--globalProjectDirs` (Game2 and VRage `GameData`), `--globalAssemblyDirs` (Editor folder) and `--projectExecutableSearchPaths` (game folder).
- These arguments are built once in `Veronica.csproj` (`EditorLaunchArgs`) and used by `Launch-Editor.ps1` and the Visual Studio launch profile.
- The Editor loads `Veronica.dll` straight from the build output and keeps it locked; rebuilding needs the Editor closed.
