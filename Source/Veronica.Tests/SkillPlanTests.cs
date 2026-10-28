using Veronica.Agent;
using Veronica.Agent.Tools;
using Xunit;

namespace Veronica.Tests;

public sealed class SkillPlanTests
{
    private static readonly Skill Block = new("create-block", "d", "BLOCK BODY", ["Create the prefab", "Fill block name"]);
    private static readonly Skill Thruster = new("block-thruster", "d", "THRUSTER BODY", ["Fill thrust", "Show the block"]);

    private sealed class Chat
    {
        public SkillPlan Plan { get; } = new();
        public ToolSet Tools { get; }

        public Chat() => Tools = new ToolSet(
        [
            new SkillTool(() => [Block, Thruster], Plan),
            new ActivateSkillTool(() => [Block, Thruster], Plan),
            new CompletePlanItemTool(Plan),
            new SkipPlanItemTool(Plan),
        ]);

        public string Call(string name, string arguments = "{}") =>
            Tools.ExecuteAsync(new ToolCall("c", name, arguments), CancellationToken.None).Result.Output;
    }

    [Fact]
    public void Activate_lists_the_plan_and_names_the_first_item()
    {
        var chat = new Chat();
        chat.Call("skill", """{"name":"create-block"}""");

        var output = chat.Call("activate_skill", """{"name":"create-block"}""");

        Assert.StartsWith("Skill create-block is active. Its plan:\n1. Create the prefab\n2. Fill block name\nWork on item 1 of 2 now: Create the prefab.", output);
        Assert.DoesNotContain("BLOCK BODY", output);
        Assert.Equal("create-block", chat.Plan.ActiveSkill);
    }

    [Fact]
    public void Current_item_note_names_the_open_item_only_while_a_plan_runs()
    {
        var chat = new Chat();
        Assert.Equal("", chat.Plan.CurrentItemNote());

        chat.Call("activate_skill", """{"name":"create-block"}""");
        chat.Call("complete_plan_item");

        Assert.Equal(" You are on item 2 of 2 of create-block: Fill block name.", chat.Plan.CurrentItemNote());
        chat.Call("complete_plan_item");
        Assert.Equal("", chat.Plan.CurrentItemNote());
    }

    [Fact]
    public void Activate_carries_the_body_of_a_skill_not_read_yet_once()
    {
        var chat = new Chat();

        Assert.EndsWith("\n\nThe skill:\nBLOCK BODY", chat.Call("activate_skill", """{"name":"create-block"}"""));
        Assert.DoesNotContain("BLOCK BODY", chat.Call("activate_skill", """{"name":"create-block"}"""));
    }

    [Fact]
    public void Activate_an_unknown_skill_lists_the_skills_and_keeps_the_plan()
    {
        var chat = new Chat();
        chat.Call("activate_skill", """{"name":"create-block"}""");

        var output = chat.Call("activate_skill", """{"name":"thruster"}""");

        Assert.Equal("There is no skill named 'thruster'; the skills are: create-block, block-thruster.", output);
        Assert.Equal("create-block", chat.Plan.ActiveSkill);
    }

    [Fact]
    public void Complete_ticks_items_in_order_until_the_plan_is_through()
    {
        var chat = new Chat();
        chat.Call("activate_skill", """{"name":"create-block"}""");

        Assert.StartsWith("Done: Create the prefab. Work on item 2 of 2 now: Fill block name.", chat.Call("complete_plan_item"));
        Assert.StartsWith("Done: Fill block name. The plan of create-block is through.", chat.Call("complete_plan_item"));
        Assert.Null(chat.Plan.ActiveSkill);
        Assert.StartsWith("No skill is active.", chat.Call("complete_plan_item"));
    }

    [Fact]
    public void Skip_needs_a_reason_and_records_it()
    {
        var chat = new Chat();
        chat.Call("activate_skill", """{"name":"create-block"}""");

        Assert.Equal("skip_plan_item needs a reason.", chat.Call("skip_plan_item", """{"reason":" "}"""));
        Assert.StartsWith("Skipped: Create the prefab (exists already). Work on item 2 of 2 now: Fill block name.", chat.Call("skip_plan_item", """{"reason":"exists already"}"""));
        Assert.Equal(new PlanItem("Create the prefab", PlanItemState.Skipped, "exists already"), chat.Plan.Items[0]);
    }

    [Fact]
    public void Activate_replaces_the_active_plan()
    {
        var chat = new Chat();
        chat.Call("activate_skill", """{"name":"create-block"}""");
        chat.Call("complete_plan_item");

        var output = chat.Call("activate_skill", """{"name":"block-thruster"}""");

        Assert.StartsWith("This replaces the plan of create-block, whose open items are dropped. Skill block-thruster is active.", output);
        Assert.Equal(["Fill thrust", "Show the block"], chat.Plan.Items.Select(i => i.Text));
    }

