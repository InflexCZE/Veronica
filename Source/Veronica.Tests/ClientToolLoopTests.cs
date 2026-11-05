using Veronica.Agent;
using Xunit;

namespace Veronica.Tests;

public sealed class ClientToolLoopTests
{
    private const string Nudge = "Pick one and go.";
    private const string Poke = "Step 2 is still open.";

    private sealed record Round(string FinishReason, string? Reasoning = null, string? Text = null, string? Call = null, bool Reward = false, string? Call2 = null, bool ContextFull = false);

    private sealed class Run : ITurnHost
    {
        private readonly Queue<Round> _rounds;

        public Run(IEnumerable<Round> rounds) => _rounds = new Queue<Round>(rounds);

        public List<IReadOnlyList<ChatMessage>> Sent { get; } = [];
        public List<RoundRequest> Requests { get; } = [];
        public List<ChatMessage> Transcript { get; } = [];
        public List<string> Notices { get; } = [];
        public List<string> ToolsRun { get; } = [];
        public string Text { get; private set; } = "";
        public int Remaining => _rounds.Count;
        public Func<ToolCall, CancellationTokenSource?, ToolResult?>? Tool { get; init; }
        public Action<CancellationTokenSource>? DuringRound { get; init; }
        public CancellationTokenSource Cancel { get; } = new();

        public Task Go(TurnLimits? limits = null) =>
            ClientToolLoop.RunAsync(Stream, new TurnRequest("SYS", [new ChatMessage(ChatRole.User, "Review.")], [], limits ?? new TurnLimits()), this, Cancel.Token);

        private Task Stream(RoundRequest request, Action<StreamDelta> onDelta, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Sent.Add(request.Messages);
            if (!_rounds.TryDequeue(out var round))
                throw new InvalidOperationException("no round scripted");
            if (round.Reasoning != null) onDelta(new ReasoningDelta(round.Reasoning));
            DuringRound?.Invoke(Cancel);
            if (round.Text != null) onDelta(new TextDelta(round.Text));
            if (round.Call != null) onDelta(new ToolCallDelta(0, "c1", round.Call, round.Reward ? "{\"reward\":true}" : "{}"));
            if (round.Call2 != null) onDelta(new ToolCallDelta(1, "c2", round.Call2, "{}"));
            onDelta(new DoneDelta(round.FinishReason, round.ContextFull));
            return Task.CompletedTask;
        }

        public void OnRoundStart() { }
        public void OnText(string delta) => Text += delta;
        public void OnReasoning(string delta) { }
        public void OnRoundStats(RoundStats stats) { }
        public void OnNotice(string text) => Notices.Add(text);
        public void AppendTranscript(ChatMessage message) => Transcript.Add(message);

        public Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken)
        {
            ToolsRun.Add(call.Name);
            var result = Tool != null ? Tool(call, Cancel) : new ToolResult("RESULT", call.Arguments.Contains("reward"));
            return Task.FromResult(result);
        }

