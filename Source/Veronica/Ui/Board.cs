using Avalonia.Controls;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.UI.UniDock.Docking;

namespace Veronica.Ui;

// The Board panel: added on its first graph, and again whenever the user closed it or a layout restore dropped it.
public static class Board
{
    // The Render View's group: a graph needs the main area.
    private const string PreferredDockGroup = "CentralPanelDockGroup";

    // UI thread. False when the Editor's layout has nowhere to show the Board.
    public static bool Present(IReadOnlyList<BoardNode> nodes, IEnumerable<(int From, int To, string? Label)> edges)
    {
        var board = BoardPanelViewModel.Current ??= new BoardPanelViewModel();
        board.Present(nodes, edges);
        if (!Dock(board))
            return false;
        board.Select();
        return true;
    }

    private static bool Dock(BoardPanelViewModel board)
    {
        if (EditorShell.MainWindow?.FindResource("TheDockManager") is not DockManager dock)
        {
            Log.Default.WriteLine("[Veronica] Editor dock manager not found, Board not shown.");
            return false;
        }
        if (dock.ContainsViewModel(board))
            return true;

        string? group = dock.ConnectedGroups.Any(g => g.DockId == PreferredDockGroup) ? PreferredDockGroup : EditorShell.PickDockGroup(dock);
        if (group == null)
        {
            Log.Default.WriteLine("[Veronica] No dock group in the current layout, Board not shown.");
            return false;
        }
        board.DefaultDockGroupId = group;
        if (dock.AddViewModel(board))
            return true;
        Log.Default.WriteLine("[Veronica] Editor dock manager has no panel list, Board not shown.");
        return false;
    }
}
