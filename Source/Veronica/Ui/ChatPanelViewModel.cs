using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using Keen.VRage.Editor.Services.Docking;
using Veronica.Agent;

namespace Veronica.Ui;

public sealed class ChatPanelViewModel : DockItemViewModel
{
    private const string ClearCommand = "/clear";

    private readonly Conversation _conversation;
    private string _draft = "";
    private string _status = "";
    private CancellationTokenSource? _turn;
    private Task? _turnTask;
    private bool _clearing;

    public ChatPanelViewModel(IProviderSession session)
    {
        Header = "Veronica";
        ContentTemplateResourceKey = "VeronicaChatPanel";
        CanClose = true;
        CanFloat = true;

        _conversation = new Conversation(session, Prompts.System, Prompts.ChatLimits);
        SendCommand = new UiCommand(SendOrStop);
        SubmitCommand = new UiCommand(Submit);
    }

    // Any thread.
    public void ShowStatus(string status) => Dispatcher.UIThread.Post(() => Status = status);

    // The dock item's content is built from a template with no data, so ChatView looks its view model
    // up here, the way the Editor's own panels use its view model registry.
    public static ChatPanelViewModel? Current { get; set; }

    public ObservableCollection<ChatItem> Messages { get; } = new();

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

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
                return;
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public bool IsBusy => _turn != null;

    public string SendLabel => IsBusy ? "Stop" : "Send";

    public UiCommand SendCommand { get; }

    // Enter only sends: stopping a turn takes the button.
    public UiCommand SubmitCommand { get; }

    private void SendOrStop()
    {
        if (_turn != null)
            _turn.Cancel();
        else
            Submit();
    }

    private void Submit()
    {
        if (_clearing)
            return;

        var text = Draft.Trim();
        if (text == ClearCommand)
        {
            Draft = "";
            _ = ClearAsync();
            return;
        }

        if (_turn != null || text.Length == 0)
            return;
        Draft = "";

        Messages.Add(new UserItem(text));
        _turnTask = RunTurnAsync(text);
    }

    private async Task RunTurnAsync(string text)
    {
        _turn = new CancellationTokenSource();
        BusyChanged();
        try
        {
            await _conversation.SendAsync(text, new UiHost(this), _turn.Token);
        }
        catch (Exception e)
        {
            Messages.Add(new NoticeItem(e.Message, true));
        }
        finally
        {
            // Not disposed: Stop can complete the turn inline, inside the token's own Cancel().
            _turn = null;
            BusyChanged();
        }
    }

    private async Task ClearAsync()
    {
        _clearing = true;
        try
        {
            _turn?.Cancel();
            if (_turnTask != null)
                await _turnTask;
            await _conversation.ClearAsync();
            Messages.Clear();
        }
        finally
        {
            _clearing = false;
        }
    }

    private void BusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(SendLabel));
    }

    // Turn callbacks arrive on worker threads, in order; posting keeps that order on the UI thread.
    private sealed class UiHost(ChatPanelViewModel panel) : ITurnHost
    {
        private AssistantItem? _reply;

        public void OnRoundStart() => Post(() => _reply = null);

        public void OnReasoning(string delta) => Post(() => Reply().AppendReasoning(delta));

        public void OnText(string delta) => Post(() => Reply().AppendText(delta));

        public void OnRoundStats(RoundStats stats) => Post(() => panel.Status = string.Format(CultureInfo.InvariantCulture,
            "{0} prompt tokens at {1:F0} tok/s, {2} generated at {3:F1} tok/s",
            stats.PromptTokens, stats.PrefillTokensPerSecond, stats.CompletionTokens, stats.DecodeTokensPerSecond));

        public void OnNotice(string text) => Post(() => panel.Messages.Add(new NoticeItem(text, false)));

        public void AppendTranscript(ChatMessage message)
        {
        }

        public Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken) =>
            Task.FromResult<ToolResult?>(new ToolResult($"Unknown tool {call.Name}: Veronica has no tools yet."));

        private AssistantItem Reply()
        {
            if (_reply == null)
            {
                _reply = new AssistantItem();
                panel.Messages.Add(_reply);
            }
            return _reply;
        }

        private static void Post(Action action) => Dispatcher.UIThread.Post(action);
    }
}
