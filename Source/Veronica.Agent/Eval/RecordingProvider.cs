using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Veronica.Agent.Eval;

// Records every session an eval run opens, each to a JSONL file of its own: the chat's to
// transcript.jsonl, a subagent's to <name>-<n>.jsonl, so the chat's file stays what the chat model saw.
public sealed class RecordingProvider(IProvider inner, string directory, string description) : IProvider
{
    // The tool call running now in this async flow: a session opened inside it is that call's child.
    private static readonly AsyncLocal<CallScope?> RunningCall = new();

    private readonly object _gate = new();
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private string? _writeError;
    private string? _lastCompaction;

    public int ContextWindow => inner.ContextWindow;

    public bool RunsSubagents => inner.RunsSubagents;

    // The first write failure in any file. Recording never fails the turn it records.
    public string? WriteError
    {
        get { lock (_gate) return _writeError; }
    }

    public IProviderSession OpenSession(string name)
    {
        string fileName;
        lock (_gate)
        {
            int count = _counts[name] = _counts.GetValueOrDefault(name) + 1;
            fileName = name == IProvider.ChatSession ? RunTranscript.FileName : $"{name}-{count}.jsonl";
            if (name == Compaction.SessionName)
                _lastCompaction = fileName;
        }
        var parent = RunningCall.Value;
        RunTranscript? transcript = null;
        try
        {
            transcript = new RunTranscript(directory, fileName, parent, Failed);
            parent?.Children.Add(fileName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Failed(e.Message);
        }
        var session = inner.OpenSession(name);
        return transcript == null ? session : new Session(session, transcript, description, LastCompaction);
    }

    private string? LastCompaction()
    {
        lock (_gate)
            return _lastCompaction;
    }

    private void Failed(string error)
    {
        lock (_gate)
            _writeError ??= error;
    }

    internal sealed record CallScope(string File, string CallId)
    {
        public List<string> Children { get; } = [];
    }

    private sealed class Session(IProviderSession inner, RunTranscript transcript, string description, Func<string?> lastCompaction) : IProviderSession
    {
        private string? _system;
        private IReadOnlyList<ToolDefinition>? _tools;

        public async Task RunTurnAsync(TurnRequest request, ITurnHost host, CancellationToken cancellationToken)
        {
            if (request.SystemPrompt != _system || !ReferenceEquals(request.Tools, _tools))
            {
                _system = request.SystemPrompt;
                _tools = request.Tools;
                transcript.WriteSetup(description, request.SystemPrompt, request.Tools);
            }
            if (request.Compacted && request.Transcript.Count > 0)
                transcript.WriteCompacted(request.Transcript[^1].Content, lastCompaction());
            else if (request.Transcript.Count > 0 && request.Transcript[^1].Role == ChatRole.User)
                transcript.WriteUser(request.Transcript[^1].Content);

            var recorded = transcript.Wrap(host);
            try
            {
                await inner.RunTurnAsync(request, recorded, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                transcript.WriteTurnEnd(cancellationToken.IsCancellationRequested ? TurnOutcome.Stopped : TurnOutcome.Failed, e.Message, recorded.Context);
                throw;
            }
            transcript.WriteTurnEnd(cancellationToken.IsCancellationRequested ? TurnOutcome.Stopped : TurnOutcome.Answered, null, recorded.Context);
        }

        // The next turn writes its setup again, so a cleared chat starts its record afresh.
        public Task ResetAsync()
        {
            transcript.WriteClear();
            _system = null;
            return inner.ResetAsync();
        }

        public void Dispose()
        {
            inner.Dispose();
            transcript.Dispose();
        }
    }

    // One session's file, appended as it happens, one whole message per line, so a hung or killed Editor
    // loses nothing.
    internal sealed class RunTranscript : IDisposable
    {
        public const string FileName = "transcript.jsonl";

        private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private readonly object _gate = new();
        private readonly FileStream _file;
        private readonly string _fileName;
        private readonly CallScope? _parent;
        private readonly Action<string> _failed;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private int _line;
        private int _round;

        public RunTranscript(string directory, string fileName, CallScope? parent, Action<string> failed)
        {
            Directory.CreateDirectory(directory);
            _file = new FileStream(Path.Combine(directory, fileName), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _fileName = fileName;
            _parent = parent;
            _failed = failed;
        }

        public void WriteSetup(string provider, string systemPrompt, IReadOnlyList<ToolDefinition> tools) => Write("setup", w =>
        {
            w.WriteString("started", DateTime.UtcNow.ToString("O"));
            w.WriteString("provider", provider);
            if (_parent != null)
            {
                w.WriteStartObject("parent");
                w.WriteString("file", _parent.File);
                w.WriteString("call", _parent.CallId);
                w.WriteEndObject();
            }
            w.WriteString("system", systemPrompt);
            w.WriteStartArray("tools");
            foreach (var tool in tools)
            {
                w.WriteStartObject();
                w.WriteString("name", tool.Name);
                w.WriteString("description", tool.Description);
                w.WritePropertyName("parameters");
                WriteJsonOrString(w, tool.Parameters);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });

        public void WriteUser(string text) => Write("user", w => w.WriteString("text", text));

        public void WriteClear() => Write("clear", _ => { });

        public void WriteCompacted(string text, string? session) => Write("compacted", w =>
        {
            w.WriteString("text", text);
            if (session != null)
                w.WriteString("session", session);
        });

        public void WriteTurnEnd(TurnOutcome outcome, string? error, int? contextTokens) => Write("turn_end", w =>
        {
            w.WriteString("outcome", OutcomeName(outcome));
            if (error != null)
                w.WriteString("error", error);
            if (contextTokens is { } context)
                w.WriteNumber("context", context);
        });

        public Host Wrap(ITurnHost host) => new(this, host);

        public void Dispose()
        {
            lock (_gate)
                _file.Dispose();
        }

        private void Write(string type, Action<Utf8JsonWriter> body)
        {
            lock (_gate)
            {
                try
                {
                    byte[] line;
                    try
                    {
                        line = Line(_line, type, body);
                    }
                    catch (Exception e) when (e is ArgumentException or InvalidOperationException)
                    {
                        // Text the writer refuses, e.g. a lone surrogate from the model.
                        line = Line(_line, type, w => w.WriteString("unwritable", e.Message));
                    }
                    _line++;
                    _file.Write(line);
                    _file.Flush();
                }
                catch (Exception e)
                {
                    _failed(e.Message);
                }
            }
        }

        private byte[] Line(int index, string type, Action<Utf8JsonWriter> body)
        {
            using var buffer = new MemoryStream();
            using (var w = new Utf8JsonWriter(buffer, WriterOptions))
            {
                w.WriteStartObject();
                w.WriteNumber("i", index);
                w.WriteNumber("t", Math.Round(_clock.Elapsed.TotalSeconds, 1));
                w.WriteString("type", type);
                body(w);
                w.WriteEndObject();
            }
            buffer.WriteByte((byte)'\n');
            return buffer.ToArray();
        }

        private static void WriteJsonOrString(Utf8JsonWriter w, string text)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                document.RootElement.WriteTo(w);
            }
            catch (JsonException)
            {
                w.WriteStringValue(text);
            }
        }

        internal sealed class Host(RunTranscript run, ITurnHost host) : ITurnHost
        {
            private readonly Dictionary<string, (long Ms, List<string> Sessions)> _calls = new(StringComparer.Ordinal);
            private RoundStats? _stats;

            public int? Context { get; private set; }

            public void OnRoundStart()
            {
                Interlocked.Increment(ref run._round);
                _stats = null;
                host.OnRoundStart();
            }

            public void OnText(string delta) => host.OnText(delta);

            public void OnReasoning(string delta) => host.OnReasoning(delta);

            public void OnRoundStats(RoundStats stats)
            {
                _stats = stats;
                if (stats.ContextTokens is { } context)
                    Context = context;
                host.OnRoundStats(stats);
            }

            public async Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken)
            {
                var clock = Stopwatch.StartNew();
                var scope = RunningCall.Value = new CallScope(run._fileName, call.Id);
                try
                {
                    return await host.OnToolCallAsync(call, context, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    lock (_calls)
                        _calls[call.Id] = (clock.ElapsedMilliseconds, scope.Children);
                }
            }

            public void OnNotice(string text)
            {
                run.Write("notice", w => w.WriteString("text", text));
                host.OnNotice(text);
            }

            public void AppendTranscript(ChatMessage message)
            {
                switch (message.Role)
                {
                    case ChatRole.Assistant:
                        WriteAssistant(message);
                        break;
                    case ChatRole.Tool:
                        WriteTool(message);
                        break;
                    default:
                        run.Write("nudge", w => w.WriteString("text", message.Content));
                        break;
                }
                host.AppendTranscript(message);
            }

            private void WriteAssistant(ChatMessage message)
            {
                var stats = _stats;
                run.Write("assistant", w =>
                {
                    w.WriteNumber("round", run._round);
                    w.WriteString("text", message.Content);
                    if (message.Reasoning != null)
                        w.WriteString("reasoning", message.Reasoning);
                    if (message.ToolCalls is { Count: > 0 } calls)
                    {
                        w.WriteStartArray("calls");
                        foreach (var call in calls)
                        {
                            w.WriteStartObject();
                            w.WriteString("id", call.Id);
                            w.WriteString("name", call.Name);
                            w.WritePropertyName("args");
                            WriteJsonOrString(w, call.Arguments);
                            w.WriteEndObject();
                        }
                        w.WriteEndArray();
                    }
                    if (stats != null)
                    {
                        w.WriteStartObject("stats");
                        w.WriteNumber("prompt", stats.PromptTokens);
                        if (stats.CachedTokens is { } cached)
                            w.WriteNumber("cached", cached);
                        w.WriteNumber("completion", stats.CompletionTokens);
                        if (stats.ContextTokens is { } context)
                            w.WriteNumber("context", context);
                        w.WriteNumber("prefill_tps", Math.Round(stats.PrefillTokensPerSecond, 1));
                        w.WriteNumber("decode_tps", Math.Round(stats.DecodeTokensPerSecond, 1));
                        w.WriteEndObject();
                    }
                });
            }

            private void WriteTool(ChatMessage message)
            {
                (long Ms, List<string> Sessions)? call = null;
                lock (_calls)
                {
                    if (message.ToolCallId != null && _calls.Remove(message.ToolCallId, out var found))
                        call = found;
                }
                run.Write("tool", w =>
                {
                    w.WriteString("id", message.ToolCallId);
                    w.WriteString("name", message.Name);
                    if (call is { } c)
                    {
                        w.WriteNumber("ms", c.Ms);
                        if (c.Sessions.Count > 0)
                        {
                            w.WriteStartArray("sessions");
                            foreach (var file in c.Sessions)
                                w.WriteStringValue(file);
                            w.WriteEndArray();
                        }
                    }
                    w.WriteString("result", message.Content);
                });
            }
        }
    }

    public static string OutcomeName(TurnOutcome outcome) => outcome switch
    {
        TurnOutcome.Answered => "answered",
        TurnOutcome.Stopped => "stopped",
        _ => "error",
    };
}
