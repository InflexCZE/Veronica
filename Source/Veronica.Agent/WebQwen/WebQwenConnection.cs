using System.Text.Json;
using WebQwen.Protocol;

namespace Veronica.Agent.WebQwen;

public sealed class WebQwenException(string message) : Exception(message);

// Thrown when WebQwen.exe is gone: it exited, crashed or broke the protocol.
public sealed class WebQwenClosedException(string message) : Exception(message);

// The plugin's end of the stdio protocol. Events are read on a background loop; requests
// run one at a time.
public sealed class WebQwenConnection
{
    private readonly TextReader _events;
    private readonly TextWriter _commands;
    private readonly Action<string> _log;
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<string> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Pending? _pending;
    private long _nextId;

    public WebQwenConnection(TextReader events, TextWriter commands, Action<string> log)
    {
        _events = events;
        _commands = commands;
        _log = log;
        _ready.Task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    // Load progress (0..1) and its text, on the reader thread.
    public event Action<double, string>? Progress;

    public Task Ready => _ready.Task;

    // Completes with the reason once the event stream has ended.
    public Task<string> Closed => _closed.Task;

    public void Start() => _ = Task.Run(ReadLoopAsync);

    public async Task ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        int maxTokens,
        bool thinking,
        Action<StreamDelta> onDelta,
        CancellationToken cancellationToken)
    {
        var wireTools = tools.Count > 0 ? tools.Select(ToWire).ToList() : null;
        var wireMessages = messages.Select(ToWire).ToList();
        await Ready.WaitAsync(cancellationToken).ConfigureAwait(false);

        var pending = new Pending(Interlocked.Increment(ref _nextId), onDelta, maxTokens);
        lock (_gate)
        {
            if (_closed.Task.IsCompleted)
                throw new WebQwenClosedException(_closed.Task.Result);
            if (_pending != null)
                throw new InvalidOperationException("A WebQwen request is already running.");
            _pending = pending;
        }
        try
        {
            var command = new ChatCommand(pending.Id, wireMessages, maxTokens, thinking, wireTools);
            await SendAsync(command).ConfigureAwait(false);
            using (cancellationToken.Register(() => _ = SendAsync(new InterruptCommand(pending.Id))))
                await pending.Done.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                if (_pending == pending)
                    _pending = null;
            }
        }
    }

    private static WireMessage ToWire(ChatMessage message)
    {
        string role = message.Role switch
        {
            ChatRole.System => "system",
            ChatRole.User => "user",
            ChatRole.Assistant => "assistant",
            ChatRole.Tool => "tool",
            _ => throw new ArgumentOutOfRangeException(nameof(message)),
        };
        var calls = message.ToolCalls?.Select(c => new WireToolCall(c.Id, c.Name, c.Arguments)).ToList();
        return new WireMessage(role, message.Content, message.Reasoning, calls, message.ToolCallId);
    }

    private static WireTool ToWire(ToolDefinition tool)
    {
        using var schema = JsonDocument.Parse(tool.Parameters);
        return new WireTool(tool.Name, tool.Description, schema.RootElement.Clone());
    }

    private async Task SendAsync(Command command)
    {
        string line = Wire.Encode(command);
        try
        {
            // TextWriter is not thread-safe; an interrupt can race the chat command.
            await _commandLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _commands.WriteLineAsync(line).ConfigureAwait(false);
                await _commands.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                _commandLock.Release();
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            Close($"WebQwen stopped accepting commands: {e.Message}");
        }
    }

    private readonly SemaphoreSlim _commandLock = new(1, 1);

    private async Task ReadLoopAsync()
    {
        string reason = "WebQwen exited.";
        try
        {
            while (await _events.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length == 0)
                    continue;
                Event e;
                try
                {
                    e = Wire.DecodeEvent(line);
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException)
                {
                    reason = $"WebQwen sent an unreadable event: {ex.Message}";
                    break;
                }
                if (e is ErrorEvent { Id: null } fatal)
                {
                    reason = fatal.Message;
                    break;
                }
                Dispatch(e);
            }
        }
        catch (Exception e)
        {
            reason = $"Reading from WebQwen failed: {e.Message}";
        }
        Close(reason);
    }

    private void Dispatch(Event e)
    {
        switch (e)
        {
            case ProgressEvent p:
                Progress?.Invoke(p.Progress, p.Text);
                break;
            case ReadyEvent:
                _ready.TrySetResult();
                break;
            case DeltaEvent d when Current(d.Id) is { } pending:
                pending.OnDelta(d.Channel == Channels.Reasoning ? new ReasoningDelta(d.Text) : new TextDelta(d.Text));
                break;
            case ToolCallEvent t when Current(t.Id) is { } pending:
                pending.OnDelta(new ToolCallDelta(pending.ToolCalls++, t.Call.Id, t.Call.Name, t.Call.Arguments));
                break;
            case DoneEvent d when Current(d.Id) is { } pending:
                pending.OnDelta(new StatsDelta(new RoundStats(d.PromptTokens, d.CompletionTokens, d.PrefillTokensPerS, d.DecodeTokensPerS)));
                pending.OnDelta(new DoneDelta(d.FinishReason, ContextFullException.IsReplyWall(d.FinishReason, d.CompletionTokens, pending.MaxTokens)));
                pending.Done.TrySetResult();
                break;
            case ErrorEvent { Id: { } id } error when Current(id) is { } pending:
                pending.Done.TrySetException(error.Code == ErrorCodes.ContextFull
                    ? new ContextFullException(ContextFullPhase.Prompt, error.Message)
                    : new WebQwenException(error.Message));
                break;
            default:
                _log($"Ignored WebQwen event: {Wire.Encode(e)}");
                break;
        }
    }

    private Pending? Current(long id)
    {
        lock (_gate)
            return _pending?.Id == id ? _pending : null;
    }

    private void Close(string reason)
    {
        Pending? pending;
        lock (_gate)
        {
            if (!_closed.TrySetResult(reason))
                return;
            pending = _pending;
        }
        _ready.TrySetException(new WebQwenClosedException(reason));
        pending?.Done.TrySetException(new WebQwenClosedException(reason));
    }

    private sealed class Pending(long id, Action<StreamDelta> onDelta, int maxTokens)
    {
        public long Id { get; } = id;
        public Action<StreamDelta> OnDelta { get; } = onDelta;
        public int MaxTokens { get; } = maxTokens;
        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Read-loop thread only.
        public int ToolCalls { get; set; }
    }
}
