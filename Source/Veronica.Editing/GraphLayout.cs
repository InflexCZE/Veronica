using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;
using MsaglNode = Microsoft.Msagl.Core.Layout.Node;
using MsaglEdge = Microsoft.Msagl.Core.Layout.Edge;

namespace Veronica.Editing;

// Node positions for the Board: layered, left to right, with screen coordinates (y down) starting at 0, 0.
public static class GraphLayout
{
    public const double LayerGap = 90;
    public const double NodeGap = 30;

    // An edge's label sits halfway between its nodes, so columns stand far enough apart for the widest one.
    public static double GapFor(double widestLabel) => Math.Max(LayerGap, widestLabel + 2 * NodeGap);

    public static IReadOnlyList<(double X, double Y)> Arrange(IReadOnlyList<(double Width, double Height)> sizes, IEnumerable<(int From, int To)> edges, double layerGap = LayerGap)
    {
        if (sizes.Count == 0)
            return [];

        var graph = new GeometryGraph();
        var nodes = sizes
            .Select(s => new MsaglNode(CurveFactory.CreateRectangle(Math.Max(s.Width, 1), Math.Max(s.Height, 1), new Point(0, 0))))
            .ToList();
        foreach (var node in nodes)
            graph.Nodes.Add(node);
        foreach (var (from, to) in edges)
            graph.Edges.Add(new MsaglEdge(nodes[from], nodes[to]));

        var settings = new SugiyamaLayoutSettings
        {
            LayerSeparation = layerGap,
            NodeSeparation = NodeGap,
            Transformation = PlaneTransformation.Rotation(Math.PI / 2),
        };
        // The Board draws its own edges; only node positions are used.
        settings.EdgeRoutingSettings.EdgeRoutingMode = Microsoft.Msagl.Core.Routing.EdgeRoutingMode.None;
        LayoutHelpers.CalculateLayout(graph, settings, null);

        // MSAGL's y grows upward.
        var corners = nodes.Select((n, i) => (X: n.Center.X - sizes[i].Width / 2, Y: -n.Center.Y - sizes[i].Height / 2)).ToList();
        double left = corners.Min(c => c.X);
        double top = corners.Min(c => c.Y);
        return corners.Select(c => (c.X - left, c.Y - top)).ToList();
    }
}
