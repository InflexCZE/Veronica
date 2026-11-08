using System.Collections.ObjectModel;
using Veronica.Agent;
using Veronica.Agent.Chat;
using Xunit;

namespace Veronica.Tests;

public sealed class ChatTimelineTests
{
    private readonly ObservableCollection<ChatItem> _items = new();
    private TimeSpan _now;
    private readonly ChatTimeline _timeline;

    public ChatTimelineTests() =>
        _timeline = new ChatTimeline(_items, () => _now, new HashSet<string> { "ask_user" });

    private TurnItem Turn => Assert.IsType<TurnItem>(_items[^1]);

    [Fact]
    public void A_multi_round_turn_is_one_block_in_the_order_things_happened()
    {
        _timeline.BeginTurn();
        _timeline.RoundStart();
        _timeline.Text("Let me look.");
        _timeline.ToolDone("read", new ToolResult("raw") { Summary = "Read Door.def" });
        _timeline.RoundStart();
        _timeline.Text("It is a door.");
        _timeline.EndTurn(null);

        var turn = Assert.IsType<TurnItem>(Assert.Single(_items));
        Assert.Collection(turn.Parts,
            p => Assert.Equal("Let me look.", Assert.IsType<TextPart>(p).Text),
            p => Assert.Equal("Read Door.def", Assert.IsType<ToolPart>(p).Summary),
            p => Assert.Equal("It is a door.", Assert.IsType<TextPart>(p).Text));
    }

    [Fact]
    public void Thinking_nobody_clicked_goes_when_the_round_moves_on()
    {
        _timeline.BeginTurn();
        _timeline.RoundStart();
        _timeline.Reasoning("hmm");
        Assert.IsType<ThinkingPart>(Assert.Single(Turn.Parts));
        _timeline.Text("Hi");
        Assert.IsType<TextPart>(Assert.Single(Turn.Parts));
    }

    [Fact]
    public void Clicked_thinking_stays_with_its_duration()
    {
        _timeline.BeginTurn();
        _timeline.RoundStart();
        _timeline.Reasoning("hmm");
        var thinking = Assert.IsType<ThinkingPart>(Assert.Single(Turn.Parts));
        _now = TimeSpan.FromSeconds(7.6);
        Assert.Equal("Thinking 7s", thinking.Label);
        thinking.Toggle();
        Assert.True(thinking.Expanded);
        thinking.Toggle();
        _now = TimeSpan.FromSeconds(9);
        _timeline.Text("Hi");
        _now = TimeSpan.FromSeconds(30);

        Assert.Same(thinking, Turn.Parts[0]);
        Assert.False(thinking.Live);
        Assert.False(thinking.Expanded);
        Assert.Equal("Thought for 9s", thinking.Label);
    }

    [Fact]
    public void Clicked_thinking_without_text_still_goes()
    {
        _timeline.BeginTurn();
        _timeline.RoundStart();
        Assert.IsType<ThinkingPart>(Assert.Single(Turn.Parts)).Toggle();
        _timeline.Text("Hi");
        Assert.IsType<TextPart>(Assert.Single(Turn.Parts));
    }

    [Fact]
    public void The_tail_is_the_latest_reasoning_on_one_line()
    {
        _timeline.BeginTurn();
        _timeline.Reasoning(new string('a', 300) + "\n\nlast   words ");
        var thinking = Assert.IsType<ThinkingPart>(Assert.Single(Turn.Parts));
        Assert.EndsWith("a last words", thinking.Tail);
        Assert.True(thinking.Tail.Length <= 200);
    }

    [Fact]
    public void Ask_user_gets_no_line_and_a_tool_without_summary_falls_back()
    {
        _timeline.BeginTurn();
        _timeline.ToolDone("ask_user", new ToolResult("shown") { Summary = "Asked: what?" });
        _timeline.ToolDone("skill", new ToolResult("first line\nsecond"));
        var tool = Assert.IsType<ToolPart>(Assert.Single(Turn.Parts));
        Assert.Equal("skill: first line", tool.Summary);
        Assert.Equal("first line\nsecond", tool.Detail);
    }

    [Fact]
    public void A_refused_call_is_a_failed_line()
    {
        _timeline.BeginTurn();
        _timeline.ToolDone("read", new ToolResult("No such item", Refused: true) { Summary = "Could not read X" });
        Assert.True(Assert.IsType<ToolPart>(Assert.Single(Turn.Parts)).Failed);
    }

