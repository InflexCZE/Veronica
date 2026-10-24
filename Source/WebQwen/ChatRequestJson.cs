using System.Text;
using System.Text.Json;

namespace WebQwen;

// The OpenAI-shaped chat request wq_chat takes, and the tool-call deltas it streams back.
internal static class ChatRequestJson
{
    public static string Write(ChatRequest request)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteStartArray("messages");
            foreach (var m in request.Messages)
                WriteMessage(w, m);
            w.WriteEndArray();
            if (request.Tools is { Count: > 0 } tools)
            {
                w.WriteStartArray("tools");
                foreach (var t in tools)
                {
                    w.WriteStartObject();
                    w.WriteString("type", "function");
                    w.WriteStartObject("function");
                    w.WriteString("name", t.Name);
                    w.WriteString("description", t.Description);
                    w.WritePropertyName("parameters");
                    w.WriteRawValue(t.ParametersJson);
                    w.WriteEndObject();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            if (request.MaxTokens > 0) w.WriteNumber("max_tokens", request.MaxTokens);
            if (request.Temperature is { } temperature) w.WriteNumber("temperature", temperature);
            if (request.TopP is { } topP) w.WriteNumber("top_p", topP);
            if (request.Seed is >= 0) w.WriteNumber("seed", request.Seed.Value);
            w.WriteBoolean("enable_thinking", request.EnableThinking);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static void WriteMessage(Utf8JsonWriter w, ChatMessage m)
    {
        w.WriteStartObject();
        w.WriteString("role", m.Role);
        w.WriteString("content", m.Content);
        if (m.Reasoning != null) w.WriteString("reasoning_content", m.Reasoning);
        if (m.ToolCalls is { Count: > 0 } calls)
        {
            w.WriteStartArray("tool_calls");
            foreach (var c in calls)
            {
                w.WriteStartObject();
                w.WriteString("id", c.Id);
                w.WriteString("type", "function");
                w.WriteStartObject("function");
                w.WriteString("name", c.Name);
                w.WriteString("arguments", c.Arguments);
                w.WriteEndObject();
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        if (m.ToolCallId != null) w.WriteString("tool_call_id", m.ToolCallId);
        w.WriteEndObject();
    }

    public static EngineToolCall ReadToolCall(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new EngineToolCall(Text(root, "id"), Text(root, "name"), Text(root, "arguments"));
    }

    // The engine's arguments are normally a JSON string; anything else is kept as its JSON text.
    private static string Text(JsonElement root, string name) =>
        !root.TryGetProperty(name, out var v) || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? ""
        : v.ValueKind == JsonValueKind.String ? v.GetString()!
        : v.GetRawText();
}
