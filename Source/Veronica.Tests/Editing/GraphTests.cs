using System.Text.Json;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class GraphTests
{
    private static Graph Parse(string json) => Graph.Parse(JsonDocument.Parse(json).RootElement);

    private static string Error(string json) => Assert.Throws<EditError>(() => Parse(json)).Message;

    [Fact]
    public void ParsesNodesAndEdges()
    {
        var graph = Parse("""
            {"nodes":[{"id":"prefab","item":"A.def","note":"the block"},{"id":"flame","item":"B.def"}],
             "edges":[{"from":"prefab","to":"flame","label":"Flame"}]}
            """);

        Assert.Equal([new GraphNode("prefab", "A.def", "the block"), new GraphNode("flame", "B.def", null)], graph.Nodes);
        Assert.Equal([new GraphEdge("prefab", "flame", "Flame")], graph.Edges);
        Assert.Equal(1, graph.IndexOf("flame"));
    }

    [Fact]
    public void EdgesAreOptional_AndNodesNeedNoEdge()
    {
        var graph = Parse("""{"nodes":[{"id":"a","item":"A.def"},{"id":"b","item":"B.def"}]}""");

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void TakesArraysSentAsJsonText()
    {
        var graph = Parse("""{"nodes":"[{\"id\":\"a\",\"item\":\"A.def\"}]","edges":"[]"}""");

        Assert.Single(graph.Nodes);
    }

    [Fact]
    public void EdgeIdsMatchIgnoringCase_AndPrintAsTheNodeHasThem()
    {
        var graph = Parse("""{"nodes":[{"id":"Prefab","item":"A.def"},{"id":"b","item":"B.def"}],"edges":[{"from":"prefab","to":"B"}]}""");

        Assert.Equal(new GraphEdge("Prefab", "b", null), graph.Edges[0]);
    }

    [Fact]
    public void UnknownId_ListsTheIds() =>
        Assert.Contains("the ids are: a, b", Error("""{"nodes":[{"id":"a","item":"A"},{"id":"b","item":"B"}],"edges":[{"from":"a","to":"c"}]}"""));

    [Fact]
    public void DuplicateId_IsRefused() =>
        Assert.Contains("Two nodes have the id 'A'", Error("""{"nodes":[{"id":"a","item":"A"},{"id":"A","item":"B"}]}"""));

    [Fact]
    public void SelfEdge_IsRefused() =>
        Assert.Contains("to itself", Error("""{"nodes":[{"id":"a","item":"A"}],"edges":[{"from":"a","to":"a"}]}"""));

    [Fact]
    public void RepeatedEdge_IsRefused_ButTheReverseIsNot()
    {
        Assert.Contains("given twice", Error("""{"nodes":[{"id":"a","item":"A"},{"id":"b","item":"B"}],"edges":[{"from":"a","to":"b"},{"from":"a","to":"b","label":"X"}]}"""));

        Assert.Equal(2, Parse("""{"nodes":[{"id":"a","item":"A"},{"id":"b","item":"B"}],"edges":[{"from":"a","to":"b"},{"from":"b","to":"a"}]}""").Edges.Count);
    }

    [Fact]
    public void Limits_AreChecked()
    {
        string many = string.Join(",", Enumerable.Range(0, Graph.MaxNodes + 1).Select(i => $"{{\"id\":\"n{i}\",\"item\":\"I{i}\"}}"));
        string tooMany = Error($"{{\"nodes\":[{many}]}}");
        Assert.Contains($"at most {Graph.MaxNodes}", tooMany);
        Assert.Contains("Drop 1 of them", tooMany);

        string note = new('x', Graph.MaxNote + 1);
        Assert.Contains($"at most {Graph.MaxNote}", Error($"{{\"nodes\":[{{\"id\":\"a\",\"item\":\"A\",\"note\":\"{note}\"}}]}}"));

        string label = new('x', Graph.MaxLabel + 1);
        Assert.Contains($"at most {Graph.MaxLabel}", Error($"{{\"nodes\":[{{\"id\":\"a\",\"item\":\"A\"}},{{\"id\":\"b\",\"item\":\"B\"}}],\"edges\":[{{\"from\":\"a\",\"to\":\"b\",\"label\":\"{label}\"}}]}}"));
    }

    [Fact]
    public void MissingParts_AreNamed()
    {
        Assert.Equal("nodes is missing.", Error("{}"));
        Assert.Contains("nodes is empty", Error("""{"nodes":[]}"""));
        Assert.Equal("nodes[0].item is missing.", Error("""{"nodes":[{"id":"a"}]}"""));
        Assert.Equal("edges[0].to is missing.", Error("""{"nodes":[{"id":"a","item":"A"}],"edges":[{"from":"a"}]}"""));
    }
}

public sealed class GraphLayoutTests
{
    [Fact]
    public void ReferencedItems_StandRightOfTheirReferrers()
    {
        var at = GraphLayout.Arrange([(100, 150), (100, 150), (100, 150)], [(0, 1), (1, 2)]);

        Assert.True(at[0].X + 100 <= at[1].X, $"{at[0]} {at[1]}");
        Assert.True(at[1].X + 100 <= at[2].X, $"{at[1]} {at[2]}");
    }

    [Fact]
    public void StartsAtTheOrigin_AndNodesDoNotOverlap()
    {
        var sizes = new List<(double, double)> { (120, 200), (80, 100), (150, 120), (100, 100), (100, 100) };
        var at = GraphLayout.Arrange(sizes, [(0, 1), (0, 2), (2, 3)]);

        Assert.Equal(0, at.Min(p => p.X), 3);
        Assert.Equal(0, at.Min(p => p.Y), 3);
        for (int i = 0; i < at.Count; i++)
        for (int j = i + 1; j < at.Count; j++)
        {
            bool apart = at[i].X + sizes[i].Item1 <= at[j].X || at[j].X + sizes[j].Item1 <= at[i].X
                || at[i].Y + sizes[i].Item2 <= at[j].Y || at[j].Y + sizes[j].Item2 <= at[i].Y;
            Assert.True(apart, $"nodes {i} and {j} overlap: {at[i]} {at[j]}");
        }
    }

    [Fact]
    public void ASingleNode_IsPlaced() =>
        Assert.Equal((0d, 0d), GraphLayout.Arrange([(100, 100)], [])[0]);
}
