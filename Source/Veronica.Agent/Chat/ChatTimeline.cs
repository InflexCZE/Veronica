namespace Veronica.Agent.Chat;

// Turns the turn's callbacks into chat items. One thread: the caller serializes the calls.
public sealed class ChatTimeline(IList<ChatItem> items, Func<TimeSpan> clock, IReadOnlySet<string> silentTools)
{
    private const int FallbackSummaryLength = 120;

    private readonly List<RoundStats> _rounds = new();
    private readonly List<SubAgentPart> _subAgents = new();
    private TurnItem? _turn;
    private ThinkingPart? _thinking;
    private TextPart? _text;
    private bool _running;

    public bool Running => _running;

    // Bumped by each turn: a callback of an earlier turn that arrives late (an MCP call still in flight) is dropped by its host.
    public int Generation { get; private set; }

    public void BeginTurn()
    {
        CloseLive();
        Generation++;
        _running = true;
        _turn = null;
        _rounds.Clear();
        Turn();
        UpdateWaiting();
    }

    public void RoundStart()
    {
        CloseLive();
        _thinking = new ThinkingPart(clock);
        Turn().Parts.Add(_thinking);
        UpdateWaiting();
    }

    public void Reasoning(string delta)
    {
        CloseIfInterrupted();
        CloseText();
        if (_thinking == null)
        {
            _thinking = new ThinkingPart(clock);
            Turn().Parts.Add(_thinking);
        }
        _thinking.Append(delta);
        UpdateWaiting();
    }

    public void Text(string delta)
    {
        CloseIfInterrupted();
        CloseThinking();
        if (_text == null)
        {
            _text = new TextPart();
            Turn().Parts.Add(_text);
        }
        _text.Append(delta);
        UpdateWaiting();
    }

    // A tool runs with nothing live on screen but the waiting dot.
    public void ToolStarted()
    {
        CloseLive();
        UpdateWaiting();
    }

    public void ToolDone(string name, ToolResult result)
    {
        CloseLive();
        if (!silentTools.Contains(name))
            Turn().Parts.Add(Line(name, result));
        UpdateWaiting();
    }

    public void ToolStopped(string name)
    {
        CloseLive();
        if (!silentTools.Contains(name))
            Turn().Parts.Add(Stopped(name));
        UpdateWaiting();
    }

    public void Notice(string text)
    {
        CloseLive();
        Turn().Parts.Add(new NoticePart(text));
        UpdateWaiting();
    }

    public void RoundStats(RoundStats stats) => _rounds.Add(stats);

    public SubAgentPart StartSubAgent(string title)
    {
        CloseLive();
        var card = new SubAgentPart(title, clock);
        _subAgents.Add(card);
        Turn().Parts.Add(card);
        UpdateWaiting();
        return card;
    }

    public static ToolPart Stopped(string name) => new($"{Name(name)} was stopped", "", failed: true);

    public static ToolPart Line(string name, ToolResult result) =>
        new(result.Summary ?? Fallback(name, result.Output), result.Output, result.Refused);

    // A null error is a turn that ended normally or was stopped.
    public void EndTurn(string? error)
    {
        CloseLive();
        if (_turn == null)
        {
            _running = false;
            return;
        }
        if (error != null)
            Turn().Parts.Add(new ErrorPart(error));
        _turn.Stats = TurnStatsText.Footer(_rounds);
        _turn.StatsDetail = TurnStatsText.Breakdown(_rounds);
        _running = false;
        _turn.Waiting = false;
        if (_turn.Parts.Count == 0 && !_turn.HasStats)
            items.Remove(_turn);
        _turn = null;
    }

    // Once a second, for the live timers.
    public void Tick()
    {
        _thinking?.Tick();
        _subAgents.RemoveAll(c => !c.Running);
        foreach (var card in _subAgents)
            card.Tick();
    }

    public void Clear()
    {
        _thinking = null;
        _text = null;
        _subAgents.Clear();
        _turn = null;
        items.Clear();
        if (_running)
            Turn();
    }

    // A message of the user's inside a running turn (answers to a blocked question) starts a new block under its own header.
    private TurnItem Turn()
    {
        if (_turn == null || items.Count == 0 || !ReferenceEquals(items[^1], _turn))
        {
            if (_turn != null)
            {
                _turn.Waiting = false;
                if (_turn.Parts.Count == 0)
                    items.Remove(_turn);
            }
            _turn = new TurnItem();
            items.Add(_turn);
        }
        return _turn;
    }

    // Something else was added below the turn's block: what streams next goes into a new block.
    private void CloseIfInterrupted()
    {
        if (_turn != null && (items.Count == 0 || !ReferenceEquals(items[^1], _turn)))
            CloseLive();
    }

    private void CloseLive()
    {
        CloseThinking();
        CloseText();
    }

    private void CloseThinking()
    {
        if (_thinking is not { } thinking)
            return;
        _thinking = null;
        if (thinking.Kept && thinking.Text.Length > 0)
            thinking.End();
        else
            foreach (var turn in items.OfType<TurnItem>())
                if (turn.Parts.Remove(thinking))
                    break;
    }

    private void CloseText() => _text = null;

    // After a message of the user's inside the turn, the dot goes under it, in a block of its own.
    private void UpdateWaiting()
    {
        if (_turn == null)
            return;
        bool waiting = _running && _thinking == null;
        if (waiting && !ReferenceEquals(items.LastOrDefault(), _turn))
            Turn();
        _turn.Waiting = waiting;
    }

    private static string Name(string name) => name.Length > 0 ? name : "A tool call";

    private static string Fallback(string name, string output)
    {
        var first = output.Split('\n', 2)[0].Trim();
        var line = first.Length == 0 ? Name(name) : $"{Name(name)}: {first}";
        return line.Length > FallbackSummaryLength ? line[..(FallbackSummaryLength - 3)] + "..." : line;
    }
}
