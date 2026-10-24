using System.Text.Json;
using WebQwen.Protocol;

namespace WebQwen;

public interface IChatEngine
{
    ChatStream Chat(ChatRequest request, CancellationToken cancellationToken);
}

// Serves the stdio protocol (Protocol.cs): loads the model at start, then runs
// one chat at a time while it keeps reading commands, so an interrupt reaches
// the running chat.
public sealed class Server(TextReader input, TextWriter output, Action<string>? log = null)
{
    private readonly Lock _writeGate = new();
    private readonly Lock _chatGate = new();
    private long? _runningId;
    private CancellationTokenSource? _runningCancel;
    private Task _running = Task.CompletedTask;

    // Returns the exit code: 0 when the input ends, 1 when the model fails to load.
    public async Task<int> RunAsync(Func<IProgress<LoadProgress>, Task<IChatEngine>> load, CancellationToken cancellationToken = default)
    {
        var loading = LoadAsync(load);
        var reading = ReadAsync(loading, cancellationToken);
        if (await Task.WhenAny(loading, reading) == loading && await loading == null)
        {
            StopRunningChat();
            return 1;
        }
        try
        {
            await reading;
        }
        catch (OperationCanceledException)
        {
        }
        StopRunningChat();
        await Task.WhenAny(_running, Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None));
        return 0;
    }

    private async Task<IChatEngine?> LoadAsync(Func<IProgress<LoadProgress>, Task<IChatEngine>> load)
    {
        string last = "";
        try
        {
            var engine = await load(new SyncProgress(p =>
            {
                if (p.Text == last) return;
                last = p.Text;
                Send(new ProgressEvent(p.Progress, p.Text));
            }));
            Send(new ReadyEvent());
            return engine;
        }
        catch (Exception e)
        {
            Send(new ErrorEvent(null, e.Message));
            return null;
        }
    }

    private async Task ReadAsync(Task<IChatEngine?> loading, CancellationToken cancellationToken)
    {
        while (await input.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0) continue;
            Command command;
            try
            {
                command = Wire.DecodeCommand(line);
            }
            catch (Exception e) when (e is JsonException or NotSupportedException)
            {
                log?.Invoke($"ignoring bad command: {e.Message}");
                continue;
            }
            switch (command)
            {
                case ChatCommand chat:
                    StartChat(chat, loading);
                    break;
                case InterruptCommand interrupt:
                    lock (_chatGate)
                    {
                        if (_runningId == interrupt.Id) _runningCancel!.Cancel();
                    }
                    break;
            }
        }
    }

    private void StartChat(ChatCommand chat, Task<IChatEngine?> loading)
    {
        lock (_chatGate)
        {
            if (_runningId != null)
            {
                Send(new ErrorEvent(chat.Id, "busy"));
                return;
            }
            _runningId = chat.Id;
            _runningCancel = new CancellationTokenSource();
            _running = RunChatAsync(chat, loading, _runningCancel.Token);
        }
    }

    private async Task RunChatAsync(ChatCommand chat, Task<IChatEngine?> loading, CancellationToken cancellationToken)
    {
        await Task.Yield();
        Event end;
        try
        {
            end = await ChatAsync(chat, loading, cancellationToken);
        }
        catch (Exception e)
        {
            end = new ErrorEvent(chat.Id, e.Message);
        }
        // The plugin may send its next chat as soon as it reads the end event, so the
        // chat must stop counting as running before that event goes out.
        CancellationTokenSource? cancel;
        lock (_chatGate)
        {
            _runningId = null;
            cancel = _runningCancel;
            _runningCancel = null;
        }
        cancel?.Dispose();
        Send(end);
    }

    private async Task<Event> ChatAsync(ChatCommand chat, Task<IChatEngine?> loading, CancellationToken cancellationToken)
    {
        var engine = await loading ?? throw new EngineException("the model failed to load");
        // An interrupt that lands before the engine starts this chat would
        // be dropped there (it only interrupts a running generation).
        if (cancellationToken.IsCancellationRequested)
            return new DoneEvent(chat.Id, "abort", 0, 0, 0, 0);
        var messages = chat.Messages.Select(m => new ChatMessage(
            m.Role,
            m.Content,
            m.Reasoning,
            m.ToolCalls?.Select(c => new EngineToolCall(c.Id, c.Name, c.Arguments)).ToList(),
            m.ToolCallId)).ToList();
        var tools = chat.Tools?.Select(t => new ToolSpec(t.Name, t.Description, t.Parameters.GetRawText())).ToList();
        var request = new ChatRequest(messages, chat.MaxTokens, EnableThinking: chat.Thinking, Tools: tools);
        var stream = engine.Chat(request, cancellationToken);
        await foreach (var delta in stream.WithCancellation(CancellationToken.None))
        {
            if (delta.ToolCall is { } call)
            {
                Send(new ToolCallEvent(chat.Id, new WireToolCall(call.Id, call.Name, call.Arguments)));
                continue;
            }
            string channel = delta.Channel == ChatChannel.Reasoning ? Channels.Reasoning : Channels.Content;
            Send(new DeltaEvent(chat.Id, channel, delta.Text));
        }
        var result = await stream.Completion;
        return new DoneEvent(chat.Id, result.FinishReason, result.PromptTokens, result.CompletionTokens,
            Finite(result.PrefillTokensPerSecond), Finite(result.DecodeTokensPerSecond));
    }

    private void StopRunningChat()
    {
        lock (_chatGate)
        {
            _runningCancel?.Cancel();
        }
    }

    // JSON has no NaN; the engine reports NaN when it measured no rate.
    private static double Finite(double value) => double.IsFinite(value) ? value : 0;

    private void Send(Event e)
    {
        string line = Wire.Encode(e);
        lock (_writeGate)
        {
            try
            {
                output.WriteLine(line);
                output.Flush();
            }
            catch (IOException)
            {
                // The reader is gone; the input ends next and the server stops.
            }
        }
    }
}

// Progress<T> posts to the thread pool, out of order; reports arrive in order here.
internal sealed class SyncProgress(Action<LoadProgress> report) : IProgress<LoadProgress>
{
    public void Report(LoadProgress value) => report(value);
}
