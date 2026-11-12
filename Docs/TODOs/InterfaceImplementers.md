# Finding the components that implement an interface

**Exists:** `component_add` names, in its reply, the components that provide an interface dependency the Editor's filling skips (`Docs/Tech/Tools_Creators.md`, interface dependencies). `describe` lists base types and concrete derived types but filters interfaces out. No tool answers "which components implement this interface" on its own.

**Missing:** a tool the model can call when a dependency is missing or a behaviour comes through an interface, such as power draw through `IConsumedResourceModifier`, that lists the candidates:
- on a prefab: which of its components implement the interface;
- across the whole game: every component type that implements it, so the model knows what it could add.

**Where to start:** `DescribeTool.cs` (where interfaces are filtered out), the lookup `component_add` already does for interface dependencies; `Type.GetInterfaces()` over the game assemblies, filtered to game namespaces.

**Owner decision:** wanted. Open questions:
- A new tool, or a mode of `describe` or `find_content`?
- Does the game-wide list include components no vanilla prefab uses?
- Are interface names shown with their doc summary, so the model knows what the interface means?
