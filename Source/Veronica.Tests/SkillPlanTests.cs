using Veronica.Agent;
using Veronica.Agent.Tools;
using Xunit;

namespace Veronica.Tests;

public sealed class SkillPlanTests
{
    private static readonly Skill Block = new("create-block", "d", "Plan:\n- Create the prefab\n\nBLOCK BODY", ["Create the prefab", "Fill block name", "Next skill"]);
    private static readonly Skill Thruster = new("block-thruster", "d", "THRUSTER BODY", ["Fill thrust", "Show the block"]);

    private sealed class Chat
    {
        public SkillPlan Plan { get; } = new();
        public ToolSet Tools { get; }

        public Chat() => Tools = new ToolSet(
        [
            new SkillTool(() => [Block, Thruster], Plan),
            new ActivateSkillTool(() => [Block, Thruster], Plan),
            new MarkCompletedTool(Plan),
            new MarkSkippedTool(Plan),
        ]);

        public string Call(string name, string arguments = "{}") =>
            Tools.ExecuteAsync(new ToolCall("c", name, arguments), CancellationToken.None).Result.Output;
    }

    [Fact]
    public void Activate_names_only_the_current_item()
    {
        var chat = new Chat();
        chat.Call("skill", """{"name":"create-block"}""");

        var output = chat.Call("activate_skill", """{"name":"create-block"}""");

        Assert.StartsWith("Skill create-block is active. Work on item 1 of 3 now: Create the prefab.", output);
        Assert.DoesNotContain("Fill block name", output);
        Assert.DoesNotContain("BLOCK BODY", output);
        Assert.Equal("create-block", chat.Plan.ActiveSkill);
    }

    [Fact]
    public void Current_item_note_names_the_open_item_only_while_a_plan_runs()
    {
        var chat = new Chat();
        Assert.Equal("", chat.Plan.CurrentItemNote());

        chat.Call("activate_skill", """{"name":"create-block"}""");
        Done(chat);

        Assert.Equal(" You are on item 2 of 3 of create-block: Fill block name.", chat.Plan.CurrentItemNote());
        Done(chat);
        Done(chat);
        Assert.Equal("", chat.Plan.CurrentItemNote());
    }

    [Fact]
    public void Activate_carries_the_overview_of_a_skill_not_read_yet_once()
    {
        var chat = new Chat();

        Assert.EndsWith("\n\nThe skill:\nBLOCK BODY", chat.Call("activate_skill", """{"name":"create-block"}"""));
        chat.Call("mark_completed", """{"proof":"p"}""");
        chat.Call("mark_completed", """{"proof":"p"}""");
        chat.Call("mark_completed", """{"proof":"p"}""");

        var again = chat.Call("activate_skill", """{"name":"create-block"}""");

        Assert.StartsWith("Skill create-block is active.", again);
        Assert.DoesNotContain("BLOCK BODY", again);
    }

    [Fact]
    public void Activate_the_active_skill_keeps_its_plan_and_names_the_current_item()
    {
        var chat = new Chat();
        chat.Call("activate_skill", """{"name":"create-block"}""");
        chat.Call("mark_completed", """{"proof":"p"}""");

        var output = chat.Call("activate_skill", """{"name":"create-block"}""");

        Assert.StartsWith("create-block is already active. Work on item 2 of 3", output);
        Assert.Equal(PlanItemState.Done, chat.Plan.Items[0].State);
    }

