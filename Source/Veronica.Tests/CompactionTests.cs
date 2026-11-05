using Veronica.Agent;
using Xunit;

namespace Veronica.Tests;

public sealed class CompactionTests
{
    private static readonly Skill Build = new("make-block", "Makes a block.", "Steps: do it.", ["a", "b", "c", "d", "e", "f", "g"]);
    private static readonly Skill Nested = new("nested", "d", "NESTED BODY", ["x", "y"]);

    private sealed class Host : ITurnHost
    {
        public List<string> Notices { get; } = [];
        public void OnRoundStart() { }
        public void OnText(string delta) { }
        public void OnReasoning(string delta) { }
        public void OnRoundStats(RoundStats stats) { }
        public Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken) =>
            Task.FromResult<ToolResult?>(new ToolResult("out"));
        public void OnNotice(string text) => Notices.Add(text);
        public void AppendTranscript(ChatMessage message) { }
    }

    // Each turn the session runs takes the next scripted step: it appends to the transcript, then may throw.
    private sealed class Session(params Func<TurnRequest, ITurnHost, Task>[] turns) : IProviderSession
    {
        private readonly Queue<Func<TurnRequest, ITurnHost, Task>> _turns = new(turns);

        public List<TurnRequest> Requests { get; } = [];

        public Task RunTurnAsync(TurnRequest request, ITurnHost host, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return _turns.Dequeue()(request, host);
        }

        public Task ResetAsync() => Task.CompletedTask;

        public void Dispose() { }
    }

    private static Func<TurnRequest, ITurnHost, Task> Reply(string text, Action? also = null) => (_, host) =>
    {
        also?.Invoke();
        host.AppendTranscript(new ChatMessage(ChatRole.Assistant, text));
        return Task.CompletedTask;
    };

    private static Func<TurnRequest, ITurnHost, Task> Wall(ContextFullPhase phase = ContextFullPhase.Prompt, Action<ITurnHost>? before = null) => (_, host) =>
    {
        before?.Invoke(host);
        throw new ContextFullException(phase, "full");
    };

    private static void Research(ITurnHost host, string answer)
    {
        var call = new ToolCall("w1", WikiTool.ToolName, "{\"question\":\"q\"}");
        host.AppendTranscript(new ChatMessage(ChatRole.Assistant, "") { ToolCalls = [call] });
        host.AppendTranscript(new ChatMessage(ChatRole.Tool, answer) { ToolCallId = "w1", Name = WikiTool.ToolName });
    }

    private sealed class Summarizer(params Func<SubAgentRequest, SubAgentResult>[] runs)
    {
        private readonly Queue<Func<SubAgentRequest, SubAgentResult>> _runs = new(runs);

        public List<SubAgentRequest> Requests { get; } = [];

        public Task<SubAgentResult> RunAsync(SubAgentRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_runs.Dequeue()(request));
        }
    }

    private static Func<SubAgentRequest, SubAgentResult> Summary(string text) => _ => new SubAgentResult(text, false);

    private static (Conversation Chat, SkillPlan Plan) Chat(Session session, Summarizer? summarizer)
    {
        var plan = new SkillPlan();
        var compaction = new Compaction(plan, () => [Build, Nested]);
        if (summarizer != null)
            compaction.Attach(summarizer.RunAsync);
        return (new Conversation(session, () => "SYS", new TurnLimits(), [], compaction), plan);
    }

    [Fact]
    public async Task A_full_context_is_summarized_and_the_turn_goes_on_from_the_summary()
    {
        var session = new Session(Wall(before: host => Research(host, "Facts: thrusters push.")), Reply("done"));
        var summarizer = new Summarizer(Summary(" SUMMARY "));
        var (chat, plan) = Chat(session, summarizer);
        plan.Activate(Build);
        var host = new Host();

        await chat.SendAsync("Make a thruster", host, CancellationToken.None);

        var brief = Assert.Single(summarizer.Requests);
        Assert.Equal(Compaction.SessionName, brief.Name);
        Assert.Equal(Prompts.CompactionTitle, brief.Title);
        Assert.Empty(brief.Tools);
        Assert.Equal((1, 4000, false), (brief.Limits.MaxRounds, brief.Limits.MaxTokens, brief.Limits.Thinking));
        Assert.Contains("[user]\nMake a thruster", brief.Brief);
        Assert.Contains("[result of wiki]\nFacts: thrusters push.", brief.Brief);

        var resumed = session.Requests[1];
        Assert.True(resumed.Compacted);
        var compacted = Assert.Single(resumed.Transcript);
        Assert.Equal(ChatRole.User, compacted.Role);
        Assert.Equal(Prompts.Compacted("SUMMARY", "Facts: thrusters push.", Build, plan.CurrentItem()), compacted.Content);
        Assert.Contains("SUMMARY", compacted.Content);
        Assert.Contains("Steps: do it.", compacted.Content);
        Assert.Contains("item 1 of 7 of make-block: a", compacted.Content);
        Assert.Equal([compacted.Content, "done"], chat.Transcript.Select(m => m.Content));
        Assert.Equal([Prompts.CompactedNotice], host.Notices);
    }

    [Fact]
    public async Task The_brief_lists_every_open_plan_and_only_the_active_skill_stays_in_context()
    {
        var session = new Session(Wall(), Reply("done"));
        var summarizer = new Summarizer(Summary("SUMMARY"));
        var (chat, plan) = Chat(session, summarizer);
        plan.Activate(Build);
        plan.Activate(Nested);

        await chat.SendAsync("go", new Host(), CancellationToken.None);

        Assert.Contains("<plans>\n" + plan.OpenPlans() + "\n</plans>", summarizer.Requests[0].Brief);
        Assert.Contains("make-block (paused until the plans below it are done):\n  1. [current] a\n  2. b", plan.OpenPlans());
        Assert.Contains("NESTED BODY", session.Requests[1].Transcript[0].Content);
        plan.Complete("p");
        Assert.EndsWith("\n\nThe skill:\nSteps: do it.", plan.Complete("p"));
        Assert.DoesNotContain("NESTED BODY", plan.Activate(Nested));
    }

    [Fact]
    public async Task A_second_wall_in_the_same_turn_ends_it_with_the_compact_choice()
    {
        var session = new Session(Wall(), Wall(ContextFullPhase.Reply));
        var summarizer = new Summarizer(Summary("SUMMARY"));
        var (chat, _) = Chat(session, summarizer);

        var e = await Assert.ThrowsAsync<ContextFullTurnException>(() => chat.SendAsync("go", new Host(), CancellationToken.None));

        Assert.True(e.CanCompact);
        Assert.Equal(Prompts.ContextFullAgain, e.Message);
        Assert.Single(summarizer.Requests);
        Assert.Single(chat.Transcript);
    }

    [Fact]
    public async Task Five_closed_plan_items_give_the_allowance_back()
    {
        SkillPlan? planRef = null;
        void Close(int count)
        {
            for (int i = 0; i < count; i++)
                planRef!.Complete("did it");
        }
        var session = new Session(
            Wall(),
            Wall(before: _ => Close(Compaction.ItemsPerAllowance)),
            Wall(before: _ => Close(Compaction.ItemsPerAllowance - 1)));
        var summarizer = new Summarizer(Summary("ONE"), Summary("TWO"));
        var (chat, plan) = Chat(session, summarizer);
        planRef = plan;
        plan.Activate(Build);
        plan.Activate(new Skill("nested", "d", "b", ["x", "y", "z", "w"]));

        var e = await Assert.ThrowsAsync<ContextFullTurnException>(() => chat.SendAsync("go", new Host(), CancellationToken.None));

        Assert.True(e.CanCompact);
        Assert.Equal(2, summarizer.Requests.Count);
        Assert.Equal(3, session.Requests.Count);
    }

    [Fact]
    public async Task The_users_next_message_gives_the_allowance_back()
    {
        var session = new Session(Wall(), Reply("first"), Wall(), Reply("second"));
        var summarizer = new Summarizer(Summary("ONE"), Summary("TWO"));
        var (chat, _) = Chat(session, summarizer);

        await chat.SendAsync("go", new Host(), CancellationToken.None);
        await chat.SendAsync("answers", new Host(), CancellationToken.None);

        Assert.Equal(2, summarizer.Requests.Count);
        Assert.Contains("[user]\nanswers", summarizer.Requests[1].Brief);
        Assert.Equal("second", chat.Transcript[^1].Content);
    }

    [Theory]
    [InlineData("throws")]
    [InlineData("empty")]
    [InlineData("full")]
    public async Task A_failed_summary_leaves_the_conversation_as_it_was(string failure)
    {
        var session = new Session(Wall(before: host => host.AppendTranscript(new ChatMessage(ChatRole.Assistant, "partial"))));
        var summarizer = new Summarizer(_ => failure switch
        {
            "throws" => throw new InvalidOperationException("server gone"),
            "full" => throw new ContextFullException(ContextFullPhase.Prompt, "full"),
            _ => new SubAgentResult(null, false),
        });
        var (chat, _) = Chat(session, summarizer);

        var e = await Assert.ThrowsAsync<ContextFullTurnException>(() => chat.SendAsync("go", new Host(), CancellationToken.None));

        Assert.False(e.CanCompact);
        Assert.Equal(["go", "partial"], chat.Transcript.Select(m => m.Content));
        Assert.Contains(failure switch { "throws" => "server gone", "full" => "too long even to summarize", _ => "came back empty" }, e.Message);
    }

    [Fact]
    public async Task Compact_choice_summarizes_and_goes_on()
    {
        var session = new Session(Wall(), Wall(), Reply("done"));
        var summarizer = new Summarizer(Summary("ONE"), Summary("TWO"));
        var (chat, _) = Chat(session, summarizer);
        await Assert.ThrowsAsync<ContextFullTurnException>(() => chat.SendAsync("go", new Host(), CancellationToken.None));

        await chat.CompactAsync(new Host(), CancellationToken.None);

        Assert.Equal(2, summarizer.Requests.Count);
        Assert.True(session.Requests[2].Compacted);
        Assert.Equal("done", chat.Transcript[^1].Content);
    }

    [Fact]
    public async Task Stopping_the_summary_leaves_the_conversation_as_it_was()
    {
        using var stop = new CancellationTokenSource();
        var session = new Session(Wall());
        var (chat, _) = Chat(session, new Summarizer(_ =>
        {
            stop.Cancel();
            throw new OperationCanceledException(stop.Token);
        }));

        await Assert.ThrowsAsync<OperationCanceledException>(() => chat.SendAsync("go", new Host(), stop.Token));

        Assert.Equal(["go"], chat.Transcript.Select(m => m.Content));
        Assert.Single(session.Requests);
    }

    [Fact]
    public async Task Without_a_runner_the_wall_fails_the_turn_as_it_is()
    {
        var (chat, _) = Chat(new Session(Wall()), null);

        await Assert.ThrowsAsync<ContextFullException>(() => chat.SendAsync("go", new Host(), CancellationToken.None));
    }

    [Fact]
    public void Last_wiki_answer_skips_research_that_gave_none()
    {
        ChatMessage Wiki(string content) => new(ChatRole.Tool, content) { Name = WikiTool.ToolName };
        var transcript = new List<ChatMessage>
        {
            Wiki("Facts: old."),
            Wiki("Facts: new."),
            new(ChatRole.Tool, "read out") { Name = "read" },
            Wiki(WikiTool.NoAnswerOutput),
            Wiki("wiki failed: 400 Bad Request"),
            Wiki(ClientToolLoop.CancelledToolOutput),
        };

        Assert.Equal("Facts: new.", Compaction.LastWikiAnswer(transcript));
        Assert.Null(Compaction.LastWikiAnswer([Wiki(WikiTool.UnavailableOutput)]));
    }
}
