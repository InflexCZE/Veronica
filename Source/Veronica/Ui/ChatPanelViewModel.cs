using System.Collections.ObjectModel;
using Keen.VRage.Editor.Services.Docking;

namespace Veronica.Ui;

public sealed record ChatMessage(string Author, string Text, bool FromUser);

public sealed class ChatPanelViewModel : DockItemViewModel
{
    private string _draft = "";

    public ChatPanelViewModel()
    {
        Header = "Veronica";
        ContentTemplateResourceKey = "VeronicaChatPanel";
        CanClose = true;
        CanFloat = true;

        SendCommand = new UiCommand(Send);

        Messages.Add(new ChatMessage("Veronica", "Hi, I am Veronica. I am not connected to a model yet, this panel is a static preview.", false));
        Messages.Add(new ChatMessage("You", "Can you render text outside Roboto?", true));
        Messages.Add(new ChatMessage("Veronica", "Emoji \U0001F680 \U0001F916, CJK \u4F60\u597D \u3053\u3093\u306B\u3061\u306F, symbols \u2713 \u2192 \u2211 \u221E.", false));
    }

    // The dock item's content is built from a template with no data, so ChatView looks its view model
    // up here, the way the Editor's own panels use its view model registry.
    public static ChatPanelViewModel? Current { get; set; }

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public string Draft
    {
        get => _draft;
        set
        {
            if (_draft == value)
                return;
            _draft = value;
            OnPropertyChanged(nameof(Draft));
        }
    }

    public UiCommand SendCommand { get; }

    private void Send()
    {
        var text = Draft.Trim();
        if (text.Length == 0)
            return;

        Messages.Add(new ChatMessage("You", text, true));
        Messages.Add(new ChatMessage("Veronica", "(not connected to a model yet)", false));
        Draft = "";
    }
}
