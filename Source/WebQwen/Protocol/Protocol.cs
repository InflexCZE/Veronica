using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebQwen.Protocol;

// The stdio protocol between Veronica.dll and WebQwen.exe: one JSON object per
// line, commands on stdin, events on stdout. Compiled into both sides, so the
// types are internal to each.

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChatCommand), "chat")]
[JsonDerivedType(typeof(InterruptCommand), "interrupt")]
internal abstract record Command;

internal sealed record ChatCommand(
    long Id,
    IReadOnlyList<WireMessage> Messages,
    int MaxTokens,
    bool Thinking,
    IReadOnlyList<WireTool>? Tools = null) : Command;

internal sealed record InterruptCommand(long Id) : Command;

// Role "tool" carries a tool result and its ToolCallId.
internal sealed record WireMessage(
    string Role,
    string Content,
    string? Reasoning = null,
    IReadOnlyList<WireToolCall>? ToolCalls = null,
    string? ToolCallId = null);

// Parameters is a JSON Schema object.
internal sealed record WireTool(string Name, string Description, JsonElement Parameters);

// Arguments is the JSON text the model wrote. A call the engine could not parse has an
// empty name and the raw call text as its arguments.
internal sealed record WireToolCall(string Id, string Name, string Arguments);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ProgressEvent), "progress")]
[JsonDerivedType(typeof(ReadyEvent), "ready")]
[JsonDerivedType(typeof(DeltaEvent), "delta")]
[JsonDerivedType(typeof(ToolCallEvent), "toolCall")]
[JsonDerivedType(typeof(DoneEvent), "done")]
[JsonDerivedType(typeof(ErrorEvent), "error")]
internal abstract record Event;

internal sealed record ProgressEvent(double Progress, string Text) : Event;

internal sealed record ReadyEvent : Event;

internal sealed record DeltaEvent(long Id, string Channel, string Text) : Event;

// One finished tool call of the chat with this id, in the order the model wrote them.
internal sealed record ToolCallEvent(long Id, WireToolCall Call) : Event;

internal sealed record DoneEvent(
    long Id,
    string FinishReason,
    int PromptTokens,
    int CompletionTokens,
    double PrefillTokensPerS,
    double DecodeTokensPerS) : Event;

// Without an id the error is fatal and WebQwen.exe exits after sending it.
// Code, when set, is one of ErrorCodes.
internal sealed record ErrorEvent(long? Id, string Message, string? Code = null) : Event;

internal static class ErrorCodes
{
    // The chat's prompt does not fit in the context window.
    public const string ContextFull = "context_full";
}

internal static class Channels
{
    public const string Content = "content";
    public const string Reasoning = "reasoning";
}

internal static class Wire
{
    public static string Encode(Command command) => JsonSerializer.Serialize(command, ProtocolJson.Default.Command);

    public static string Encode(Event e) => JsonSerializer.Serialize(e, ProtocolJson.Default.Event);

    // Throw JsonException (or NotSupportedException for an unknown type) on bad input.
    public static Command DecodeCommand(string line) =>
        JsonSerializer.Deserialize(line, ProtocolJson.Default.Command) ?? throw new JsonException("null command");

    public static Event DecodeEvent(string line) =>
        JsonSerializer.Deserialize(line, ProtocolJson.Default.Event) ?? throw new JsonException("null event");
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(Command))]
[JsonSerializable(typeof(Event))]
internal sealed partial class ProtocolJson : JsonSerializerContext;
