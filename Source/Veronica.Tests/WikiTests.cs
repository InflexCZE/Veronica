using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Xunit;

namespace Veronica.Tests;

public sealed class WikiTests
{
    private static string Dir([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "Veronica", "Wiki");

    private static readonly WikiPage GScreen = new("gscreen", "The G-Screen", "src", "Use for G-Screen places.", "Category > Subcategory.");

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Parse_ReadsAllFields()
    {
        var page = WikiFiles.Parse("---\nname: gscreen\ntitle: The G-Screen\nsource: game source\ndescription: Use for\n  G-Screen places.\n---\nBody.");

        Assert.Equal(new WikiPage("gscreen", "The G-Screen", "game source", "Use for G-Screen places.", "Body."), page);
    }

    [Theory]
    [InlineData("---\nname: x\nsource: s\ndescription: d\n---\nb", "no title")]
    [InlineData("---\nname: x\ntitle: t\ndescription: d\n---\nb", "no source")]
    public void Parse_RejectsMissingFields(string text, string message)
    {
        var e = Assert.Throws<FormatException>(() => WikiFiles.Parse(text));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public async Task PageTool_ReturnsTitleAndBody_IgnoringCase()
    {
        var tool = new WikiPageTool(() => [GScreen]);

        Assert.Equal("# The G-Screen\n\nCategory > Subcategory.", (await tool.ExecuteAsync(Args("""{"name":"GScreen"}"""), CancellationToken.None)).Output);
        Assert.Equal("There is no wiki page named 'x'; the pages are: gscreen.", (await tool.ExecuteAsync(Args("""{"name":"x"}"""), CancellationToken.None)).Output);
    }

    [Fact]
    public void ShippedPages_LoadCleanly_InAscii()
    {
        var load = WikiFiles.Load(Dir());

        Assert.Empty(load.Problems);
        Assert.NotEmpty(load.Pages);
        foreach (var file in Directory.EnumerateFiles(Dir(), "*" + Frontmatter.Extension))
        {
            var text = File.ReadAllText(file);
            int bad = text.ToList().FindIndex(c => c > 127 || (c < 32 && c != '\n' && c != '\r' && c != '\t'));
            Assert.True(bad < 0, $"{Path.GetFileName(file)} has a non-ASCII or control character at {bad}");
        }
    }

    [Fact]
    public void Render_QuotesTheTranscriptAndCutsLongResults()
    {
        var call = new ToolCall("c1", "read", "{\"path\":\"a\"}");
        var wiki = new ToolCall("c2", "wiki", "{\"question\":\"q\"}");
        var transcript = new List<ChatMessage>
        {
            new(ChatRole.User, "Make a block"),
            new(ChatRole.Assistant, " Reading. ") { Reasoning = "hidden", ToolCalls = [call, wiki] },
            new(ChatRole.Tool, new string('x', 1500)) { ToolCallId = "c1", Name = "read" },
            new(ChatRole.Tool, new string('w', 1500)) { ToolCallId = "c2", Name = "wiki" },
            new(ChatRole.Assistant, ""),
        };

        var text = TranscriptRender.Render(transcript, new HashSet<string> { "wiki" });

        Assert.Equal(
            "[user]\nMake a block\n\n[assistant]\nReading.\n\n[assistant used read] {\"path\":\"a\"}\n\n[assistant used wiki] {\"question\":\"q\"}\n\n" +
            "[result of read]\n" + new string('x', 1000) + "... (cut, 1500 characters in all)\n\n[result of wiki]\n" + new string('w', 1500),
            text);
    }

    private sealed class FakeProvider(params (string? Text, string? Call)[] rounds) : IProvider
    {
        private readonly Queue<(string? Text, string? Call)> _rounds = new(rounds);

        public List<string> Sessions { get; } = [];
        public List<RoundRequest> Requests { get; } = [];
        public int ContextTokens { get; set; } = 100;
        public int ContextWindow => 1000;
        public bool RunsSubagents => true;

        public IProviderSession OpenSession(string name)
        {
            Sessions.Add(name);
            return ClientToolLoop.CreateSession(Stream);
        }

        private Task Stream(RoundRequest request, Action<StreamDelta> onDelta, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var (text, call) = _rounds.Dequeue();
            onDelta(new StatsDelta(new RoundStats(ContextTokens, 0, 1, 1) { CachedTokens = 0 }));
            if (text != null) onDelta(new TextDelta(text));
            if (call != null) onDelta(new ToolCallDelta(0, "c" + Requests.Count, call, "{\"name\":\"gscreen\"}"));
            onDelta(new DoneDelta(call != null ? "tool_calls" : "stop"));
            return Task.CompletedTask;
        }
    }

    private static SubAgentRequest Request(int reserve = 0, int maxRounds = 10, string brief = "BRIEF") =>
        new("wiki", "Wiki research: q", "SYS", brief, [new WikiPageTool(() => [GScreen])], new TurnLimits { MaxRounds = maxRounds }, reserve, "FULL");

    [Fact]
    public async Task SubAgent_RunsTheBriefOnItsOwnSession_AndReturnsItsLastText()
    {
        var provider = new FakeProvider(("Looking.", "wiki_page"), ("Facts: x", null));

        var result = await SubAgent.RunAsync(provider, Request(), null, CancellationToken.None);

        Assert.Equal(new SubAgentResult("Facts: x", false), result);
        Assert.Equal(["wiki"], provider.Sessions);
        Assert.Equal(["SYS", "BRIEF"], provider.Requests[0].Messages.Select(m => m.Content));
        Assert.StartsWith("# The G-Screen", provider.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public async Task SubAgent_RefusesToolsOnceTheContextReachesTheReserve()
    {
        var provider = new FakeProvider((null, "wiki_page"), ("Facts: y", null)) { ContextTokens = 900 };

        await SubAgent.RunAsync(provider, Request(reserve: 100), null, CancellationToken.None);

        Assert.Equal("FULL", provider.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public async Task SubAgent_EstimatesTheContextFromTextWhenTheServerCountsOnlyNewTokens()
    {
        var provider = new FakeProvider((null, "wiki_page"), ("Facts: y", null)) { ContextTokens = 10 };

        await SubAgent.RunAsync(provider, Request(reserve: 100, brief: new string('b', 900 * SubAgent.CharsPerToken)), null, CancellationToken.None);

        Assert.Equal("FULL", provider.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public async Task SubAgent_AppendsTheCallReminderOnceToItsCall()
    {
        var provider = new FakeProvider((null, "wiki_page"), (null, "wiki_page"), (null, "wiki_page"), ("Facts: z", null));

        await SubAgent.RunAsync(provider, Request() with { CallReminder = new CallReminder(2, "REMIND") }, null, CancellationToken.None);

        Assert.DoesNotContain("REMIND", provider.Requests[1].Messages[^1].Content);
        Assert.EndsWith("\n\nREMIND", provider.Requests[2].Messages[^1].Content);
        Assert.DoesNotContain("REMIND", provider.Requests[3].Messages[^1].Content);
    }

    [Fact]
    public async Task SubAgent_ReportsTheRoundCap()
    {
        var provider = new FakeProvider(("Partial.", "wiki_page"));

        var result = await SubAgent.RunAsync(provider, Request(maxRounds: 1), null, CancellationToken.None);

        Assert.Equal(new SubAgentResult("Partial.", true), result);
    }

    [Fact]
    public async Task SubAgent_StoppedThrows()
    {
        using var stop = new CancellationTokenSource();
        stop.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SubAgent.RunAsync(new FakeProvider(("x", null)), Request(), null, stop.Token));
    }

    private static WikiTool Tool(SkillPlan? plan = null) => new(() => [GScreen], () => [], [], plan ?? new SkillPlan());

    [Fact]
    public async Task WikiTool_WithoutARunnerIsUnavailable()
    {
        var result = await Tool().ExecuteAsync(Args("""{"question":"q"}"""), CancellationToken.None);

        Assert.Equal("The wiki is not available with this model provider.", result.Output);
    }

    [Fact]
    public async Task WikiTool_BriefsTheResearcherWithTheChatAndThePlanItem()
    {
        var plan = new SkillPlan();
        plan.Activate(new Skill("create-block", "d", "Plan:\n- Fill name", ["Fill name"]));
        SubAgentRequest? sent = null;
        var tool = Tool(plan);
        tool.Attach((request, _) =>
        {
            sent = request;
            return Task.FromResult(new SubAgentResult("Facts: z", false));
        }, () => [new ChatMessage(ChatRole.User, "Make a block")]);

        var result = await tool.ExecuteAsync(Args("""{"question":"Where does the name go?"}"""), CancellationToken.None);

        Assert.Equal("Facts: z", result.Output);
        Assert.Equal("wiki", sent!.Name);
        Assert.Contains("<transcript>\n[user]\nMake a block\n</transcript>", sent.Brief);
        Assert.Contains("Veronica is on item 1 of 1 of create-block: Fill name.", sent.Brief);
        Assert.Contains("Veronica's question: Where does the name go?", sent.Brief);
        Assert.Contains("- gscreen: Use for G-Screen places.", sent.SystemPrompt);
        Assert.Equal(WikiTool.AnswerReserve, sent.ContextReserve);
        Assert.Equal(["wiki_page", "skill"], sent.Tools.Select(t => t.Name));
    }

    [Theory]
    [InlineData(null, false, "The research ended without an answer. Continue without it.")]
    [InlineData("Facts: p", true, "Facts: p\n\n(The research ran out of rounds; this may be incomplete.)")]
    public async Task WikiTool_SaysWhenTheAnswerIsMissingOrPartial(string? text, bool roundCap, string output)
    {
        var tool = Tool();
        tool.Attach((_, _) => Task.FromResult(new SubAgentResult(text, roundCap)), () => []);

        Assert.Equal(output, (await tool.ExecuteAsync(Args("""{"question":"q"}"""), CancellationToken.None)).Output);
        Assert.Equal("wiki needs a question.", (await tool.ExecuteAsync(Args("{}"), CancellationToken.None)).Output);
    }
}
