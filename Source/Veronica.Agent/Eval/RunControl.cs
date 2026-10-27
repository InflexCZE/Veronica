using System.Text.Json;

namespace Veronica.Agent.Eval;

public enum TurnOutcome
{
    Answered,
    Stopped,
    Failed,
}

// What a driving harness can ask of the chat. Send and Stop act on the same conversation the panel shows.
public interface IChatControl
{
    // Null when the message was taken, else why not.
    Task<string?> SendAsync(string text);

    void Stop();
}

// An eval run's turn state, the optional transcript, and the chat it drives.
public sealed class RunControl(IChatControl chat, RunTranscript? transcript) : IDisposable
{
    private readonly object _gate = new();
    private TaskCompletionSource _idle = Completed();
    private bool _busy;
    private int _turns;
    private TurnOutcome? _lastOutcome;
    private string? _lastError;
    private int? _context;

    public IChatControl Chat => chat;

    // Called by the chat, synchronously, when it takes a message.
    public void TurnStarted(string text)
    {
        lock (_gate)
        {
            _busy = true;
            _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        transcript?.WriteUser(text);
    }

    public void TurnEnded(TurnOutcome outcome, string? error)
    {
        TaskCompletionSource idle;
        int? context;
        lock (_gate)
        {
            _busy = false;
            _turns++;
            _lastOutcome = outcome;
            _lastError = error;
            idle = _idle;
            context = _context;
        }
        try
        {
            transcript?.WriteTurnEnd(outcome, error, context);
        }
        finally
        {
            idle.TrySetResult();
        }
    }

    public ITurnHost Wrap(ITurnHost host) => new StatsHost(this, transcript?.Wrap(host) ?? host);

    public async Task WaitIdleAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task idle;
        lock (_gate)
            idle = _idle.Task;
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await Task.WhenAny(idle, Task.Delay(timeout, timer.Token)).ConfigureAwait(false);
        timer.Cancel();
    }

    public string StatusJson()
    {
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
                    w.WriteString("outcome", RunTranscript.OutcomeName(outcome));
                    if (_lastError != null)
                        w.WriteString("error", _lastError);
                    w.WriteEndObject();
                }
                if (_context is { } context)
                    w.WriteNumber("context", context);
                if (transcript?.WriteError is { } writeError)
                    w.WriteString("transcript_error", writeError);
                w.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
    }

    public void Dispose() => transcript?.Dispose();

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