    [Fact]
    public void Marking_without_a_plan_says_no_skill_is_active_before_asking_for_proof()
    {
        var chat = new Chat();

        Assert.StartsWith("No skill is active.", chat.Call("mark_completed", "{}"));
        Assert.StartsWith("No skill is active.", chat.Call("mark_skipped", "{}"));
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
    public void Mark_completed_needs_proof_and_ticks_items_in_order_until_the_plan_is_through()
    {
        var chat = new Chat();
        chat.Call("activate_skill", """{"name":"create-block"}""");

        Assert.Equal("mark_completed needs proof: one line on what you did for the item.", chat.Call("mark_completed", """{"proof":" "}"""));
        Assert.Equal("mark_completed needs proof: one line on what you did for the item.", chat.Call("mark_completed"));
        Assert.StartsWith("Done: Create the prefab (made it). Work on item 2 of 3 now: Fill block name.", chat.Call("mark_completed", """{"proof":"made it"}"""));
        Assert.Equal(new PlanItem("Create the prefab", PlanItemState.Done, "made it"), chat.Plan.Items[0]);
        Done(chat);
        Assert.StartsWith("Done: Next skill (did it). The plan of create-block is through.", Done(chat));
        Assert.Null(chat.Plan.ActiveSkill);
        Assert.StartsWith("No skill is active.", Done(chat));
    }

    [Fact]
    public void Mark_skipped_needs_a_reason_and_records_it()
    {
        var chat = new Chat();
        chat.Call("activate_skill", """{"name":"create-block"}""");

        Assert.Equal("mark_skipped needs a reason.", chat.Call("mark_skipped", """{"reason":" "}"""));
        Assert.StartsWith("Skipped: Create the prefab (exists already). Work on item 2 of 3 now: Fill block name.", chat.Call("mark_skipped", """{"reason":"exists already"}"""));
        Assert.Equal(new PlanItem("Create the prefab", PlanItemState.Skipped, "exists already"), chat.Plan.Items[0]);
    }

    [Fact]
    public void Activate_is_refused_while_an_item_before_the_last_is_open()
    {
        var chat = new Chat();
        chat.Call("activate_skill", """{"name":"create-block"}""");
        Done(chat);

        var output = chat.Call("activate_skill", """{"name":"block-thruster"}""");

        Assert.Equal("create-block still has open items: 2. Fill block name. Before activating another skill, call mark_completed with proof for each one you did, " +
            "or mark_skipped with the reason it does not apply.", output);
        Assert.Equal("create-block", chat.Plan.ActiveSkill);
        Assert.Equal(PlanItemState.Open, chat.Plan.Items[1].State);
    }

    [Fact]
    public void Activate_from_the_last_item_hands_off()
    {
        var chat = new Chat();
        chat.Call("activate_skill", """{"name":"create-block"}""");
        Done(chat);
        Done(chat);

        var output = chat.Call("activate_skill", """{"name":"block-thruster"}""");

        Assert.StartsWith("Skill block-thruster is active. Work on item 1 of 2 now: Fill thrust.", output);
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
        Done(chat);

        var nudge = chat.Plan.EndNudge("Done.");
        Assert.NotNull(nudge);
        Assert.StartsWith("<harness>You ended your turn, but item 2 of 3 of create-block is still open: Fill block name.", nudge);
        Assert.Contains("mark_completed", nudge);
        Assert.Contains("mark_skipped", nudge);
        Assert.EndsWith(SkillPlan.EndTurnMarker + "</harness>", nudge);
        Assert.Null(chat.Plan.EndNudge("What name? " + SkillPlan.EndTurnMarker));
    }

    [Fact]
    public void End_marker_is_hidden_from_display()
    {
        Assert.Equal("What name?", SkillPlan.StripEndTurnMarker("What name? <end_turn>"));
    }

    private static string Done(Chat chat) => chat.Call("mark_completed", """{"proof":"did it"}""");

    [Fact]
    public void Plan_tool_names_are_free_and_bookkeeping()
    {
        Assert.Equal(new HashSet<string> { "skill", "activate_skill", "mark_completed", "mark_skipped" }, SkillPlan.ToolNames);
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
            new(Call: "mark_completed", Arguments: """{"proof":"did it"}"""),
            new(Call: "mark_skipped", Arguments: """{"reason":"none"}"""),
            new(Call: "mark_completed", Arguments: """{"proof":"did it"}"""),
            new(Call: "read"),
            new(Text: "Done."),
        ]);

        // The hard cap is three times the budget, so 7 rounds need a budget of 3; only "read" counts against it.
        await run.Go(maxRounds: 3);

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
