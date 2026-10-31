# Board stale after a project reload

**Exists:** the Board keeps its last graph across project reloads (`Docs/Tech/EditorUi.md`, Board).

**Missing:** after a reload the nodes still hold the previous project's items and Content Browser view models. The tiles stop updating, and a click or double click does nothing (the explorer finds no such file in the new project; the failure only reaches the Editor log).

**Options (owner decision):** resolve the nodes again by path on reload and rebind them, empty the Board on reload, or mark the graph as stale. Start in `Ui/Board.cs` and `VeronicaPlugin.OnProjectLoaded`.
