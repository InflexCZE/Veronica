using Veronica.Agent.Chat;

namespace Veronica.Ui;

// A turn ended at a full context. A null compact offers only Clear. The choices go once one is taken or another turn starts.
public sealed class ContextFullItem : ChatItem
{
    private bool _open = true;

    public ContextFullItem(string text, Action? compact, Action clear)
    {
        Text = text;
        CanCompact = compact != null;
        CompactCommand = new UiCommand(() => Take(compact));
        ClearCommand = new UiCommand(() => Take(clear));
    }

    public string Text { get; }

    public bool CanCompact { get; }

    public UiCommand CompactCommand { get; }

    public UiCommand ClearCommand { get; }

    public bool Open
    {
        get => _open;
        private set
        {
            Set(ref _open, value);
            Changed(nameof(ShowCompact));
        }
    }

    public bool ShowCompact => _open && CanCompact;

    public void Close() => Open = false;

    private void Take(Action? choice)
    {
        if (!_open || choice == null)
            return;
        Close();
        choice();
    }
}
