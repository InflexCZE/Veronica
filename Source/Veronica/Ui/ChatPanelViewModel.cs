using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using Keen.VRage.Editor.Services.Docking;
using Veronica.Agent;
using Veronica.Agent.Eval;
using Veronica.Agent.Tools;

namespace Veronica.Ui;

public sealed class ChatPanelViewModel : DockItemViewModel
{
    private const string ClearCommand = "/clear";

    private readonly Conversation _conversation;
    private readonly ToolSet _tools;
    private readonly SkillPlan _plan;
    private string _draft = "";
    private string _status = "";
    private string _updateNotice = "";
    private CancellationTokenSource? _turn;
    private Task? _turnTask;
    private readonly IProvider _provider;
    private bool _clearing;
    private QuestionCard? _card;
    // Answers given before the turn that asked had wound down: sent when it has.
    private string? _queuedAnswers;

    public ChatPanelViewModel(IProvider provider, ToolSet tools, SkillPlan plan, Func<string> systemPrompt, WikiTool wiki, Compaction compaction, QuestionBoard questions)
    {
        Header = "Veronica";
        ContentTemplateResourceKey = "VeronicaChatPanel";
        CanClose = true;
        CanFloat = true;

        _provider = provider;
        _tools = tools;
        _plan = plan;
        Questions = questions;
        questions.Changed += () => Dispatcher.UIThread.Post(RefreshCard);
        _conversation = new Conversation(provider.OpenSession(IProvider.ChatSession), systemPrompt, Prompts.ChatLimits(plan), tools.Definitions, compaction);
        if (provider.RunsSubagents)
        {
            wiki.Attach(RunSubAgentAsync, () => _conversation.Transcript.ToList());
            compaction.Attach(RunSubAgentAsync);
        }
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

    // A newer Veronica release; empty hides the notice.
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

    // Any thread.
    public void ShowUpdate(string notice, Action runSetup) => Dispatcher.UIThread.Post(() =>
    {
        UpdateCommand = new UiCommand(runSetup);
        OnPropertyChanged(nameof(UpdateCommand));
        UpdateNotice = notice;
    });

    // Set when an eval run drives or records this chat.
    public RunControl? Run { get; set; }

    public QuestionBoard Questions { get; }

    // Shown after the messages while questions are open.
    public QuestionCard? Card
    {
        get => _card;
        private set
        {
            if (_card == value)
                return;
            _card = value;
            OnPropertyChanged(nameof(Card));
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
            StopTurn();
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
        Start(text);
    }

    // UI thread. Null when the message was taken, else why not.
    public string? TrySend(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return "The message is empty.";
        if (_clearing)
            return "The chat is being cleared.";
        if (_turn != null)
            return "A turn is running.";
        Start(text);
        return null;
    }

    // UI thread.
    public void StopTurn()
    {
        if (_turn is not { } turn)
            return;
        _queuedAnswers = null;
        // Cancelled first, so a blocked question reports a stop rather than a card the user closed.
        turn.Cancel();
        Questions.Close();
    }

    // UI thread. Null when the answers were taken, else why not.
    public string? AnswerQuestions(IReadOnlyList<string?> answers)
    {
        if (_clearing)
            return "The chat is being cleared.";
        var result = Questions.Answer(answers);
        if (result.Refused != null)
            return result.Refused;
        // Answers to a blocked call are part of the running Claude turn; to another MCP client's, not of the chat.
        if (result.Waited != null && _turn != null)
            Messages.Add(new UserItem(result.Waited));
        if (result.Unwaited != null)
        {
            if (_turn == null)
                Start(result.Unwaited);
            else
                _queuedAnswers = _queuedAnswers == null ? result.Unwaited : _queuedAnswers + "\n\n" + result.Unwaited;
        }
        return null;
    }

    // A message the user sends while questions are open is their reply instead: the card closes unanswered.
    private void Start(string text, bool closeQuestions = true)
    {
        if (closeQuestions)
            Questions.Close();
        _queuedAnswers = null;
        Messages.Add(new UserItem(text));
        _turnTask = RunTurnAsync((host, token) => _conversation.SendAsync(text, host, token));
    }

    // The Compact choice of a turn that ended at a full context.
    private void StartCompact()
    {
        if (_clearing || _turn != null)
            return;
        Questions.Close();
        _queuedAnswers = null;
        _turnTask = RunTurnAsync(_conversation.CompactAsync);
    }

    private void RefreshCard()
    {
        var (questions, card) = Questions.Snapshot();
        if (questions.Count == 0)
            Card = null;
        else if (Card == null || Card.Generation != card)
            Card = new QuestionCard(questions, card, answers => AnswerQuestions(answers), Questions.Close);
        else
            Card.Update(questions);
    }

    private async Task RunTurnAsync(Func<ITurnHost, CancellationToken, Task> send)
    {
        foreach (var full in Messages.OfType<ContextFullItem>())
            full.Close();
        var turn = _turn = new CancellationTokenSource();
        BusyChanged();
        var run = Run;
        var outcome = TurnOutcome.Answered;
        string? error = null;
        try
        {
            run?.TurnStarted();
            ITurnHost host = new UiHost(this);
            await send(run?.Wrap(host) ?? host, turn.Token);
            if (turn.IsCancellationRequested)
                outcome = TurnOutcome.Stopped;
        }
        catch (Exception e)
        {
            outcome = turn.IsCancellationRequested ? TurnOutcome.Stopped : TurnOutcome.Failed;
            error = e.Message;
            if (e is ContextFullTurnException full && !turn.IsCancellationRequested)
                Messages.Add(new ContextFullItem(full.Message, full.CanCompact ? StartCompact : null, () => _ = ClearAsync()));
            else
                Messages.Add(new NoticeItem(e.Message, true));
        }
        finally
        {
            // Not disposed: Stop can complete the turn inline, inside the token's own Cancel().
            _turn = null;
            BusyChanged();
            run?.TurnEnded(outcome, error, nextQueued: _queuedAnswers != null && !_clearing);
        }
        // Questions asked after these answers stay open for the user.
        if (_queuedAnswers is { } answers && !_clearing)
            Start(answers, closeQuestions: false);
    }

    private async Task ClearAsync()
    {
        _clearing = true;
        try
        {
            _queuedAnswers = null;
            _turn?.Cancel();
            Questions.Close();
            if (_turnTask != null)
                await _turnTask;
            await _conversation.ClearAsync();
            _plan.Reset();
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

        public async Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken)
        {
            var activity = new ToolActivity(call.Name.Length > 0 ? $"{call.Name} {call.Arguments}" : "unreadable tool call");
            Post(() => Reply().Tools.Add(activity));
            try
            {
                var result = await panel._tools.ExecuteAsync(call, cancellationToken).ConfigureAwait(false);
                Post(() => activity.Complete(result.Output));
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Post(() => activity.Complete("stopped"));
                return null;
            }
        }

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

    // A subagent gets a card of its own in the chat: its steps as it works, then its answer.
    private async Task<SubAgentResult> RunSubAgentAsync(SubAgentRequest request, CancellationToken cancellationToken)
    {
        var card = new SubAgentItem(request.Title);
        Dispatcher.UIThread.Post(() => Messages.Add(card));
        try
        {
            var result = await SubAgent.RunAsync(_provider, request, host => new CardHost(this, card, host), cancellationToken).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() => card.Finish(result.Text ?? "(no answer)", result.RoundCap ? "ran out of rounds" : "finished"));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Dispatcher.UIThread.Post(() => card.Finish("", "stopped"));
            throw;
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(() => card.Finish("", "failed: " + e.Message));
            throw;
        }
    }

    private sealed class CardHost(ChatPanelViewModel panel, SubAgentItem card, ITurnHost host) : ITurnHost
    {
        public void OnRoundStart() => host.OnRoundStart();

        public void OnText(string delta) => host.OnText(delta);

        public void OnReasoning(string delta) => host.OnReasoning(delta);

        public void OnRoundStats(RoundStats stats)
        {
            Dispatcher.UIThread.Post(() => panel.Status = string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} prompt tokens at {2:F0} tok/s, {3} generated at {4:F1} tok/s",
                card.Title, stats.PromptTokens, stats.PrefillTokensPerSecond, stats.CompletionTokens, stats.DecodeTokensPerSecond));
            host.OnRoundStats(stats);
        }

        public Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken)
        {
            Dispatcher.UIThread.Post(() => card.Steps.Add($"{call.Name} {call.Arguments}"));
            return host.OnToolCallAsync(call, context, cancellationToken);
        }

        public void OnNotice(string text) => host.OnNotice(text);

        public void AppendTranscript(ChatMessage message) => host.AppendTranscript(message);
    }
}
