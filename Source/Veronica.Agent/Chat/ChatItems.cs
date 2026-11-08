using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Veronica.Agent.Chat;

public abstract class ChatItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class UserItem(string text) : ChatItem
{
    public string Text { get; } = text;
}

// One turn of Veronica's under a single header: thinking, text and tool lines in the order they happened.
public sealed class TurnItem : ChatItem
{
    private bool _waiting;
    private string? _stats;
    private string? _statsDetail;

    public ObservableCollection<TurnPart> Parts { get; } = new();

    // The turn runs with nothing live to show for it, e.g. while a tool works.
    public bool Waiting
    {
        get => _waiting;
        internal set => Set(ref _waiting, value);
    }

    public string? Stats
    {
        get => _stats;
        internal set
        {
            Set(ref _stats, value);
            Changed(nameof(HasStats));
        }
    }

    public string? StatsDetail
    {
        get => _statsDetail;
        internal set => Set(ref _statsDetail, value);
    }

    public bool HasStats => _stats != null;
}

public abstract class TurnPart : ChatItem
{
}

public sealed class ThinkingPart : TurnPart
{
    private const int TailLength = 200;
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    private readonly Func<TimeSpan> _clock;
    private readonly TimeSpan _started;
    private TimeSpan? _ended;
    private string _text = "";
    private bool _expanded;

    internal ThinkingPart(Func<TimeSpan> clock)
    {
        _clock = clock;
        _started = clock();
    }

    public string Text
    {
        get => _text;
        private set
        {
            Set(ref _text, value);
            Changed(nameof(Tail));
        }
    }

    public bool Live => _ended == null;

    // Clicked at least once: the part stays after the thinking ends.
    public bool Kept { get; private set; }

    public bool Expanded
    {
        get => _expanded;
        private set
        {
            Set(ref _expanded, value);
            Changed(nameof(ShowTail));
        }
    }

    public bool ShowTail => Live && !_expanded;

    public string Tail
    {
        get
        {
            var tail = _text.Length > TailLength ? _text[^TailLength..] : _text;
            return Whitespace.Replace(tail, " ").Trim();
        }
    }

    public string Label
    {
        get
        {
            int seconds = (int)((_ended ?? _clock()) - _started).TotalSeconds;
            return Live
                ? string.Create(CultureInfo.InvariantCulture, $"Thinking {seconds}s")
                : string.Create(CultureInfo.InvariantCulture, $"Thought for {seconds}s");
        }
    }

    public void Toggle()
    {
        Kept = true;
        Expanded = !_expanded;
    }

    internal void Append(string delta) => Text += delta;

    internal void End()
    {
        _ended = _clock();
        Changed(nameof(Live));
        Changed(nameof(ShowTail));
        Changed(nameof(Label));
    }

    internal void Tick() => Changed(nameof(Label));
}

public sealed class TextPart : TurnPart
{
    private string _text = "";

    public string Text
    {
        get => _text;
        private set
        {
            Set(ref _text, value);
            Changed(nameof(Blocks));
        }
    }

    public IReadOnlyList<MarkupBlock> Blocks => ChatMarkup.Parse(_text);

    internal void Append(string delta) => Text += delta;
}

public sealed class ToolPart : TurnPart
{
    private bool _expanded;

    public ToolPart(string summary, string detail, bool failed)
    {
        Summary = summary;
        Detail = detail;
        Failed = failed;
    }

    public string Summary { get; }

    // What the model received.
    public string Detail { get; }

    public bool Failed { get; }

    public bool Expanded
    {
        get => _expanded;
        private set => Set(ref _expanded, value);
    }

    public void Toggle() => Expanded = !_expanded;
}

public sealed class NoticePart(string text) : TurnPart
{
    public string Text { get; } = text;
}

public sealed class ErrorPart(string text) : TurnPart
{
    public string Text { get; } = text;
}

// A subagent's card: its steps as it works, then a one-line outcome with its answer behind Show.
public sealed class SubAgentPart : TurnPart
{
    private readonly Func<TimeSpan> _clock;
    private readonly TimeSpan _started;
    private TimeSpan? _ended;
    private string _outcome = "";
    private string _answer = "";
    private bool _expanded;

    internal SubAgentPart(string title, Func<TimeSpan> clock)
    {
        Title = title;
        _clock = clock;
        _started = clock();
    }

    public string Title { get; }

    public ObservableCollection<ToolPart> Steps { get; } = new();

    public bool Running => _ended == null;

    public string Elapsed =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)((_ended ?? _clock()) - _started).TotalSeconds}s");

    public string Outcome
    {
        get => _outcome;
        private set => Set(ref _outcome, value);
    }

    public string Answer
    {
        get => _answer;
        private set
        {
            Set(ref _answer, value);
            Changed(nameof(HasAnswer));
        }
    }

    public bool HasAnswer => _answer.Length > 0;

    // Steps show while it runs; once done they hide behind Show with the answer.
    public bool ShowSteps => Running || _expanded;

    public bool Expanded
    {
        get => _expanded;
        private set
        {
            Set(ref _expanded, value);
            Changed(nameof(ShowSteps));
            Changed(nameof(ToggleLabel));
        }
    }

    public string ToggleLabel => _expanded ? "Hide" : "Show";

    public void Toggle() => Expanded = !_expanded;

    public void AddStep(ToolPart step) => Steps.Add(step);

    public void Finish(string answer, string outcome)
    {
        _ended = _clock();
        Answer = answer;
        Outcome = outcome;
        Changed(nameof(Running));
        Changed(nameof(ShowSteps));
        Changed(nameof(Elapsed));
    }

    internal void Tick() => Changed(nameof(Elapsed));
}
