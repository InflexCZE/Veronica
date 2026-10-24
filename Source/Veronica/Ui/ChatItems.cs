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

public sealed class NoticeItem(string text, bool isError) : ChatItem
{
    public string Text { get; } = text;

    public bool IsError { get; } = isError;
}
