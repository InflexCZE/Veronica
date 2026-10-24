using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using WebQwen.Interop;

namespace WebQwen;

public enum LogLevel
{
    Debug = Wq.WQ_LOG_DEBUG,
    Info = Wq.WQ_LOG_INFO,
    Warn = Wq.WQ_LOG_WARN,
    Error = Wq.WQ_LOG_ERROR,
}

public enum ChatChannel
{
    Content = Wq.WQ_CHANNEL_CONTENT,
    Reasoning = Wq.WQ_CHANNEL_REASONING,
    ToolCall = Wq.WQ_CHANNEL_TOOL_CALL,
}

public sealed record ModelSpec(
    string ModelId,
    string ModelUrl,
    string LibUrl,
    string LibSha256,
    IReadOnlyList<string> RequiredFeatures,
    int ContextWindowSize = 0);

public sealed record LoadProgress(double Progress, string Text);

// Arguments is the JSON text the model wrote. A call the engine could not parse
// has an empty name and the raw call text as its arguments.
public sealed record EngineToolCall(string Id, string Name, string Arguments);

public sealed record ToolSpec(string Name, string Description, string ParametersJson);

// Reasoning: the think block that preceded an assistant reply's content.
// ToolCallId: role "tool" only, the call this message answers.
public sealed record ChatMessage(
    string Role,
    string Content,
    string? Reasoning = null,
    IReadOnlyList<EngineToolCall>? ToolCalls = null,
    string? ToolCallId = null);

public sealed record ChatRequest(
    IReadOnlyList<ChatMessage> Messages,
    int MaxTokens = 0,
    double? Temperature = null,
    double? TopP = null,
    long? Seed = null,
    bool EnableThinking = true,
    IReadOnlyList<ToolSpec>? Tools = null);

// A ToolCall delta has an empty Text and carries the whole call.
public readonly record struct ChatDelta(ChatChannel Channel, string Text, EngineToolCall? ToolCall = null);

public sealed record ChatResult(
    int PromptTokens,
    int CompletionTokens,
    double PrefillTokensPerSecond,
    double DecodeTokensPerSecond,
    string FinishReason);

public sealed record TokenScores(int Argmax, int VocabSize, IReadOnlyList<(int Id, double Logprob)> Top);

public sealed class EngineException(string message) : Exception(message);

// Serves the resources the engine fetches, keyed by URL. Called on the
// engine thread.
public interface IResourceProvider
{
    // Size in bytes, or -1 if the resource is not available.
    long Size(string url);

    // Fills destination from offset; returns the bytes read, or -1.
    long Read(string url, long offset, Span<byte> destination);
}

// The WebQwen engine. Operations run in order on the engine's own thread.
public sealed unsafe class Engine : IChatEngine, IDisposable
{
    private readonly IResourceProvider _resources;
    private readonly Action<LogLevel, string>? _log;
    // Guards _handle against Interrupt, which cancellation calls from any thread.
    private readonly Lock _gate = new();
    private GCHandle _self;
    private nint _handle;

    private Engine(IResourceProvider resources, Action<LogLevel, string>? log)
    {
        _resources = resources;
        _log = log;
    }

    // Starts the engine thread and evaluates the engine; blocks until it is ready.
    public static Engine Create(IResourceProvider resources, Action<LogLevel, string>? log = null)
    {
        var engine = new Engine(resources, log);
        engine._self = GCHandle.Alloc(engine);
        var host = new wq_host
        {
            ctx = (void*)GCHandle.ToIntPtr(engine._self),
            log = &OnLog,
            resource_size = &OnResourceSize,
            resource_read = &OnResourceRead,
        };
        byte* error = null;
        engine._handle = Wq.wq_create(&host, &error);
        if (engine._handle == 0)
        {
            string message = TakeString(error) ?? "wq_create failed";
            engine._self.Free();
            throw new EngineException(message);
        }
        return engine;
    }

    public void Dispose()
    {
        nint handle;
        lock (_gate)
        {
            handle = _handle;
            _handle = 0;
        }
        if (handle == 0) return;
        Wq.wq_destroy(handle);
        _self.Free();
    }

