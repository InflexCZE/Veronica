using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Veronica.Agent.Eval;

// Appends an eval run's conversation to a JSONL file as it happens, one whole message per line,
// so a hung or killed Editor loses nothing.
public sealed class RunTranscript : IDisposable
{
    public const string FileName = "transcript.jsonl";

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly object _gate = new();
    private readonly FileStream _file;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _line;
    private int _round;

    public RunTranscript(string directory, string provider, string systemPrompt, IReadOnlyList<ToolDefinition> tools)
    {
        Directory.CreateDirectory(directory);
        _file = new FileStream(Path.Combine(directory, FileName), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        Write("setup", w =>
        {
            w.WriteString("started", DateTime.UtcNow.ToString("O"));
            w.WriteString("provider", provider);
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
    }

    public void WriteUser(string text) => Write("user", w => w.WriteString("text", text));

    public void WriteTurnEnd(TurnOutcome outcome, string? error, int? contextTokens) => Write("turn_end", w =>
    {
        w.WriteString("outcome", OutcomeName(outcome));
        if (error != null)
            w.WriteString("error", error);
        if (contextTokens is { } context)
            w.WriteNumber("context", context);
    });

    public ITurnHost Wrap(ITurnHost host) => new Host(this, host);

    public void Dispose()
    {
        lock (_gate)
            _file.Dispose();
    }

    public static string OutcomeName(TurnOutcome outcome) => outcome switch
    {
        TurnOutcome.Answered => "answered",
        TurnOutcome.Stopped => "stopped",
        _ => "error",
    };

    // The first write failure, if any. Recording never fails the turn it records.
    public string? WriteError { get; private set; }

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
                WriteError ??= e.Message;
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

    private sealed class Host(RunTranscript run, ITurnHost host) : ITurnHost
    {
        private readonly Dictionary<string, long> _toolMs = new(StringComparer.Ordinal);
        private RoundStats? _stats;

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
            host.OnRoundStats(stats);
        }

        public async Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken)
        {
            var clock = Stopwatch.StartNew();
            try
            {
                return await host.OnToolCallAsync(call, context, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_toolMs)
                    _toolMs[call.Id] = clock.ElapsedMilliseconds;
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
            long? ms = null;
            lock (_toolMs)
            {
                if (message.ToolCallId != null && _toolMs.Remove(message.ToolCallId, out long elapsed))
                    ms = elapsed;
            }
            run.Write("tool", w =>
            {
                w.WriteString("id", message.ToolCallId);
                w.WriteString("name", message.Name);
                if (ms is { } elapsed)
                    w.WriteNumber("ms", elapsed);
                w.WriteString("result", message.Content);
            });
        }
    }
}