        public ChatRole[] Roles => Transcript.Select(m => m.Role).ToArray();
    }

    private static readonly TurnLimits Nudging = new() { LengthNudge = new LengthNudge(() => Nudge, MaxNudges: 2) };

    private static TurnLimits Poking(int maxRounds = TurnLimits.DefaultMaxRounds) => new()
    {
        MaxRounds = maxRounds,
        EndNudge = new EndNudge(reply => reply.Contains("STOP") ? null : Poke, new HashSet<string> { "set_plan" }),
    };

    private const ChatRole A = ChatRole.Assistant, U = ChatRole.User, T = ChatRole.Tool;

    [Fact]
    public async Task Reply_that_ran_into_the_context_window_fails_the_turn_without_a_nudge_and_appends_nothing()
    {
        var run = new Run([new Round("tool_calls", Call: "lookup"), new Round("length", Reasoning: "hm", Call: "lookup", ContextFull: true), new Round("stop", Text: "never")]);

        var e = await Assert.ThrowsAsync<ContextFullException>(() => run.Go(Nudging));

        Assert.Equal(ContextFullPhase.Reply, e.Phase);
        Assert.Equal([A, T], run.Roles);
        Assert.Equal(["lookup"], run.ToolsRun);
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task Turn_ending_tool_runs_the_rounds_calls_then_ends_the_turn_without_a_reminder()
    {
        var run = new Run([new Round("tool_calls", Text: "What name?", Call: "lookup", Call2: "wait"), new Round("stop", Text: "never")]);

        await run.Go(Poking() with { TurnEndingTools = new HashSet<string> { "wait" } });

        Assert.Equal(["lookup", "wait"], run.ToolsRun);
        Assert.Equal([A, T, T], run.Roles);
        Assert.Equal(1, run.Remaining);
        Assert.Empty(run.Notices);
    }

    [Fact]
    public async Task Turn_ending_tool_without_text_in_its_round_continues_and_asks_for_the_question()
    {
        var run = new Run([
            new Round("tool_calls", Call: "wait"),
            new Round("tool_calls", Text: "What name?", Call: "wait"),
            new Round("stop", Text: "never")]);

        await run.Go(new TurnLimits { TurnEndingTools = new HashSet<string> { "wait" } });

        Assert.Equal([A, T, A, T], run.Roles);
        Assert.Equal(ClientToolLoop.SilentWaitOutput("wait"), run.Transcript[1].Content);
        Assert.Equal("RESULT", run.Transcript[3].Content);
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task Turn_ending_tool_needs_text_in_its_own_round_not_an_earlier_one()
    {
        var run = new Run([
            new Round("tool_calls", Text: "Let me look first.", Call: "lookup"),
            new Round("tool_calls", Call: "wait"),
            new Round("tool_calls", Text: "Which folder?", Call: "wait"),
            new Round("stop", Text: "never")]);

        await run.Go(new TurnLimits { TurnEndingTools = new HashSet<string> { "wait" } });

        Assert.Equal([A, T, A, T, A, T], run.Roles);
        Assert.Equal(ClientToolLoop.SilentWaitOutput("wait"), run.Transcript[3].Content);
        Assert.Equal("RESULT", run.Transcript[5].Content);
        Assert.Equal(1, run.Remaining);
    }

    private static readonly TurnLimits Asking = Poking() with
    {
        TurnEndingTools = new HashSet<string> { "wait", "ask" },
        UserFacingTools = new HashSet<string> { "ask" },
    };

    [Fact]
    public async Task User_facing_tool_ends_the_turn_without_text()
    {
        var run = new Run([new Round("tool_calls", Call: "ask"), new Round("stop", Text: "never")]);

        await run.Go(Asking);

        Assert.Equal([A, T], run.Roles);
        Assert.Equal("RESULT", run.Transcript[1].Content);
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task User_facing_call_after_a_wait_lets_the_textless_wait_end_the_turn()
    {
        var run = new Run([new Round("tool_calls", Call: "wait", Call2: "ask"), new Round("stop", Text: "never")]);

        await run.Go(Asking);

        Assert.Equal([A, T, T], run.Roles);
        Assert.All(run.Transcript.Skip(1), m => Assert.Equal("RESULT", m.Content));
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task Refused_user_facing_call_neither_ends_the_turn_nor_speaks_for_a_wait()
    {
        var run = new Run([new Round("tool_calls", Call: "ask", Call2: "wait"), new Round("stop", Text: "STOP")])
        {
            Tool = (call, _) => new ToolResult(call.Name == "ask" ? "BAD" : "RESULT", Refused: call.Name == "ask"),
        };

        await run.Go(Asking);

        Assert.Equal([A, T, T, A], run.Roles);
        Assert.Equal("BAD", run.Transcript[1].Content);
        Assert.Equal(ClientToolLoop.SilentWaitOutput("wait"), run.Transcript[2].Content);
    }

    [Fact]
    public async Task User_facing_call_the_tool_set_could_not_run_does_not_end_the_turn()
    {
        var tools = new Veronica.Agent.Tools.ToolSet([new AskUserTool(new QuestionBoard(), waitsForAnswers: false)]);
        var run = new Run([new Round("tool_calls", Call: "ask_user"), new Round("stop", Text: "STOP")])
        {
            Tool = (call, _) => tools.ExecuteAsync(call with { Arguments = "{not json" }, CancellationToken.None).Result,
        };

        await run.Go(Asking with { TurnEndingTools = new HashSet<string> { "ask_user" }, UserFacingTools = new HashSet<string> { "ask_user" } });

        Assert.Equal([A, T, A], run.Roles);
        Assert.Contains("not valid JSON", run.Transcript[1].Content);
    }

    [Fact]
    public async Task Turn_ending_call_on_the_last_round_ends_without_the_budget_notice()
    {
        var run = new Run([new Round("tool_calls", Call: "read"), new Round("tool_calls", Call: "ask"), new Round("stop", Text: "never")]);

        await run.Go(Asking with { MaxRounds = 2 });

        Assert.Empty(run.Notices);
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task Stop_during_a_later_call_keeps_the_earlier_results()
    {
        var run = new Run([new Round("tool_calls", Call: "read", Call2: "slow")])
        {
            Tool = (call, _) => call.Name == "slow" ? null : new ToolResult("RESULT"),
        };

        await run.Go();

        Assert.Equal([A, T, T], run.Roles);
        Assert.Equal("RESULT", run.Transcript[1].Content);
        Assert.Equal(ClientToolLoop.CancelledToolOutput, run.Transcript[2].Content);
    }

    [Fact]
    public async Task Plain_reply_ends_the_turn_with_reasoning_kept()
    {
        var run = new Run([new("stop", Reasoning: "hmm", Text: "Hello.")]);
        await run.Go();

        var reply = Assert.Single(run.Transcript);
        Assert.Equal(new ChatMessage(A, "Hello.") { Reasoning = "hmm" }, reply);
        Assert.Equal(ChatRole.System, run.Sent[0][0].Role);
        Assert.Equal("SYS", run.Sent[0][0].Content);
    }

    [Fact]
    public async Task Tool_results_feed_the_next_round()
    {
        var run = new Run([new("tool_calls", Call: "read"), new("stop", Text: "Done.")]);
        await run.Go();

        Assert.Equal([A, T, A], run.Roles);
        Assert.Equal("c1", run.Transcript[1].ToolCallId);
        Assert.Equal("RESULT", run.Transcript[1].Content);
        Assert.Equal([U, A, T], run.Sent[1].Skip(1).Select(m => m.Role));
    }

    [Fact]
    public async Task Tool_call_fragments_merge_by_index()
    {
        var run = new Run([]);
        StreamRound stream = (request, onDelta, ct) =>
        {
            if (run.Sent.Count == 1)
            {
                onDelta(new DoneDelta("stop"));
                return Task.CompletedTask;
            }
            run.Sent.Add(request.Messages);
            onDelta(new ToolCallDelta(1, "b", "se", "{\"x\""));
            onDelta(new ToolCallDelta(0, "a", "first", "{}"));
            onDelta(new ToolCallDelta(1, null, "cond", ":1}"));
            onDelta(new DoneDelta("tool_calls"));
            return Task.CompletedTask;
        };
        await ClientToolLoop.RunAsync(stream, new TurnRequest("SYS", [], [], new TurnLimits()), run, CancellationToken.None);

        Assert.Equal([new ToolCall("a", "first", "{}"), new ToolCall("b", "second", "{\"x\":1}")], run.Transcript[0].ToolCalls);
        Assert.Equal(["first", "second"], run.ToolsRun);
    }

    [Fact]
    public async Task Length_nudge_keeps_the_cut_thinking_and_continues()
    {
        var run = new Run([new("length", Reasoning: "A or B"), new("tool_calls", Reasoning: "A", Call: "read"), new("stop", Text: "VERDICT")]);
        await run.Go(Nudging);

        Assert.Equal([A, U, A, T, A], run.Roles);
        Assert.Equal(new ChatMessage(A, "") { Reasoning = "A or B" }, run.Transcript[0]);
        Assert.Equal(new ChatMessage(U, Nudge), run.Transcript[1]);
        Assert.Equal(new ChatMessage(U, Nudge), run.Sent[1][^1]);
        Assert.Equal("VERDICT", run.Text);
    }

    [Fact]
    public async Task Length_nudge_gives_up_after_max_nudges()
    {
        var run = new Run([new("length", Reasoning: "1"), new("length", Reasoning: "2"), new("length", Reasoning: "3"), new("stop", Text: "never")]);
        await run.Go(Nudging);

        Assert.Equal(2, run.Transcript.Count(m => m.Role == U));
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task Length_nudge_without_a_count_spends_the_round_budget()
    {
        var run = new Run([.. Enumerable.Repeat(new Round("length", Reasoning: "w"), 4), new("stop", Text: "never")]);
        await run.Go(new TurnLimits { MaxRounds = 3, LengthNudge = new LengthNudge(() => Nudge) });

        Assert.Equal(2, run.Transcript.Count(m => m.Role == U));
        Assert.Equal([ClientToolLoop.RoundCapNotice(3)], run.Notices);
        Assert.Equal(2, run.Remaining);
    }

    [Fact]
    public async Task Nudged_rounds_and_tool_rounds_share_one_budget()
    {
        var run = new Run([new("length", Reasoning: "1"), new("tool_calls", Call: "read"), new("length", Reasoning: "2"), new("tool_calls", Call: "read"), new("stop", Text: "never")]);
        await run.Go(new TurnLimits { MaxRounds = 4, LengthNudge = new LengthNudge(() => Nudge) });

        Assert.Equal(4, run.Sent.Count);
        Assert.Equal([ClientToolLoop.RoundCapNotice(4)], run.Notices);
    }

    [Fact]
    public async Task Hard_cap_on_a_nudged_round_ends_with_the_notice()
    {
        var run = new Run([.. Enumerable.Repeat(new Round("tool_calls", Call: "lookup"), 5), new("length", Reasoning: "w")]);
        await run.Go(new TurnLimits { MaxRounds = 2, LengthNudge = new LengthNudge(() => Nudge), FreeRoundTools = new HashSet<string> { "lookup" } });

        Assert.Equal(6, run.Sent.Count);
        Assert.Equal([ClientToolLoop.RoundCapNotice(2)], run.Notices);
        Assert.Equal(A, run.Transcript[^1].Role);
    }

    [Fact]
    public async Task No_nudge_unless_asked()
    {
        var run = new Run([new("length", Reasoning: "w"), new("stop", Text: "never")]);
        await run.Go();

        Assert.Equal([A], run.Roles);
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task No_nudge_for_a_round_cut_mid_answer()
    {
        var run = new Run([new("length", Reasoning: "done", Text: "Finding 1 (line"), new("stop", Text: "never")]);
        await run.Go(Nudging);

        Assert.Equal([A], run.Roles);
    }

    [Fact]
    public async Task Call_cut_by_the_length_limit_keeps_empty_arguments_is_not_run_and_the_turn_continues()
    {
        var run = new Run([]);
        StreamRound stream = (r, d, ct) =>
        {
            run.Sent.Add(r.Messages);
            if (run.Sent.Count == 1)
            {
                d(new ToolCallDelta(0, "a", "write", "{\"x\":1}"));
                d(new ToolCallDelta(1, "b", "write", "{\"x\":"));
                d(new DoneDelta("length"));
            }
            else
                d(new DoneDelta("stop"));
            return Task.CompletedTask;
        };
        await ClientToolLoop.RunAsync(stream, new TurnRequest("SYS", [], [], new TurnLimits()), run, CancellationToken.None);

        Assert.Equal([A, T, T, A], run.Roles);
        Assert.Equal([new ToolCall("a", "write", "{\"x\":1}"), new ToolCall("b", "write", "{}")], run.Transcript[0].ToolCalls);
        Assert.Equal(["write"], run.ToolsRun);
        Assert.Equal("RESULT", run.Transcript[1].Content);
        Assert.Equal(ClientToolLoop.CutCallOutput, run.Transcript[2].Content);
        Assert.Equal("b", run.Transcript[2].ToolCallId);
        Assert.Equal(2, run.Sent.Count);
    }

    [Fact]
    public async Task Cut_turn_ending_call_does_not_end_the_turn()
    {
        var run = new Run([]);
        StreamRound stream = (r, d, ct) =>
        {
            run.Sent.Add(r.Messages);
            if (run.Sent.Count == 1)
            {
                d(new TextDelta("Which name?"));
                d(new ToolCallDelta(0, "a", "wait", "{\"q"));
                d(new DoneDelta("length"));
            }
            else
                d(new DoneDelta("stop"));
            return Task.CompletedTask;
        };
        await ClientToolLoop.RunAsync(stream, new TurnRequest("SYS", [], [], new TurnLimits { TurnEndingTools = new HashSet<string> { "wait" } }), run, CancellationToken.None);

        Assert.Empty(run.ToolsRun);
        Assert.Equal(ClientToolLoop.CutCallOutput, run.Transcript[1].Content);
        Assert.Equal(2, run.Sent.Count);
    }

    [Fact]
    public async Task Complete_calls_of_a_length_cut_round_run_unchanged()
    {
        var run = new Run([new("length", Call: "read"), new("stop", Text: "ok")]);
        await run.Go();

        Assert.Equal(["read"], run.ToolsRun);
        Assert.Equal("RESULT", run.Transcript[1].Content);
    }

    [Fact]
    public async Task End_nudge_sends_the_model_back()
    {
        var run = new Run([new("stop", Text: "All done."), new("tool_calls", Call: "read"), new("stop", Text: "Now done."), new("stop", Text: "Really.")]);
        await run.Go(Poking());

        Assert.Equal([A, U, A, T, A, U, A], run.Roles);
        Assert.Equal(new ChatMessage(U, Poke), run.Transcript[1]);
        Assert.Equal(0, run.Remaining);
    }

    [Fact]
    public async Task End_nudge_lets_a_model_end_twice_in_a_row()
    {
        var run = new Run([new("stop", Text: "Q?"), new("stop", Text: "Q?"), new("stop", Text: "never")]);
        await run.Go(Poking());

        Assert.Equal([A, U, A], run.Roles);
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task End_nudge_respects_a_null_message()
    {
        var run = new Run([new("stop", Text: "Waiting. STOP"), new("stop", Text: "never")]);
        await run.Go(Poking());

        Assert.Equal([A], run.Roles);
    }

    [Fact]
    public async Task End_nudge_stays_disarmed_across_bookkeeping_rounds()
    {
        var run = new Run([new("stop", Text: "Done."), new("tool_calls", Call: "set_plan"), new("stop", Text: "Done."), new("stop", Text: "never")]);
        await run.Go(Poking());

        Assert.Equal([A, U, A, T, A], run.Roles);
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task End_nudge_leaves_a_cut_thinking_round_to_the_length_nudge()
    {
        var run = new Run([new("length", Reasoning: "w"), new("stop", Text: "Done."), new("stop", Text: "Done.")]);
        await run.Go(Nudging with { EndNudge = Poking().EndNudge });

        Assert.Equal([Nudge, Poke], run.Transcript.Where(m => m.Role == U).Select(m => m.Content));
    }

    [Fact]
    public async Task End_nudge_spends_a_round()
    {
        var run = new Run([new("tool_calls", Call: "read"), new("stop", Text: "Done."), new("stop", Text: "never")]);
        await run.Go(Poking(maxRounds: 2));

        Assert.Equal([ClientToolLoop.RoundCapNotice(2)], run.Notices);
        Assert.Equal(A, run.Transcript[^1].Role);
    }

    [Fact]
    public async Task Round_cap_stops_with_a_notice_and_tells_the_last_tool()
    {
        var lastRounds = new List<bool>();
        var run = new Run(Enumerable.Repeat(new Round("tool_calls", Call: "read"), 5))
        {
            Tool = (call, _) => new ToolResult("ok"),
        };
        var host = new ContextSpy(run, lastRounds);
        await ClientToolLoop.RunAsync(
            (r, d, ct) => { run.Sent.Add(r.Messages); d(new ToolCallDelta(0, "c", "read", "{}")); d(new DoneDelta("tool_calls")); return Task.CompletedTask; },
            new TurnRequest("SYS", [], [], new TurnLimits { MaxRounds = 3 }), host, CancellationToken.None);

        Assert.Equal(3, run.Sent.Count);
        Assert.Equal([false, false, true], lastRounds);
        Assert.Equal([ClientToolLoop.RoundCapNotice(3)], run.Notices);
    }

    [Fact]
    public async Task Free_rounds_do_not_spend_the_budget()
    {
        var run = new Run([new("tool_calls", Call: "lookup"), new("tool_calls", Call: "lookup"), new("tool_calls", Call: "read"), new("stop", Text: "ok")]);
        await run.Go(new TurnLimits { MaxRounds = 2, FreeRoundTools = new HashSet<string> { "lookup" } });

        Assert.Empty(run.Notices);
        Assert.Equal(0, run.Remaining);
    }

    [Fact]
    public async Task Reward_refunds_half_the_budget()
    {
        var run = new Run([new("tool_calls", Call: "edit"), new("tool_calls", Call: "build", Reward: true), new("tool_calls", Call: "edit"), new("stop", Text: "ok")]);
        await run.Go(new TurnLimits { MaxRounds = 3 });

        Assert.Empty(run.Notices);
        Assert.Equal(0, run.Remaining);
    }

    [Fact]
    public async Task Cancel_during_a_tool_backfills_the_remaining_calls()
    {
        var run = new Run([]) { Tool = (_, _) => null };
        StreamRound stream = (r, d, ct) =>
        {
            d(new ToolCallDelta(0, "c1", "a", "{}"));
            d(new ToolCallDelta(1, "c2", "b", "{}"));
            d(new DoneDelta("tool_calls"));
            return Task.CompletedTask;
        };
        await ClientToolLoop.RunAsync(stream, new TurnRequest("SYS", [], [], new TurnLimits()), run, CancellationToken.None);

        Assert.Equal([A, T, T], run.Roles);
        Assert.All(run.Transcript.Skip(1), m => Assert.Equal(ClientToolLoop.CancelledToolOutput, m.Content));
        Assert.Equal(["c1", "c2"], run.Transcript.Skip(1).Select(m => m.ToolCallId));
    }

    [Fact]
    public async Task Stopped_round_keeps_its_partial_reply_and_runs_no_tools()
    {
        var run = new Run([new("tool_calls", Reasoning: "half", Call: "read")]) { DuringRound = c => c.Cancel() };
        await run.Go();

        Assert.Equal([A, T], run.Roles);
        Assert.Equal("half", run.Transcript[0].Reasoning);
        Assert.Equal(ClientToolLoop.CancelledToolOutput, run.Transcript[1].Content);
        Assert.Empty(run.ToolsRun);
    }

    [Fact]
    public async Task Stopped_round_is_not_nudged()
    {
        var run = new Run([new("length", Reasoning: "w"), new("stop", Text: "never")]) { DuringRound = c => c.Cancel() };
        await run.Go(Nudging);

        Assert.Equal([A], run.Roles);
    }

    [Fact]
    public async Task Cancelled_before_the_round_produced_anything_leaves_no_trace()
    {
        var run = new Run([]);
        StreamRound stream = (r, d, ct) => Task.FromCanceled(new CancellationToken(true));
        run.Cancel.Cancel();
        await ClientToolLoop.RunAsync(stream, new TurnRequest("SYS", [], [], new TurnLimits()), run, run.Cancel.Token);

        Assert.Empty(run.Transcript);
    }

    [Fact]
    public async Task Round_failure_reaches_the_caller_and_appends_nothing()
    {
        var run = new Run([]);
        StreamRound stream = (r, d, ct) => throw new InvalidOperationException("boom");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ClientToolLoop.RunAsync(stream, new TurnRequest("SYS", [], [], new TurnLimits()), run, CancellationToken.None));

        Assert.Empty(run.Transcript);
    }

    [Fact]
    public async Task Limits_reach_every_round()
    {
        var run = new Run([new("tool_calls", Call: "read"), new("stop", Text: "ok")]);
        await run.Go(new TurnLimits { Thinking = false, MaxTokens = 123 });

        Assert.All(run.Requests, r =>
        {
            Assert.False(r.Thinking);
            Assert.Equal(123, r.MaxTokens);
        });
    }

    private sealed class ContextSpy(Run run, List<bool> lastRounds) : ITurnHost
    {
        public void OnRoundStart() => run.OnRoundStart();
        public void OnText(string delta) => run.OnText(delta);
        public void OnReasoning(string delta) => run.OnReasoning(delta);
        public void OnRoundStats(RoundStats stats) => run.OnRoundStats(stats);
        public void OnNotice(string text) => run.OnNotice(text);
        public void AppendTranscript(ChatMessage message) => run.AppendTranscript(message);

        public Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken)
        {
            lastRounds.Add(context.LastRound);
            return run.OnToolCallAsync(call, context, cancellationToken);
        }
    }
}