    [Fact]
    public void Waiting_shows_only_while_nothing_live_is_on_screen()
    {
        _timeline.BeginTurn();
        Assert.True(Turn.Waiting);
        _timeline.RoundStart();
        Assert.False(Turn.Waiting);
        _timeline.Text("Hi");
        Assert.True(Turn.Waiting);
        _timeline.EndTurn(null);
        Assert.False(Assert.IsType<TurnItem>(Assert.Single(_items)).Waiting);
    }

    [Fact]
    public void The_footer_comes_with_the_end_of_the_turn()
    {
        _timeline.BeginTurn();
        _timeline.RoundStart();
        _timeline.Text("Hi");
        _timeline.RoundStats(new RoundStats(100, 10, 100, 10));
        Assert.False(Turn.HasStats);
        _timeline.EndTurn(null);
        Assert.Equal("read 100 tok/s . write 10 tok/s . 100 read / 10 written", Turn.Stats);
    }

    [Fact]
    public void A_failed_turn_ends_with_its_error()
    {
        _timeline.BeginTurn();
        _timeline.EndTurn("The server went away.");
        Assert.Equal("The server went away.", Assert.IsType<ErrorPart>(Assert.Single(Turn.Parts)).Text);
    }

    [Fact]
    public void An_empty_turn_leaves_nothing()
    {
        _timeline.BeginTurn();
        _timeline.RoundStart();
        _timeline.EndTurn(null);
        Assert.Empty(_items);
    }

    [Fact]
    public void A_user_message_inside_a_turn_starts_a_new_block()
    {
        _timeline.BeginTurn();
        _timeline.Text("Which one?");
        _items.Add(new UserItem("The big one"));
        _timeline.Text("Got it.");
        _timeline.EndTurn(null);

        Assert.Equal(3, _items.Count);
        Assert.IsType<TurnItem>(_items[0]);
        Assert.IsType<UserItem>(_items[1]);
        Assert.Equal("Got it.", Assert.IsType<TextPart>(Assert.Single(Assert.IsType<TurnItem>(_items[2]).Parts)).Text);
    }

    [Fact]
    public void A_subagent_card_collects_steps_and_hides_them_once_done()
    {
        _timeline.BeginTurn();
        var card = _timeline.StartSubAgent("Wiki research");
        card.AddStep(ChatTimeline.Line("wiki_page", new ToolResult("page") { Summary = "Read wiki page: Blocks" }));
        Assert.True(card.ShowSteps);
        _now = TimeSpan.FromSeconds(4);
        card.Finish("The answer.", "Finished");

        Assert.False(card.Running);
        Assert.False(card.ShowSteps);
        Assert.Equal("4s", card.Elapsed);
        card.Toggle();
        Assert.True(card.ShowSteps);
        Assert.Equal("Hide", card.ToggleLabel);
    }

    [Fact]
    public void A_running_tool_ends_the_thinking_and_shows_the_dot()
    {
        _timeline.BeginTurn();
        _timeline.RoundStart();
        _timeline.Reasoning("call a tool");
        _timeline.ToolStarted();
        Assert.Empty(Turn.Parts);
        Assert.True(Turn.Waiting);
    }

    [Fact]
    public void An_answer_after_a_silent_question_leaves_no_empty_header_and_the_dot_goes_below()
    {
        _timeline.BeginTurn();
        _timeline.RoundStart();
        _timeline.ToolStarted();
        _items.Add(new UserItem("The big one"));
        _timeline.ToolDone("ask_user", new ToolResult("answers"));

        Assert.Equal(2, _items.Count);
        Assert.IsType<UserItem>(_items[0]);
        Assert.True(Turn.Waiting);
    }

    [Fact]
    public void An_error_after_the_users_answer_goes_below_it()
    {
        _timeline.BeginTurn();
        _timeline.Text("Which one?");
        _items.Add(new UserItem("The big one"));
        _timeline.EndTurn("Lost.");
        Assert.IsType<ErrorPart>(Assert.Single(Turn.Parts));
    }

    [Fact]
    public void Each_turn_has_a_new_generation()
    {
        _timeline.BeginTurn();
        int first = _timeline.Generation;
        _timeline.EndTurn(null);
        _timeline.BeginTurn();
        Assert.NotEqual(first, _timeline.Generation);
    }
}
