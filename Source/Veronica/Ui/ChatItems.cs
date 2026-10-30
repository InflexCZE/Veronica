using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Veronica.Ui;

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

public sealed class AssistantItem : ChatItem
{
    private string _reasoning = "";
    private string _text = "";
    private bool _thinkingExpanded;

    public string Reasoning
    {
        get => _reasoning;
        private set
        {
            Set(ref _reasoning, value);
            Changed(nameof(HasReasoning));
        }
    }

    public string Text
    {
        get => _text;
        private set
        {
            Set(ref _text, value);
            Changed(nameof(HasText));
        }
    }

    public ObservableCollection<ToolActivity> Tools { get; } = new();

    public bool HasReasoning => _reasoning.Length > 0;

    public bool HasText => _text.Length > 0;

    public bool ThinkingExpanded
    {
        get => _thinkingExpanded;
        set => Set(ref _thinkingExpanded, value);
    }

    public void AppendReasoning(string delta)
    {
        if (!HasReasoning && !HasText)
            ThinkingExpanded = true;
        Reasoning += delta;
    }

    public void AppendText(string delta)
    {
        if (!HasText && HasReasoning)
            ThinkingExpanded = false;
        Text += delta;
    }
}

public sealed class ToolActivity(string call) : ChatItem
{
    private readonly string _call = call;
    private string _text = call;

    public string Text
    {
        get => _text;
        private set => Set(ref _text, value);
    }

    public void Complete(string result) => Text = $"{_call} -> {result}";
}

public sealed class SubAgentItem(string title) : ChatItem
{
    private string _headline = title + " ...";
    private string _answer = "";

    public string Title { get; } = title;

    public string Headline
    {
        get => _headline;
        private set => Set(ref _headline, value);
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

    public ObservableCollection<string> Steps { get; } = new();

    public void Finish(string answer, string outcome)
    {
        Headline = $"{Title}: {outcome}";
        Answer = answer;
    }
}

public sealed class NoticeItem(string text, bool isError) : ChatItem
{
    public string Text { get; } = text;

    public bool IsError { get; } = isError;
}