    [Fact]
    public void Reset_drops_the_plan_and_what_was_read()
    {
        var chat = new Chat();
        chat.Call("skill", """{"name":"create-block"}""");
        chat.Call("activate_skill", """{"name":"create-block"}""");

        chat.Plan.Reset();

        Assert.Null(chat.Plan.ActiveSkill);
        Assert.Contains("BLOCK BODY", chat.Call("activate_skill", """{"name":"create-block"}"""));
    }

    [Fact]
    public void End_nudge_names_the_open_item_unless_the_reply_ends_on_purpose()
    {
        var chat = new Chat();
        Assert.Null(chat.Plan.EndNudge("Done."));

        chat.Call("activate_skill", """{"name":"create-block"}""");
        chat.Call("complete_plan_item");

        var nudge = chat.Plan.EndNudge("Done.");
        Assert.NotNull(nudge);
        Assert.StartsWith("<harness>You ended your turn, but item 2 of 2 of create-block is still open: Fill block name.", nudge);
        Assert.EndsWith(SkillPlan.EndTurnMarker + "</harness>", nudge);
        Assert.Null(chat.Plan.EndNudge("What name? " + SkillPlan.EndTurnMarker));
    }

    [Fact]
    public void End_marker_is_hidden_from_display()
    {
        Assert.Equal("What name?", SkillPlan.StripEndTurnMarker("What name? <end_turn>"));
    }

    private sealed record Round(string? Text = null, string? Call = null, string Arguments = "{}");

    private sealed class Run(Chat chat, IEnumerable<Round> rounds) : ITurnHost
    {
        private readonly Queue<Round> _rounds = new(rounds);

        public List<ChatMessage> Transcript { get; } = [];
        public List<string> Notices { get; } = [];
        public int Remaining => _rounds.Count;

        public Task Go(int maxRounds) =>
            ClientToolLoop.RunAsync(Stream, new TurnRequest("SYS", [new ChatMessage(ChatRole.User, "Make a block.")], chat.Tools.Definitions,
                Prompts.ChatLimits(chat.Plan) with { MaxRounds = maxRounds, LengthNudge = null }), this, CancellationToken.None);

        private Task Stream(RoundRequest request, Action<StreamDelta> onDelta, CancellationToken cancellationToken)
        {
            var round = _rounds.Dequeue();
            if (round.Text != null) onDelta(new TextDelta(round.Text));
            if (round.Call != null) onDelta(new ToolCallDelta(0, "c", round.Call, round.Arguments));
            onDelta(new DoneDelta(round.Call != null ? "tool_calls" : "stop"));
            return Task.CompletedTask;
        }

        public void OnRoundStart() { }
        public void OnText(string delta) { }
        public void OnReasoning(string delta) { }
        public void OnRoundStats(RoundStats stats) { }
        public void OnNotice(string text) => Notices.Add(text);
        public void AppendTranscript(ChatMessage message) => Transcript.Add(message);

        public async Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken) =>
            await chat.Tools.ExecuteAsync(call, cancellationToken);
    }

    [Fact]
    public async Task Plan_tool_rounds_are_free()
    {
        var chat = new Chat();
        var run = new Run(chat,
        [
            new(Call: "skill", Arguments: """{"name":"create-block"}"""),
            new(Call: "activate_skill", Arguments: """{"name":"create-block"}"""),
            new(Call: "complete_plan_item"),
            new(Call: "skip_plan_item", Arguments: """{"reason":"none"}"""),
            new(Call: "read"),
            new(Text: "Done."),
        ]);

        await run.Go(maxRounds: 2);

        Assert.Empty(run.Notices);
        Assert.Equal(0, run.Remaining);
    }

    [Fact]
    public async Task Ending_with_an_open_item_is_sent_back_once()
    {
        var chat = new Chat();
        var run = new Run(chat,
        [
            new(Call: "activate_skill", Arguments: """{"name":"create-block"}"""),
            new(Text: "Done."),
            new(Text: "Done, really."),
            new(Text: "never"),
        ]);

        await run.Go(maxRounds: 10);

        var nudge = Assert.Single(run.Transcript, m => m.Role == ChatRole.User);
        Assert.StartsWith("<harness>", nudge.Content);
        Assert.Equal(1, run.Remaining);
    }

    [Fact]
    public async Task End_marker_stops_without_a_reminder()
    {
        var chat = new Chat();
        var run = new Run(chat,
        [
            new(Call: "activate_skill", Arguments: """{"name":"create-block"}"""),
            new(Text: "What should the block be called? <end_turn>"),
            new(Text: "never"),
        ]);

        await run.Go(maxRounds: 10);

        Assert.DoesNotContain(run.Transcript, m => m.Role == ChatRole.User);
        Assert.Equal(1, run.Remaining);
        Assert.Equal("create-block", chat.Plan.ActiveSkill);
    }
}
