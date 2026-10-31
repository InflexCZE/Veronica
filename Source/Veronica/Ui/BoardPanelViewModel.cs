using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Editor.Services.Docking;
using Keen.VRage.Editor.Windows.ProjectExplorer.ExplorerItems;
using Veronica.Editing;

namespace Veronica.Ui;

// The Board: the last graph present_graph showed. Positions live here, since the dock rebuilds the view on every tab switch.
public sealed class BoardPanelViewModel : DockItemViewModel
{
    private Point _viewportLocation;
    private double _viewportZoom = 1;

    public BoardPanelViewModel()
    {
        Header = "Veronica Board";
        ContentTemplateResourceKey = "VeronicaBoardPanel";
        CanClose = true;
        CanFloat = true;
    }

    public static BoardPanelViewModel? Current { get; set; }

    public ObservableCollection<BoardNode> Nodes { get; } = new();

    public ObservableCollection<BoardEdge> Edges { get; } = new();

    public ObservableCollection<BoardLabel> Labels { get; } = new();

    // False until the view has measured the nodes and placed them.
    public bool Arranged { get; private set; } = true;

    public event Action? GraphChanged;

    public Point ViewportLocation
    {
        get => _viewportLocation;
        set
        {
            if (_viewportLocation == value)
                return;
            _viewportLocation = value;
            OnPropertyChanged(nameof(ViewportLocation));
        }
    }

    public double ViewportZoom
    {
        get => _viewportZoom;
        set
        {
            if (_viewportZoom == value)
                return;
            _viewportZoom = value;
            OnPropertyChanged(nameof(ViewportZoom));
        }
    }

    public void Present(IReadOnlyList<BoardNode> nodes, IEnumerable<(int From, int To, string? Label)> edges)
    {
        foreach (var edge in Edges)
            edge.Detach();
        Nodes.Clear();
        Edges.Clear();
        Labels.Clear();

        foreach (var node in nodes)
            Nodes.Add(node);
        foreach (var (from, to, label) in edges)
        {
            var edge = new BoardEdge(nodes[from], nodes[to]);
            Edges.Add(edge);
            if (label != null)
                Labels.Add(new BoardLabel(edge, label));
        }

        Arranged = false;
        GraphChanged?.Invoke();
    }

    // Called by the view once every node has its size.
    public Rect Arrange()
    {
        var at = GraphLayout.Arrange(
            Nodes.Select(n => (n.Size.Width, n.Size.Height)).ToList(),
            Edges.Select(e => (Nodes.IndexOf(e.From), Nodes.IndexOf(e.To))),
            GraphLayout.GapFor(Labels.Select(l => l.Size.Width).DefaultIfEmpty(0).Max()));
        for (int i = 0; i < Nodes.Count; i++)
            Nodes[i].Location = new Point(at[i].X, at[i].Y);
        Arranged = true;
        return Extent();
    }

    public Rect Extent()
    {
        if (Nodes.Count == 0)
            return default;
        double left = Nodes.Min(n => n.Location.X), top = Nodes.Min(n => n.Location.Y);
        double right = Nodes.Max(n => n.Location.X + n.Size.Width), bottom = Nodes.Max(n => n.Location.Y + n.Size.Height);
        return new Rect(left, top, right - left, bottom - top);
    }
}

public abstract class BoardItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class BoardNode(ExplorerAssetFile asset, ProjectExplorerFileViewModel? tile, string? note) : BoardItem
{
    private Point _location;
    private Point _drawn;
    private Size _size;

    public ExplorerAssetFile Asset { get; } = asset;

    // Null when the Content Browser has no view model for the item; the node then shows its name only.
    public ProjectExplorerFileViewModel? Tile { get; } = tile;

    public bool HasTile => Tile != null;

    public string Name { get; } = Path.GetFileName(asset.ProjectRelativePath);

    public string? Note { get; } = note;

    public bool HasNote => Note != null;

    public Point Location
    {
        get => _location;
        set
        {
            if (_location == value)
                return;
            _location = value;
            Changed(nameof(Location));
            Drawn = value;
        }
    }

    // A drag moves the node by a render transform and commits Location only when it ends; edges follow this instead.
    public Point PreviewLocation
    {
        get => Drawn;
        set => Drawn = value;
    }

    public Point Drawn
    {
        get => _drawn;
        private set
        {
            if (_drawn == value)
                return;
            _drawn = value;
            Changed(nameof(Drawn));
        }
    }

    public Size Size
    {
        get => _size;
        set
        {
            if (_size == value)
                return;
            _size = value;
            Changed(nameof(Size));
        }
    }
}

// From the referencing item's right side to the referenced item's left side, matching the left-to-right layout.
public sealed class BoardEdge : BoardItem
{
    public BoardEdge(BoardNode from, BoardNode to)
    {
        From = from;
        To = to;
        From.PropertyChanged += OnNodeChanged;
        To.PropertyChanged += OnNodeChanged;
    }

    public BoardNode From { get; }

    public BoardNode To { get; }

    public Point Source => new(From.Drawn.X + From.Size.Width, From.Drawn.Y + From.Size.Height / 2);

    public Point Target => new(To.Drawn.X, To.Drawn.Y + To.Size.Height / 2);

    public event Action? Moved;

    public void Detach()
    {
        From.PropertyChanged -= OnNodeChanged;
        To.PropertyChanged -= OnNodeChanged;
    }

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(BoardNode.Drawn) or nameof(BoardNode.Size)))
            return;
        Changed(nameof(Source));
        Changed(nameof(Target));
        Moved?.Invoke();
    }
}

// An edge's label, centred on the middle of its edge.
public sealed class BoardLabel : BoardItem
{
    private readonly BoardEdge _edge;
    private Size _size;

    public BoardLabel(BoardEdge edge, string text)
    {
        _edge = edge;
        Text = text;
        edge.Moved += () => Changed(nameof(Location));
    }

    public string Text { get; }

    public Size Size
    {
        get => _size;
        set
        {
            if (_size == value)
                return;
            _size = value;
            Changed(nameof(Location));
        }
    }

    public Point Location => new(
        (_edge.Source.X + _edge.Target.X - Size.Width) / 2,
        (_edge.Source.Y + _edge.Target.Y - Size.Height) / 2);
}
