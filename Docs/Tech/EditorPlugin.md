# Editor plugin

`Veronica.dll` is an Editor project plugin: a `[ProjectPlugin]` class implementing `IProjectPlugin`.

## Loading

- The Editor loads the assemblies a `.vrgproj` names in `EditorExtensionAssemblies`, looked up through `EditorExtensionAssembliesSearchPaths`. Every project in the loaded dependency graph contributes.
- Veronica is declared in the modder's Mod project, never in `Vanilla.vrgproj`, so Keen's files stay untouched and syncing them never conflicts.
- Search paths expand no environment variables, and relative paths resolve against the declaring `.vrgproj`. An install therefore writes its absolute path (`Distribution.md`).
- Assemblies load into the default load context. Veronica shares the Editor's copy of every library the Editor ships, and an Editor assembly with the same name as one of ours wins. Private dependencies sit beside `Veronica.dll`.
- The plugin object is created early in engine construction, which is the only moment early enough for VRAM borrow tracking (`VramSharing.md`). UI work waits for `OnProjectLoaded`, which runs after the main window and docking exist.
- **Discovery:** the Editor finds plugin classes through two index attributes that VRage's source generator normally emits. The generator is not public, so Veronica writes them by hand.

## Build

- Plain `Microsoft.NET.Sdk`. Keen's SDK, generators and Editor sources are not public, so the build depends only on the Editor's binary folder, which every modder has. Compile references are the Editor's DLLs, never copied to the output.
- The Editor ships Keen's fork of Avalonia 11.0.10 stamped with a lower assembly version than the public package, and .NET never binds a reference to a lower loaded version. The plugin therefore compiles against the Editor's Avalonia DLLs and takes the public package for its XAML compiler only.
- XAML bindings are compiled, so a binding error fails the build instead of showing up only as a blank control at runtime.
- No `[Notify]` or other generated helpers: view models implement property notification by hand.
- A plugin compiled against VRage build N does not load into an Editor with a lower build, so the plugin is compiled against the Editor the machine config points at. The oldest Editor a release supports is open (`Docs/TODOs/EditorVersionFloor.md`).
- **Machine config:** an untracked `Directory.Build.local.props` names the Editor source checkout; the Editor, game and `GameData` folders derive from it and each can be overridden, for example to build against a retail Editor.
- A development build has no install folder around it, so the build writes the repo paths of `WebQwen.exe`, the skills and the wiki into `Veronica.dll` as assembly metadata.

## Dependencies

- Private dependencies ship beside `Veronica.dll`: `Veronica.Agent.dll`, `Veronica.Editing.dll`, `System.Security.Cryptography.ProtectedData.dll` for the DPAPI-encrypted API key (`Distribution.md`), and MSAGL (`AutomaticGraphLayout`, MIT) for the Board's graph layout.
- A library the Editor ships, such as Nodify for the Board, is referenced from the Editor like its other DLLs and never shipped.

## Dev mod

- `Assets/VeronicaDevMod` is a hand-written Mod project that depends on Vanilla and loads `Veronica.dll` from the build output. It holds no machine paths.
- The Editor finds Vanilla and the game through the arguments the mod HUB passes. They are built once in `Veronica.csproj` and shared by `Launch-Editor.ps1` and the Visual Studio launch profile.
- The Editor keeps the loaded `Veronica.dll` locked; rebuilding needs the Editor closed.

## Panels

- The Editor's docking registration (view model registry, dock attributes, Window menu) is internal. A plugin can only use the public `DockItemViewModel` and `DockManager.AddViewModel`.
- A panel follows the Editor's own pattern: `Content` stays null and a `ContentTemplateResourceKey` names a `DataTemplate` merged into the application resources. A control placed in `Content` would be serialized into the saved layout. The template is untyped, since a typed one does not match the null content.
- A tabbed dock group rebuilds the selected tab's view on every switch, so all panel state lives in the view model and a view subscribes only while attached.
- Layout persistence drops view models the Editor does not know, and Restore Layout replaces the whole set. The panel is therefore added again on every project load and from the menu, never restored. The saved layout keeps the tab itself, so its position is remembered; a project without Veronica shows the tab empty.
- The saved layout keys a tab by its `DockId`, by default the view model's type name. The bare MCP panel takes the chat panel's id, so either mode fills the one remembered tab.
- The chat panel docks in the right-hand group, or the first tabbed group when the user's layout has none: a dock item whose group is missing is never shown.
- **Menu:** the Window menu is built before plugins load and has no extension point, so Veronica adds its own top-level menu, which re-adds the panel and selects it.

## Frozen viewport

- While the Editor is frozen for the local model, each visible viewport shows a badge over its last frame.
- A viewport is a native child window that Avalonia cannot draw over, so the badge is a `Popup`, a top-level window, as the Editor's own flyouts are. The popup gets the viewport as its logical parent; without one its window finds no theme and opens empty.
- A badge that fails to show never fails the freeze.

## Fonts and theme

- The Editor's theme sets its Roboto font through a relative URI, and Avalonia's glyph fallback throws on relative font URIs: any character missing from Roboto and the system default font (emoji, CJK, many symbols) crashes the whole Editor during layout.
- Every text control in Veronica's views therefore gets an absolute or system font: Roboto through its full resource URI for UI text, and a Segoe UI family with emoji and symbol coverage for model text and user input. Text written to Editor surfaces outside these views is not covered (`Docs/TODOs/EditorTextOutside.md`).
- Views use the Editor's controls and shared colour and size resources through `DynamicResource`, so they follow the Editor's theme.
