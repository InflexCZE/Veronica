using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;
using Veronica.Agent;
using Veronica.Agent.WebQwen;
using WebQwen;
using Xunit;
using AgentMessage = Veronica.Agent.ChatMessage;

namespace Veronica.Tests;

// WebQwen.exe's server and the plugin's connection, talking over in-memory pipes with a
// scripted engine in place of the model.
public sealed class ProtocolTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Pipe _commands = new();
    private readonly Pipe _events = new();
    private readonly TaskCompletionSource<IChatEngine> _engine = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _log = [];
    private readonly Task<int> _server;
    private readonly WebQwenConnection _connection;
    private IProgress<LoadProgress>? _progress;

    public ProtocolTests()
    {
        var server = new Server(_commands.Reader, _events.Writer);
        _server = Task.Run(() => server.RunAsync(progress =>
        {
            _progress = progress;
            return _engine.Task;
        }));
        _connection = new WebQwenConnection(_events.Reader, _commands.Writer, line => { lock (_log) _log.Add(line); });
        _connection.Start();
    }

    public void Dispose()
    {
        _commands.Dispose();
        _events.Dispose();
    }

    private static readonly AgentMessage[] Hello = [new(ChatRole.System, "SYS"), new(ChatRole.User, "hi")];

    private static List<StreamDelta> Deltas(out Action<StreamDelta> onDelta)
    {
        var deltas = new List<StreamDelta>();
        onDelta = d => { lock (deltas) deltas.Add(d); };
        return deltas;
    }

    [Fact]
    public async Task Chat_streams_both_channels_and_ends_with_stats()
    {
        var engine = new FakeEngine(async (request, writer, ct) =>
        {
            await writer.WriteAsync(new ChatDelta(ChatChannel.Reasoning, "think"));
            await writer.WriteAsync(new ChatDelta(ChatChannel.Content, "Hel"));
            await writer.WriteAsync(new ChatDelta(ChatChannel.Content, "lo"));
            return new ChatResult(12, 3, 100.5, 40.25, "stop");
        });
        _engine.SetResult(engine);
        var deltas = Deltas(out var onDelta);

        var history = new AgentMessage[]
        {
            new(ChatRole.System, "SYS"),
            new(ChatRole.User, "q1"),
            new(ChatRole.Assistant, "a1") { Reasoning = "r1" },
            new(ChatRole.User, "q2"),
        };
        await _connection.ChatAsync(history, 4000, true, onDelta, CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(
            [new ReasoningDelta("think"), new TextDelta("Hel"), new TextDelta("lo"), new StatsDelta(new RoundStats(12, 3, 100.5, 40.25)), new DoneDelta("stop")],
            deltas);
        var request = Assert.Single(engine.Requests);
        Assert.Equal(
            [new("system", "SYS"), new("user", "q1"), new("assistant", "a1", "r1"), new WebQwen.ChatMessage("user", "q2")],
            request.Messages);
        Assert.Equal(4000, request.MaxTokens);
        Assert.True(request.EnableThinking);
    }

    [Fact]
    public async Task Thinking_off_reaches_the_engine()
    {
        var engine = new FakeEngine((_, _, _) => Task.FromResult(new ChatResult(1, 1, 1, 1, "stop")));
        _engine.SetResult(engine);

        await _connection.ChatAsync(Hello, 0, false, _ => { }, CancellationToken.None).WaitAsync(Timeout);

        Assert.False(Assert.Single(engine.Requests).EnableThinking);
    }

    [Fact]
    public async Task Load_progress_and_ready_reach_the_plugin()
    {
        var progress = new List<string>();
        _connection.Progress += (_, text) => { lock (progress) progress.Add(text); };
        await WaitUntil(() => _progress != null);

        _progress!.Report(new LoadProgress(0.5, "half"));
        Assert.False(_connection.Ready.IsCompleted);
        _engine.SetResult(new FakeEngine((_, _, _) => Task.FromResult(new ChatResult(1, 1, 1, 1, "stop"))));
        await _connection.Ready.WaitAsync(Timeout);

        lock (progress) Assert.Contains("half", progress);
    }

    [Fact]
    public async Task Cancelling_interrupts_and_keeps_the_partial_reply()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _engine.SetResult(new FakeEngine(async (_, writer, ct) =>
        {
            await writer.WriteAsync(new ChatDelta(ChatChannel.Content, "par"));
            started.SetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, ct).ContinueWith(_ => { });
            return new ChatResult(5, 1, 1, 1, "abort");
        }));
        var deltas = Deltas(out var onDelta);
        using var cancel = new CancellationTokenSource();

        var chat = _connection.ChatAsync(Hello, 0, true, onDelta, cancel.Token);
        await started.Task.WaitAsync(Timeout);
        cancel.Cancel();
        await chat.WaitAsync(Timeout);

        Assert.Equal(new TextDelta("par"), deltas[0]);
        Assert.Equal(new DoneDelta("abort"), deltas[^1]);
    }

    [Fact]
    public async Task Cancelling_before_ready_leaves_the_load_running()
    {
        using var cancel = new CancellationTokenSource();
        var chat = _connection.ChatAsync(Hello, 0, true, _ => { }, cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chat.WaitAsync(Timeout));

        _engine.SetResult(new FakeEngine((_, _, _) => Task.FromResult(new ChatResult(1, 1, 1, 1, "stop"))));
        var deltas = Deltas(out var onDelta);
        await _connection.ChatAsync(Hello, 0, true, onDelta, CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(new DoneDelta("stop"), deltas[^1]);
    }

    [Fact]
    public async Task A_failed_request_leaves_the_connection_usable()
    {
        int calls = 0;
        _engine.SetResult(new FakeEngine((_, _, _) =>
            Interlocked.Increment(ref calls) == 1
                ? throw new EngineException("context full")
                : Task.FromResult(new ChatResult(1, 1, 1, 1, "stop"))));

        var error = await Assert.ThrowsAsync<WebQwenException>(() => _connection.ChatAsync(Hello, 0, true, _ => { }, CancellationToken.None).WaitAsync(Timeout));
        Assert.Contains("context full", error.Message);

        var deltas = Deltas(out var onDelta);
        await _connection.ChatAsync(Hello, 0, true, onDelta, CancellationToken.None).WaitAsync(Timeout);
        Assert.Equal(new DoneDelta("stop"), deltas[^1]);
    }

    [Fact]
    public async Task A_failed_load_is_fatal()
    {
        _engine.SetException(new EngineException("no adapter"));

        Assert.Equal(1, await _server.WaitAsync(Timeout));
        var error = await Assert.ThrowsAsync<WebQwenClosedException>(() => _connection.ChatAsync(Hello, 0, true, _ => { }, CancellationToken.None).WaitAsync(Timeout));
        Assert.Contains("no adapter", error.Message);
        Assert.Contains("no adapter", await _connection.Closed.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Closing_stdin_stops_the_server_and_interrupts_the_chat()
    {
        var interrupted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _engine.SetResult(new FakeEngine(async (_, _, ct) =>
        {
            started.SetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, ct).ContinueWith(_ => { });
            interrupted.SetResult();
            return new ChatResult(1, 1, 1, 1, "abort");
        }));
        _ = _connection.ChatAsync(Hello, 0, true, _ => { }, CancellationToken.None);
        await started.Task.WaitAsync(Timeout);

        _commands.Writer.Close();

        Assert.Equal(0, await _server.WaitAsync(Timeout));
        await interrupted.Task.WaitAsync(Timeout);
    }

    [Fact]
    public async Task WebQwen_vanishing_mid_chat_fails_the_round()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _engine.SetResult(new FakeEngine(async (_, _, ct) =>
        {
            started.SetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, ct).ContinueWith(_ => { });
            return new ChatResult(1, 1, 1, 1, "abort");
        }));
        var chat = _connection.ChatAsync(Hello, 0, true, _ => { }, CancellationToken.None);
        await started.Task.WaitAsync(Timeout);

        _events.Writer.Close();

        await Assert.ThrowsAsync<WebQwenClosedException>(() => chat.WaitAsync(Timeout));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException();
            await Task.Delay(10);
        }
    }

    private sealed class FakeEngine(Func<ChatRequest, ChannelWriter<ChatDelta>, CancellationToken, Task<ChatResult>> script) : IChatEngine
    {
        public List<ChatRequest> Requests { get; } = [];

        public ChatStream Chat(ChatRequest request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add(request);
            var deltas = Channel.CreateUnbounded<ChatDelta>();
            var result = Task.Run(async () =>
            {
                try
                {
                    var r = await script(request, deltas.Writer, cancellationToken);
                    deltas.Writer.TryComplete();
                    return r;
                }
                catch (Exception e)
                {
                    deltas.Writer.TryComplete(e);
                    throw;
                }
            });
            return new ChatStream(deltas.Reader, result);
        }
    }

    // One direction of a stdio pipe: closing the writer is end of stream for the reader.
    private sealed class Pipe : IDisposable
    {
        private readonly AnonymousPipeServerStream _out = new(PipeDirection.Out);
        private readonly AnonymousPipeClientStream _in;

        public Pipe()
        {
            _in = new AnonymousPipeClientStream(PipeDirection.In, _out.ClientSafePipeHandle);
            Writer = new StreamWriter(_out, new UTF8Encoding(false)) { AutoFlush = true };
            Reader = new StreamReader(_in, Encoding.UTF8);
        }

        public StreamWriter Writer { get; }
        public StreamReader Reader { get; }

        public void Dispose()
        {
            Writer.Dispose();
            Reader.Dispose();
        }
    }
}
