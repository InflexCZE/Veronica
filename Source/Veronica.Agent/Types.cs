namespace Veronica.Agent;

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool,
}

public sealed record ToolCall(string Id, string Name, string Arguments);

public sealed record ChatMessage(ChatRole Role, string Content)
{
    // Thinking models must see their own reasoning from earlier replies: their chat template
    // renders an empty think block otherwise, after which the model stops closing </think>.
    // A local engine also reuses its KV cache only when each earlier reply comes back exactly
    // as it produced it.
    public string? Reasoning { get; init; }

    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    // Tool messages only.
    public string? ToolCallId { get; init; }

    public string? Name { get; init; }
}

// Parameters is a JSON Schema object, as JSON text.
public sealed record ToolDefinition(string Name, string Description, string Parameters);

public sealed record ToolResult(string Output, bool EarnedReward = false);

public sealed record ToolCallContext(bool LastRound);

// PromptTokens counts only the tokens actually prefilled, not those served from the KV cache.
// CachedTokens are the ones served from it, when the server reports them.
public sealed record RoundStats(int PromptTokens, int CompletionTokens, double PrefillTokensPerSecond, double DecodeTokensPerSecond)
{
    public int? CachedTokens { get; init; }

    public int? ContextTokens => CachedTokens + PromptTokens + CompletionTokens;
}

public sealed record LengthNudge(Func<string> Message, int? MaxNudges = null);

public sealed record EndNudge(Func<string, string?> Message, IReadOnlySet<string>? BookkeepingTools = null);

public sealed record TurnLimits
{
    public const int DefaultMaxRounds = 20;

    public int MaxRounds { get; init; } = DefaultMaxRounds;

    public IReadOnlySet<string> FreeRoundTools { get; init; } = new HashSet<string>();

    public LengthNudge? LengthNudge { get; init; }

    public EndNudge? EndNudge { get; init; }

    // A round that calls one of these runs its calls, then ends the turn without another round.
    public IReadOnlySet<string> TurnEndingTools { get; init; } = new HashSet<string>();

    public bool? Thinking { get; init; }

    public int? MaxTokens { get; init; }

    // Thinking tokens on top of MaxTokens, for providers that budget thinking apart from the answer.
    public int? ThinkingBudget { get; init; }
}

public sealed record TurnRequest(
    string SystemPrompt,
    IReadOnlyList<ChatMessage> Transcript,
    IReadOnlyList<ToolDefinition> Tools,
    TurnLimits Limits);

// Calls arrive in model order, on any thread.
public interface ITurnHost
{
    void OnRoundStart();

    void OnText(string delta);

    void OnReasoning(string delta);

    void OnRoundStats(RoundStats stats);

    // Null when the turn was cancelled while the tool ran.
    Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken);

    void OnNotice(string text);

    void AppendTranscript(ChatMessage message);
}

public interface IProviderSession : IDisposable
{
    Task RunTurnAsync(TurnRequest request, ITurnHost host, CancellationToken cancellationToken);

    Task ResetAsync();
}

public interface IProvider
{
    IProviderSession OpenSession();
}

public sealed record RoundRequest(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ToolDefinition> Tools,
    bool? Thinking,
    int? MaxTokens,
    int? ThinkingBudget = null);

public abstract record StreamDelta;

public sealed record TextDelta(string Text) : StreamDelta;

public sealed record ReasoningDelta(string Text) : StreamDelta;

// Fragments of one call share an index; each carries whatever part of id, name and arguments arrived.
public sealed record ToolCallDelta(int Index, string? Id, string? Name, string? ArgumentsDelta) : StreamDelta;

public sealed record StatsDelta(RoundStats Stats) : StreamDelta;

public sealed record DoneDelta(string? FinishReason) : StreamDelta;

// Cancellation stops generation; the round then completes normally with what was generated so far.
public delegate Task StreamRound(RoundRequest request, Action<StreamDelta> onDelta, CancellationToken cancellationToken);