    public Task LoadModelAsync(ModelSpec model, IProgress<LoadProgress>? progress = null)
    {
        var op = new LoadOp(progress);
        using var strings = new Utf8Strings();
        var features = stackalloc byte*[model.RequiredFeatures.Count];
        for (int i = 0; i < model.RequiredFeatures.Count; i++) features[i] = strings.Add(model.RequiredFeatures[i]);
        var m = new wq_model
        {
            model_id = strings.Add(model.ModelId),
            model_url = strings.Add(model.ModelUrl),
            lib_url = strings.Add(model.LibUrl),
            lib_sha256 = strings.Add(model.LibSha256),
            required_features = features,
            required_feature_count = (nuint)model.RequiredFeatures.Count,
            context_window_size = model.ContextWindowSize,
        };
        Wq.wq_load_model(Handle, &m, &OnProgress, &OnDone, op.Pin());
        return op.Task;
    }

    // Streams the reply; cancelling interrupts generation, and the stream then
    // ends with what was generated so far.
    public ChatStream Chat(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var op = new ChatOp();
        using var strings = new Utf8Strings();
        Wq.wq_chat(Handle, strings.Add(ChatRequestJson.Write(request)), &OnDelta, &OnChatDone, op.Pin());
        if (cancellationToken.CanBeCanceled)
        {
            var registration = cancellationToken.Register(Interrupt);
            op.Result.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
        }
        return new ChatStream(op.Deltas.Reader, op.Result);
    }

    public void Interrupt()
    {
        lock (_gate)
        {
            if (_handle != 0) Wq.wq_interrupt(_handle);
        }
    }

    public Task ResetAsync()
    {
        var op = new LoadOp(null);
        Wq.wq_reset(Handle, &OnDone, op.Pin());
        return op.Task;
    }

    // Feeds tokens without sampling and returns the top_k log-probabilities of the next token.
    public Task<TokenScores> ScoreAsync(ReadOnlySpan<int> tokens, int topK)
    {
        var op = new ScoreOp();
        fixed (int* p = tokens) Wq.wq_score(Handle, p, (nuint)tokens.Length, topK, &OnScore, op.Pin());
        return op.Task;
    }

    private nint Handle => _handle != 0 ? _handle : throw new ObjectDisposedException(nameof(Engine));

    // ---- native callbacks, all on the engine thread ------------------------

    private abstract class Op
    {
        private GCHandle _handle;

        public void* Pin()
        {
            _handle = GCHandle.Alloc(this);
            return (void*)GCHandle.ToIntPtr(_handle);
        }

        public static T Get<T>(void* user) where T : Op => (T)GCHandle.FromIntPtr((nint)user).Target!;

        // The final callback of an operation releases it.
        public static T Take<T>(void* user) where T : Op
        {
            var handle = GCHandle.FromIntPtr((nint)user);
            var op = (T)handle.Target!;
            handle.Free();
            return op;
        }
    }

