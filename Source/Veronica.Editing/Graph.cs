using System.Text.Json;

namespace Veronica.Editing;

public sealed record GraphNode(string Id, string Item, string? Note);

public sealed record GraphEdge(string From, string To, string? Label);

// present_graph's arguments, checked before any item is resolved.
public sealed class Graph
{
    public const int MaxNodes = 25;
    public const int MaxEdges = 30;
    public const int MaxNote = 300;
    public const int MaxLabel = 30;

    private Graph(List<GraphNode> nodes, List<GraphEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
    }

    public IReadOnlyList<GraphNode> Nodes { get; }

    public IReadOnlyList<GraphEdge> Edges { get; }

    public int IndexOf(string id)
    {
        for (int i = 0; i < Nodes.Count; i++)
            if (Nodes[i].Id == id)
                return i;
        return -1;
    }

    public static Graph Parse(JsonElement arguments)
    {
        var nodeElements = Array(arguments, "nodes") ?? throw new EditError("nodes is missing.");
        var edgeElements = Array(arguments, "edges") ?? [];

        if (nodeElements.Count == 0)
            throw new EditError("nodes is empty; give at least one node.");
        if (nodeElements.Count > MaxNodes)
            throw new EditError($"{nodeElements.Count} nodes; at most {MaxNodes} fit on the board. Drop {nodeElements.Count - MaxNodes} of them and keep the items the answer is about.");
        if (edgeElements.Count > MaxEdges)
            throw new EditError($"{edgeElements.Count} edges; at most {MaxEdges} fit on the board.");

        var nodes = new List<GraphNode>();
        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < nodeElements.Count; i++)
        {
            var element = Object(nodeElements[i], $"nodes[{i}]");
            string id = Text(element, "id", $"nodes[{i}]");
            string item = Text(element, "item", $"nodes[{i}]");
            string? note = OptionalText(element, "note", $"nodes[{i}]");
            if (note is { Length: > MaxNote })
                throw new EditError($"Node '{id}': the note has {note.Length} characters; at most {MaxNote}.");
            if (!ids.TryAdd(id, id))
                throw new EditError($"Two nodes have the id '{id}'; give each node its own id.");
            nodes.Add(new GraphNode(id, item, string.IsNullOrEmpty(note) ? null : note));
        }

        var edges = new List<GraphEdge>();
        var pairs = new HashSet<(string, string)>();
        for (int i = 0; i < edgeElements.Count; i++)
        {
            var element = Object(edgeElements[i], $"edges[{i}]");
            string from = NodeId(ids, Text(element, "from", $"edges[{i}]"));
            string to = NodeId(ids, Text(element, "to", $"edges[{i}]"));
            string? label = OptionalText(element, "label", $"edges[{i}]");
            if (label is { Length: > MaxLabel })
                throw new EditError($"Edge {from} -> {to}: the label has {label.Length} characters; at most {MaxLabel}, such as the field that holds the reference.");
            if (from == to)
                throw new EditError($"Edge {from} -> {to} goes from a node to itself; remove it.");
            if (!pairs.Add((from, to)))
                throw new EditError($"Edge {from} -> {to} is given twice; keep one.");
            edges.Add(new GraphEdge(from, to, string.IsNullOrEmpty(label) ? null : label));
        }

        return new Graph(nodes, edges);
    }

    private static string NodeId(Dictionary<string, string> ids, string id) =>
        ids.TryGetValue(id, out var known)
            ? known
            : throw new EditError($"No node has the id '{id}'; the ids are: {string.Join(", ", ids.Values)}.");

    // Models sometimes send an array as a JSON string.
    private static List<JsonElement>? Array(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.String)
        {
            try
            {
                value = JsonDocument.Parse(value.GetString()!).RootElement;
            }
            catch (JsonException)
            {
                throw new EditError($"{name} must be an array of objects.");
            }
        }
        if (value.ValueKind != JsonValueKind.Array)
            throw new EditError($"{name} must be an array of objects.");
        return value.EnumerateArray().ToList();
    }

    private static JsonElement Object(JsonElement element, string where) =>
        element.ValueKind == JsonValueKind.Object ? element : throw new EditError($"{where} must be an object.");

    private static string Text(JsonElement element, string name, string where) =>
        OptionalText(element, name, where) is { Length: > 0 } text ? text : throw new EditError($"{where}.{name} is missing.");

    private static string? OptionalText(JsonElement element, string name, string where)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!.Trim(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Null => null,
            _ => throw new EditError($"{where}.{name} must be text."),
        };
    }
}
