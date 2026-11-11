using System.Text.Json;
using Veronica.Agent.Recording;

namespace Veronica.Agent.Eval;

public enum TurnOutcome
{
    Answered,
    Stopped,
    Failed,
}

// What a driving harness can ask of the chat. Send, Stop and Answer act on the same conversation the panel shows.
public interface IChatControl
{
    // Null when the message was taken, else why not.
    Task<string?> SendAsync(string text);

    void Stop();

    // One answer per open question, null skips it; null when taken, else why not.
    Task<string?> AnswerAsync(IReadOnlyList<string?> answers);
}

// An eval run's turn state and the chat it drives. The transcripts are the recording provider's.
public sealed class RunControl
{
    private readonly object _gate = new();
    private readonly IChatControl _chat;
    private readonly Func<string?>? _writeError;
    private readonly QuestionBoard? _board;
    private TaskCompletionSource _idle = Completed();
    private TaskCompletionSource _asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _busy;
    private int _turns;
    private TurnOutcome? _lastOutcome;
    private string? _lastError;
    private int? _context;

    public RunControl(IChatControl chat, Func<string?>? writeError = null, QuestionBoard? board = null)
    {
        _chat = chat;
        _writeError = writeError;
        _board = board;
        if (board != null)
            board.Changed += OnQuestionsChanged;
    }

    public IChatControl Chat => _chat;

    // Called by the chat, synchronously, when it takes a message.
    public void TurnStarted()
    {
        lock (_gate)
        {
            _busy = true;
            if (_idle.Task.IsCompleted)
                _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    // nextQueued: answers given while the turn wound down start the next turn at once, so the chat
    // never reports idle in between.
    public void TurnEnded(TurnOutcome outcome, string? error, bool nextQueued = false)
    {
        TaskCompletionSource idle;
        lock (_gate)
        {
            _busy = nextQueued;
            _turns++;
            _lastOutcome = outcome;
            _lastError = error;
            idle = _idle;
        }
        if (!nextQueued)
            idle.TrySetResult();
    }

    public ITurnHost Wrap(ITurnHost host) => new StatsHost(this, host);

    // A Claude turn stays running while its question card is open, so an open card ends the wait too.
    public async Task WaitIdleAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task idle, asked;
        lock (_gate)
        {
            idle = _idle.Task;
            asked = _asked.Task;
        }
        if (_board?.Questions.Count > 0)
            return;
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await Task.WhenAny(idle, asked, Task.Delay(timeout, timer.Token)).ConfigureAwait(false);
        timer.Cancel();
    }

    public string StatusJson()
    {
        var questions = _board?.Questions;
        lock (_gate)
        {
            using var buffer = new MemoryStream();
            using (var w = new Utf8JsonWriter(buffer))
            {
                w.WriteStartObject();
                w.WriteBoolean("busy", _busy);
                w.WriteNumber("turns", _turns);
                if (_lastOutcome is { } outcome)
                {
                    w.WriteStartObject("last");
                    w.WriteString("outcome", RecordingProvider.OutcomeName(outcome));
                    if (_lastError != null)
                        w.WriteString("error", _lastError);
                    w.WriteEndObject();
                }
                if (_context is { } context)
                    w.WriteNumber("context", context);
                if (questions != null)
                {
                    w.WriteStartArray("questions");
                    foreach (var question in questions)
                    {
                        w.WriteStartObject();
                        w.WriteString("question", question.Text);
                        w.WriteStartArray("options");
                        foreach (var option in question.Options)
                            w.WriteStringValue(option);
                        w.WriteEndArray();
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                if (_writeError?.Invoke() is { } error)
                    w.WriteString("transcript_error", error);
                w.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
    }

    private void OnQuestionsChanged()
    {
        if (_board!.Questions.Count == 0)
            return;
        TaskCompletionSource asked;
        lock (_gate)
        {
            asked = _asked;
            _asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        asked.TrySetResult();
    }

    private static TaskCompletionSource Completed()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        done.SetResult();
        return done;
    }

    private sealed class StatsHost(RunControl run, ITurnHost host) : ITurnHost
    {
        public void OnRoundStart() => host.OnRoundStart();

        public void OnText(string delta) => host.OnText(delta);

        public void OnReasoning(string delta) => host.OnReasoning(delta);

        public void OnRoundStats(RoundStats stats)
        {
            if (stats.ContextTokens is { } context)
                lock (run._gate)
                    run._context = context;
            host.OnRoundStats(stats);
        }

        public Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken) =>
            host.OnToolCallAsync(call, context, cancellationToken);

        public void OnNotice(string text) => host.OnNotice(text);

        public void AppendTranscript(ChatMessage message) => host.AppendTranscript(message);
    }
}