    private sealed class LoadOp(IProgress<LoadProgress>? progress) : Op
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IProgress<LoadProgress>? Progress { get; } = progress;
        public Task Task => _tcs.Task;
        public void Complete(string? error)
        {
            if (error == null) _tcs.TrySetResult();
            else _tcs.TrySetException(new EngineException(error));
        }
    }

    private sealed class ChatOp : Op
    {
        private readonly TaskCompletionSource<ChatResult> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Channel<ChatDelta> Deltas { get; } = Channel.CreateUnbounded<ChatDelta>(new UnboundedChannelOptions { SingleWriter = true });
        public Task<ChatResult> Result => _tcs.Task;
        public void Complete(ChatResult? result, string? error)
        {
            if (result != null)
            {
                Deltas.Writer.TryComplete();
                _tcs.TrySetResult(result);
            }
            else
            {
                var e = new EngineException(error ?? "chat failed");
                Deltas.Writer.TryComplete(e);
                _tcs.TrySetException(e);
            }
        }
    }

    private sealed class ScoreOp : Op
    {
        private readonly TaskCompletionSource<TokenScores> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<TokenScores> Task => _tcs.Task;
        public void Complete(TokenScores? scores, string? error)
        {
            if (scores != null) _tcs.TrySetResult(scores);
            else _tcs.TrySetException(new EngineException(error ?? "score failed"));
        }
    }

    private static Engine Self(void* ctx) => (Engine)GCHandle.FromIntPtr((nint)ctx).Target!;

    [UnmanagedCallersOnly]
    private static void OnLog(void* ctx, int level, byte* text, nuint length)
    {
        try
        {
            Self(ctx)._log?.Invoke((LogLevel)level, Encoding.UTF8.GetString(text, (int)length));
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly]
    private static long OnResourceSize(void* ctx, byte* url)
    {
        try
        {
            return Self(ctx)._resources.Size(Marshal.PtrToStringUTF8((nint)url)!);
        }
        catch
        {
            return -1;
        }
    }

    [UnmanagedCallersOnly]
    private static long OnResourceRead(void* ctx, byte* url, long offset, long length, void* destination)
    {
        try
        {
            var resources = Self(ctx)._resources;
            string u = Marshal.PtrToStringUTF8((nint)url)!;
            long total = 0;
            // Spans are int-sized, so large reads go in pieces.
            while (total < length)
            {
                int piece = (int)Math.Min(length - total, 1 << 30);
                long read = resources.Read(u, offset + total, new Span<byte>((byte*)destination + total, piece));
                if (read <= 0) return read < 0 && total == 0 ? -1 : total;
                total += read;
            }
            return total;
        }
        catch
        {
            return -1;
        }
    }

    [UnmanagedCallersOnly]
    private static void OnProgress(void* user, double progress, byte* text)
    {
        try
        {
            Op.Get<LoadOp>(user).Progress?.Report(new LoadProgress(progress, Marshal.PtrToStringUTF8((nint)text) ?? ""));
        }
        catch
        {
        }
    }

    // An exception escaping an UnmanagedCallersOnly method kills the process,
    // so the final callbacks fail their operation instead.

    [UnmanagedCallersOnly]
    private static void OnDone(void* user, int ok, byte* error)
    {
        var op = Op.Take<LoadOp>(user);
        try
        {
            op.Complete(ok != 0 ? null : Marshal.PtrToStringUTF8((nint)error) ?? "failed");
        }
        catch (Exception e)
        {
            op.Complete(e.Message);
        }
    }

    [UnmanagedCallersOnly]
    private static void OnDelta(void* user, int channel, byte* text, nuint length)
    {
        try
        {
            string s = Encoding.UTF8.GetString(text, checked((int)length));
            var delta = channel == Wq.WQ_CHANNEL_TOOL_CALL
                ? new ChatDelta(ChatChannel.ToolCall, "", ChatRequestJson.ReadToolCall(s))
                : new ChatDelta((ChatChannel)channel, s);
            Op.Get<ChatOp>(user).Deltas.Writer.TryWrite(delta);
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly]
    private static void OnChatDone(void* user, int ok, byte* error, wq_usage* usage, byte* finishReason)
    {
        var op = Op.Take<ChatOp>(user);
        try
        {
            if (ok == 0)
            {
                op.Complete(null, Marshal.PtrToStringUTF8((nint)error));
                return;
            }
            op.Complete(new ChatResult(usage->prompt_tokens, usage->completion_tokens, usage->prefill_tokens_per_s,
                usage->decode_tokens_per_s, Marshal.PtrToStringUTF8((nint)finishReason) ?? ""), null);
        }
        catch (Exception e)
        {
            op.Complete(null, e.Message);
        }
    }

    [UnmanagedCallersOnly]
    private static void OnScore(void* user, int ok, byte* error, wq_scores* scores)
    {
        var op = Op.Take<ScoreOp>(user);
        try
        {
            if (ok == 0)
            {
                op.Complete(null, Marshal.PtrToStringUTF8((nint)error));
                return;
            }
            var top = new (int, double)[checked((int)scores->top_count)];
            for (int i = 0; i < top.Length; i++) top[i] = (scores->top_ids[i], scores->top_logprobs[i]);
            op.Complete(new TokenScores(scores->argmax, scores->vocab_size, top), null);
        }
        catch (Exception e)
        {
            op.Complete(null, e.Message);
        }
    }

    private static string? TakeString(byte* p)
    {
        if (p == null) return null;
        string s = Marshal.PtrToStringUTF8((nint)p)!;
        Wq.wq_free(p);
        return s;
    }

    // UTF-8 copies of managed strings that live until disposed; wq copies its
    // inputs before a call returns.
    private sealed class Utf8Strings : IDisposable
    {
        private readonly List<nint> _allocations = [];

        public byte* Add(string s)
        {
            nint p = Marshal.StringToCoTaskMemUTF8(s);
            _allocations.Add(p);
            return (byte*)p;
        }

        public void Dispose()
        {
            foreach (nint p in _allocations) Marshal.FreeCoTaskMem(p);
        }
    }
}

public sealed class ChatStream(ChannelReader<ChatDelta> deltas, Task<ChatResult> completion) : IAsyncEnumerable<ChatDelta>
{
    // Completes when generation ends; faults if the request failed.
    public Task<ChatResult> Completion { get; } = completion;

    public async IAsyncEnumerator<ChatDelta> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var delta in deltas.ReadAllAsync(cancellationToken)) yield return delta;
    }
}
