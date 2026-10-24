# Same path in the mod and a dependency

**Exists:** editor tools name content by project-relative path (`Source/Veronica/Tools/Content.cs`). Paths carry no project name, and resolution spans the mod and its dependencies.

**Missing:** when the mod and a dependency (Vanilla) hold a file at the same relative path, the name is ambiguous and nothing the model can send picks one: the error lists the same path twice. `find_content` does not show which project a file is in either. Owner decision: prefer the mod's item, or add a project qualifier to names.
