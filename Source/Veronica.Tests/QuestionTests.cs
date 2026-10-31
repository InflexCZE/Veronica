using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Xunit;

namespace Veronica.Tests;

public sealed class QuestionTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static readonly Question Name = new("Name?", ["Bob"]);
    private static readonly Question Folder = new("Folder?", []);

    [Fact]
    public void Questions_parse_with_options_from_objects_strings_and_json_text()
    {
        var parsed = AskUserTool.Parse(Json("""{"questions":[{"question":" Name? ","options":["Bob"," ",3]},"Folder?"]}"""));
        Assert.NotNull(parsed);
        Assert.Equal(["Name?", "Folder?"], parsed.Select(q => q.Text));
        Assert.Equal(["Bob"], parsed[0].Options);
        Assert.Empty(parsed[1].Options);

        Assert.Single(AskUserTool.Parse(Json("""{"questions":"[{\"question\":\"Name?\"}]"}"""))!);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"questions":[]}""")]
    [InlineData("""{"questions":"nope"}""")]
    [InlineData("""{"questions":[{"question":"ok"},{"options":["a"]}]}""")]
    [InlineData("""{"questions":[{"question":"  "}]}""")]
    public void Unreadable_questions_are_refused_whole(string arguments) =>
        Assert.Null(AskUserTool.Parse(Json(arguments)));

    [Fact]
    public async Task Posting_tool_shows_the_card_and_returns_at_once()
    {
        var board = new QuestionBoard();
        var result = await new AskUserTool(board, waitsForAnswers: false).ExecuteAsync(Json("""{"questions":["Name?"]}"""), CancellationToken.None);

        Assert.Equal(AskUserTool.ShownOutput, result.Output);
        Assert.False(result.Refused);
        Assert.Equal(["Name?"], board.Questions.Select(q => q.Text));
    }

    [Fact]
    public async Task Malformed_call_is_refused_and_shows_nothing()
    {
        var board = new QuestionBoard();
        var result = await new AskUserTool(board, waitsForAnswers: true).ExecuteAsync(Json("""{"questions":[]}"""), CancellationToken.None);

        Assert.True(result.Refused);
        Assert.Empty(board.Questions);
    }

    [Fact]
    public async Task Waiting_tool_returns_the_answers()
    {
        var board = new QuestionBoard();
        var call = new AskUserTool(board, waitsForAnswers: true).ExecuteAsync(Json("""{"questions":["Name?","Folder?"]}"""), CancellationToken.None);
        Assert.False(call.IsCompleted);

        Assert.Null(board.Answer(["Bob", "  "]).Refused);

        Assert.Equal("Q: Name?\nA: Bob\n\nQ: Folder?\nA: (skipped)", (await call).Output);
        Assert.Empty(board.Questions);
    }

    [Fact]
    public void Posted_cards_merge_and_their_answers_are_the_next_message()
    {
        var board = new QuestionBoard();
        board.Post([Name]);
        board.Post([Folder]);
        Assert.Equal(2, board.Questions.Count);

        var answers = board.Answer([null, "Mods/X"]);
        Assert.Null(answers.Refused);
        Assert.Null(answers.Waited);
        Assert.Equal("Q: Name?\nA: (skipped)\n\nQ: Folder?\nA: Mods/X", answers.Unwaited);
    }

    [Fact]
    public async Task Merged_waiters_each_get_their_own_answers()
    {
        var board = new QuestionBoard();
        var first = board.WaitAsync([Name], CancellationToken.None);
        var second = board.WaitAsync([Folder], CancellationToken.None);

        var answers = board.Answer(["Bob", "Mods/X"]);

        Assert.Equal("Q: Name?\nA: Bob", await first);
        Assert.Equal("Q: Folder?\nA: Mods/X", await second);
        Assert.Equal("Q: Name?\nA: Bob\n\nQ: Folder?\nA: Mods/X", answers.Waited);
    }

    [Fact]
    public void Answer_needs_an_open_card_and_one_answer_per_question()
    {
        var board = new QuestionBoard();
        Assert.NotNull(board.Answer(["x"]).Refused);

        board.Post([Name, Folder]);
        Assert.NotNull(board.Answer(["x"]).Refused);
        Assert.Equal(2, board.Questions.Count);
    }

    [Fact]
    public async Task Closing_tells_the_waiter_and_empties_the_card()
    {
        var board = new QuestionBoard();
        int changes = 0;
        board.Changed += () => changes++;
        var waiting = board.WaitAsync([Name], CancellationToken.None);

        board.Close();

        Assert.Equal(QuestionBoard.ClosedOutput, await waiting);
        Assert.Empty(board.Questions);
        Assert.Equal(2, changes);
        board.Close();
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task Cancelled_wait_takes_back_only_its_own_questions_as_a_new_card()
    {
        var board = new QuestionBoard();
        board.Post([Folder]);
        using var stop = new CancellationTokenSource();
        var waiting = board.WaitAsync([Name], stop.Token);
        int card = board.Snapshot().Card;

        stop.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal([Folder], board.Questions);
        Assert.NotEqual(card, board.Snapshot().Card);
    }

    [Fact]
    public void Answering_starts_a_new_card()
    {
        var board = new QuestionBoard();
        board.Post([Name]);
        int card = board.Snapshot().Card;
        board.Post([Folder]);
        Assert.Equal(card, board.Snapshot().Card);

        board.Answer(["a", "b"]);
        board.Post([Name]);

        Assert.NotEqual(card, board.Snapshot().Card);
    }

    [Fact]
    public async Task Waiting_question_does_not_hold_the_tool_queue()
    {
        var board = new QuestionBoard();
        var tools = new ToolSet([new AskUserTool(board, waitsForAnswers: true), new Echo()]);

        var asking = tools.ExecuteAsync(new ToolCall("1", AskUserTool.ToolName, """{"questions":["Name?"]}"""), CancellationToken.None);
        var echo = await tools.ExecuteAsync(new ToolCall("2", "echo", "{}"), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("echo", echo.Output);
        Assert.False(asking.IsCompleted);
        board.Close();
        Assert.Equal(QuestionBoard.ClosedOutput, (await asking).Output);
    }

    [Fact]
    public void With_replaces_a_tool_of_the_same_name()
    {
        var board = new QuestionBoard();
        var tools = new ToolSet([new AskUserTool(board, waitsForAnswers: false), new Echo()]).With(new AskUserTool(board, waitsForAnswers: true));

        Assert.Equal(["echo", AskUserTool.ToolName], tools.Definitions.Select(d => d.Name));
    }

    private sealed class Echo : ITool
    {
        public string Name => "echo";
        public string Description => "";
        public string Parameters => """{"type":"object"}""";
        public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) => Task.FromResult(new ToolResult("echo"));
    }
}
