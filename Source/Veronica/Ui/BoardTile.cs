using Keen.VRage.Editor.Windows.ProjectExplorer.ExplorerItems;

namespace Veronica.Ui;

// The Content Browser's tile, for show only. It shares the browser's item view model, whose F2 and
// new-item events would otherwise start a rename or take focus on the Board too.
public sealed class BoardTile : ProjectExplorerFile
{
    public BoardTile()
    {
        Focusable = false;
        IsHitTestVisible = false;
    }

    protected override Type StyleKeyOverride => typeof(ProjectExplorerFile);

    protected override void StartRename()
    {
    }
}
