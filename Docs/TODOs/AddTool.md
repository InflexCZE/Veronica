# Remove the add tool

**Exists:** `Source/Veronica.Agent/Tools/AddTool.cs`, `add(a, b)`, registered in `Source/Veronica/Ui/EditorShell.cs`. It proves the tool path end to end: the model calls a tool, the plugin runs it, and the model answers with the result.

**Missing:** remove it, and its registration, when the first Editor tool lands. The `ToolSetTests` cases that use it then move to that tool or to a test-only tool.
