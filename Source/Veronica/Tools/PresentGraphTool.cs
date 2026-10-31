using System.Text.Json;
using Keen.VRage.ContentPipeline.Project.Structure;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;
using Veronica.Ui;

namespace Veronica.Tools;

public sealed class PresentGraphTool : ITool
{
    public string Name => "present_graph";

    public string Description =>
        "Shows the user a graph of content items on the Board, in the Editor's main area, to explain how they connect. " +
        "Each node is one content item, drawn as its Content Browser tile with an optional note under it. " +
        "An edge always points from the item that references to the item it references; its label names the field that holds the reference. " +
        "Explain in text as well; the graph supports the answer.";

    public string Parameters => $$$$"""
        {"type":"object","properties":{
          "nodes":{"type":"array","description":"At most {{{{Graph.MaxNodes}}}} items, each once","items":{"type":"object","properties":{
            "id":{"type":"string","description":"Short name for the node that edges use, e.g. prefab or flame_emitter"},
            "item":{"type":"string","description":"Project path of the content item, or its file name if unique"},
            "note":{"type":"string","description":"Optional text under the tile, at most {{{{Graph.MaxNote}}}} characters"}},"required":["id","item"]}},
          "edges":{"type":"array","description":"At most {{{{Graph.MaxEdges}}}}","items":{"type":"object","properties":{
            "from":{"type":"string","description":"id of the node that references"},
            "to":{"type":"string","description":"id of the node referenced"},
            "label":{"type":"string","description":"Optional, at most {{{{Graph.MaxLabel}}}} characters: the field that holds the reference, e.g. Effect"}},"required":["from","to"]}}},
         "required":["nodes","edges"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var graph = Graph.Parse(arguments);
        return new ToolResult(await EditorProject.RunAsync(project => Present(project, graph), cancellationToken));
    }

    private static string Present(LoadedProject project, Graph graph)
    {
        var assets = new List<ExplorerAssetFile>();
        var holders = new Dictionary<ExplorerAssetFile, string>();
        foreach (var node in graph.Nodes)
        {
            ExplorerAssetFile asset;
            try
            {
                asset = Content.Resolve(project, node.Item);
            }
            catch (EditError error)
            {
                throw new EditError($"Node '{node.Id}': {error.Message}");
            }
            if (!holders.TryAdd(asset, node.Id))
                throw new EditError($"Nodes '{holders[asset]}' and '{node.Id}' are both {Content.Shown(asset)}; keep '{holders[asset]}' and point the edges at it.");
            assets.Add(asset);
        }

        var nodes = graph.Nodes.Select((n, i) => new BoardNode(assets[i], Explorer.Tile(assets[i]), n.Note)).ToList();
        var edges = graph.Edges.Select(e => (graph.IndexOf(e.From), graph.IndexOf(e.To), e.Label));
        if (!Board.Present(nodes, edges))
            throw new EditError("The Editor's layout has no place for the Board, so the graph was not shown; explain in text instead.");
        return "Shown to the user.";
    }
}
