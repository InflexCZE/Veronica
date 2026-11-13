using Avalonia.Threading;
using Keen.VRage.Editor.Services.Docking;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.Mcp;

namespace Veronica.Ui;

// Bare MCP mode: no chat, only how to connect an agent and whether one is connected.
public sealed class McpPanelViewModel : DockItemViewModel
{
    private readonly McpClientSessions _sessions;
    private ProviderStatus? _state;
    private string _updateNotice = "";

    public McpPanelViewModel(McpClientSessions sessions, int port)
    {
        // The saved layout keys a tab by DockId, which defaults to the type name: under its own id this panel
        // would open a second tab beside the chat's remembered one.
        DockId = typeof(ChatPanelViewModel).FullName;
        Header = "Veronica";
        ContentTemplateResourceKey = "VeronicaMcpPanel";
        CanClose = true;
        CanFloat = true;

        _sessions = sessions;
        ConnectionPrompt = McpConnection.Prompt(port);
        sessions.Changed += () => Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Connections)));
        CopyCommand = new UiCommand(CopyPrompt);
        DismissUpdateCommand = new UiCommand(() => UpdateNotice = "");
    }

    // The dock item's content is built from a template with no data, so McpView looks its view model up here.
    public static McpPanelViewModel? Current { get; set; }

    public string ConnectionPrompt { get; }

    public string Connections => $"Active connections: {_sessions.OpenCount}";

    public UiCommand CopyCommand { get; }

    // Any thread.
    public void ShowStatus(ProviderStatus status) => Dispatcher.UIThread.Post(() =>
    {
        _state = status;
        OnPropertyChanged(nameof(ShowState));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(StateDetail));
    });

    public bool ShowState => _state is { Ready: false };

    public string StateText => _state?.Text ?? "";

    public string StateDetail => _state?.Detail ?? "";

    public string UpdateNotice
    {
        get => _updateNotice;
        private set
        {
            if (_updateNotice == value)
                return;
            _updateNotice = value;
            OnPropertyChanged(nameof(UpdateNotice));
            OnPropertyChanged(nameof(HasUpdate));
        }
    }

    public bool HasUpdate => _updateNotice.Length > 0;

    public UiCommand? UpdateCommand { get; private set; }

    public UiCommand DismissUpdateCommand { get; }

    // Any thread.
    public void ShowUpdate(string notice, Action runSetup) => Dispatcher.UIThread.Post(() =>
    {
        UpdateCommand = new UiCommand(runSetup);
        OnPropertyChanged(nameof(UpdateCommand));
        UpdateNotice = notice;
    });

    private async void CopyPrompt()
    {
        if (EditorShell.MainWindow?.Clipboard is not { } clipboard)
        {
            Log.Default.WriteLine("[Veronica] No clipboard, connection prompt not copied.");
            return;
        }
        try
        {
            await clipboard.SetTextAsync(ConnectionPrompt);
        }
        catch (Exception e)
        {
            Log.Default.WriteLine($"[Veronica] Connection prompt not copied: {LocalModel.AsciiOnly(e.Message)}");
        }
    }
}
