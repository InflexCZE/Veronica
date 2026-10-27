using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Veronica.Agent.Claude;

internal sealed record ClaudeResult(string Subtype, bool IsError, string? Text);

// Turns claude -p's stream-json events (with partial messages) into host calls and transcript
// messages, one assistant message per model message. Text and thinking come from the deltas, so a
// stopped message keeps what was generated; tool calls come from the complete blocks.
//
// Claude Code runs a message's tools before the stream reports the message's final usage and its
// stop. Tool results are held until then, so a round reports its stats, then its assistant message,
// then its tool results, as the other providers do.
internal sealed class ClaudeStream(ITurnHost host, Action<ToolCall> toolCallSeen)
{
    private readonly Dictionary<string, string> _callNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _answered = new(StringComparer.Ordinal);
    private readonly List<ToolCall> _calls = [];
    private readonly List<ChatMessage> _heldResults = [];
    private Round? _round;
    private long _requested = Stopwatch.GetTimestamp();

    // True once the model was asked: from then on Claude Code has stored the conversation.
    public bool SawMessages { get; private set; }

    public ClaudeResult? Result { get; private set; }

    private sealed class Round(long requested)
    {
        public long Requested { get; } = requested;
        public long? FirstToken;
        public long LastToken;
        public int Input;
        public int CacheRead;
        public int CacheCreation;
        public int Output;
        public StringBuilder Text { get; } = new();
        public StringBuilder Reasoning { get; } = new();
        public List<ToolCall> Calls { get; } = [];
    }

    public void Read(string line)
    {
        if (!line.StartsWith('{'))
            return;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return;
        }
        using var _ = document;
        var root = document.RootElement;
        switch (String(root, "type"))
        {
            case "system" when String(root, "subtype") == "init":
                CheckTools(root);
                break;
            case "stream_event" when root.TryGetProperty("event", out var e) && e.ValueKind == JsonValueKind.Object:
                SawMessages = true;
                StreamEvent(e);
                break;
            case "assistant":
                SawMessages = true;
                ToolUses(root);
                break;
            case "user":
                SawMessages = true;
                ToolResults(root);
                break;
            case "result":
                Flush();
                Result = new ClaudeResult(
                    String(root, "subtype") ?? "",
                    root.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True,
                    String(root, "result") ?? Errors(root));
                break;
        }
    }

    // Calls left without a result (stopped, or Claude Code died) get one, so the transcript never
    // carries dangling calls.
    public void Finish()
    {
        Flush();
        foreach (var call in _calls)
        {
            if (_answered.Add(call.Id))
                host.AppendTranscript(new ChatMessage(ChatRole.Tool, ClientToolLoop.CancelledToolOutput) { ToolCallId = call.Id, Name = call.Name });
        }
    }

    private void CheckTools(JsonElement init)
    {
        if (!init.TryGetProperty("mcp_servers", out var servers) || servers.ValueKind != JsonValueKind.Array)
            return;
        foreach (var server in servers.EnumerateArray())
        {
            if (server.ValueKind == JsonValueKind.Object && String(server, "name") == McpServer.ServerName && String(server, "status") is { } status && status != "connected")
                host.OnNotice($"Claude Code could not reach Veronica's tools ({status}).");
        }
    }

    private void StreamEvent(JsonElement e)
    {
        switch (String(e, "type"))
        {
            case "message_start":
                Flush();
                host.OnRoundStart();
                _round = new Round(_requested);
                if (e.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object
                    && message.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                {
                    _round.Input = Int(usage, "input_tokens");
                    _round.CacheRead = Int(usage, "cache_read_input_tokens");
                    _round.CacheCreation = Int(usage, "cache_creation_input_tokens");
                }
                break;
            case "content_block_delta" when _round != null && e.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object:
                long now = Stopwatch.GetTimestamp();
                _round.FirstToken ??= now;
                _round.LastToken = now;
                switch (String(delta, "type"))
                {
                    case "text_delta" when String(delta, "text") is { Length: > 0 } text:
                        _round.Text.Append(text);
                        host.OnText(text);
                        break;
                    case "thinking_delta" when String(delta, "thinking") is { Length: > 0 } thinking:
                        _round.Reasoning.Append(thinking);
                        host.OnReasoning(thinking);
                        break;
                }
                break;
            case "message_delta" when _round != null && e.TryGetProperty("usage", out var final) && final.ValueKind == JsonValueKind.Object:
                _round.Output = Int(final, "output_tokens");
                break;
            case "message_stop" when _round != null:
                ReportStats(_round);
                Flush();
                break;
        }
    }

    private void ReportStats(Round round)
    {
        long first = round.FirstToken ?? round.LastToken;
        int prompt = round.Input + round.CacheCreation;
        double prefillSeconds = round.FirstToken == null ? 0 : Stopwatch.GetElapsedTime(round.Requested, first).TotalSeconds;
        double decodeSeconds = round.FirstToken == null ? 0 : Stopwatch.GetElapsedTime(first, round.LastToken).TotalSeconds;
        host.OnRoundStats(new RoundStats(
            prompt,
            round.Output,
            prefillSeconds > 0 ? prompt / prefillSeconds : 0,
            decodeSeconds > 0 ? round.Output / decodeSeconds : 0)
        {
            CachedTokens = round.CacheRead,
        });
    }

    private void ToolUses(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return;
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object || String(block, "type") != "tool_use" || String(block, "id") is not { } callId)
                continue;
            string name = String(block, "name") ?? "";
            if (name.StartsWith(McpServer.ToolNamePrefix, StringComparison.Ordinal))
                name = name[McpServer.ToolNamePrefix.Length..];
            string arguments = block.TryGetProperty("input", out var input) ? input.GetRawText() : "{}";
            var call = new ToolCall(callId, name, arguments);
            _round ??= new Round(_requested);
            _round.Calls.Add(call);
            _callNames[callId] = name;
            toolCallSeen(call);
        }
    }

    private void ToolResults(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return;
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object || String(block, "type") != "tool_result" || String(block, "tool_use_id") is not { } callId)
                continue;
            if (!_answered.Add(callId))
                continue;
            _heldResults.Add(new ChatMessage(ChatRole.Tool, ResultText(block)) { ToolCallId = callId, Name = _callNames.GetValueOrDefault(callId) });
        }
        // The next model request goes out once the tools are done.
        _requested = Stopwatch.GetTimestamp();
        if (_round == null)
            Flush();
    }

    private void Flush()
    {
        if (_round is { } round)
        {
            _round = null;
            if (round.Text.Length > 0 || round.Reasoning.Length > 0 || round.Calls.Count > 0)
            {
                host.AppendTranscript(new ChatMessage(ChatRole.Assistant, round.Text.ToString())
                {
                    Reasoning = round.Reasoning.Length > 0 ? round.Reasoning.ToString() : null,
                    ToolCalls = round.Calls.Count > 0 ? round.Calls : null,
                });
                _calls.AddRange(round.Calls);
            }
        }
        foreach (var result in _heldResults)
            host.AppendTranscript(result);
        _heldResults.Clear();
    }

    private static string ResultText(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var content))
            return "";
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? "";
        if (content.ValueKind != JsonValueKind.Array)
            return content.GetRawText();
        return string.Join("\n", content.EnumerateArray()
            .Where(part => part.ValueKind == JsonValueKind.Object && String(part, "type") == "text")
            .Select(part => String(part, "text") ?? ""));
    }

    private static string? Errors(JsonElement root) =>
        root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
            ? string.Join("; ", errors.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText()))
            : null;

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n) ? n : 0;
}
