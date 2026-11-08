using Veronica.Agent.Tools;

namespace Veronica.Agent;

// One self-contained turn on a session of its own: its own system prompt, one brief as the only user
// message, its own tools and limits, and its last text as the result. Its transcript dies with the
// session; the caller decides what of the answer the chat sees.
// Once the prompt grows past the context window less ContextReserve, tool calls are not run but answered
// with ContextFullOutput, so room for the answer is always left.
// CallReminder, when set, is appended once to the result of its AfterCalls-th tool call.
public sealed record SubAgentRequest(
    string Name,
    string Title,
    string SystemPrompt,
    string Brief,
    IReadOnlyList<ITool> Tools,
    TurnLimits Limits,
    int ContextReserve,
    string ContextFullOutput,
    CallReminder? CallReminder = null);

public sealed record CallReminder(int AfterCalls, string Text);

// Text is the last thing it wrote: its answer, or what it had when the rounds ran out. Null when it wrote nothing.
public sealed record SubAgentResult(string? Text, bool RoundCap);

// Supplied by the chat, which shows the run.
public delegate Task<SubAgentResult> SubAgentRunner(SubAgentRequest request, CancellationToken cancellationToken);

public static class SubAgent
{
    // A rough token count where the server reports none of its own.
    public const int CharsPerToken = 3;

    // observe wraps the subagent's host, e.g. to show its steps; cancellation throws.
    public static async Task<SubAgentResult> RunAsync(IProvider provider, SubAgentRequest request, Func<ITurnHost, ITurnHost>? observe, CancellationToken cancellationToken)
    {
        var tools = new ToolSet(request.Tools);
        var host = new Host(tools, request, provider.ContextWindow - request.ContextReserve);
        using (var session = provider.OpenSession(request.Name))
        {
            await session.RunTurnAsync(
                new TurnRequest(request.SystemPrompt, [new ChatMessage(ChatRole.User, request.Brief)], tools.Definitions, request.Limits),
                observe?.Invoke(host) ?? host,
                cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new SubAgentResult(host.Text, host.RoundCap);
    }

    private sealed class Host(ToolSet tools, SubAgentRequest request, int contextLimit) : ITurnHost
    {
        private readonly string _roundCapNotice = ClientToolLoop.RoundCapNotice(request.Limits.MaxRounds);
        private long _chars = request.SystemPrompt.Length + request.Brief.Length;
        private int? _reported;
        private int _calls;

        public string? Text { get; private set; }

        public bool RoundCap { get; private set; }

        // Servers that serve the prompt from a KV cache without reporting it (WebQwen) count only the
        // newly prefilled tokens, so only a whole count is taken from them.
        private int Context => Math.Max(_reported ?? 0, (int)(_chars / CharsPerToken));

        public void OnRoundStart()
        {
        }

        public void OnText(string delta)
        {
        }

        public void OnReasoning(string delta)
        {
        }

        public void OnRoundStats(RoundStats stats) => _reported = stats.ContextTokens ?? _reported;

        public async Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken)
        {
            if (Context >= contextLimit)
                return new ToolResult(request.ContextFullOutput) { Summary = $"Did not run {call.Name}: no room left to read more" };
            try
            {
                var result = await tools.ExecuteAsync(call, cancellationToken).ConfigureAwait(false);
                return ++_calls == request.CallReminder?.AfterCalls
                    ? result with { Output = result.Output + "\n\n" + request.CallReminder.Text }
                    : result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }

        public void OnNotice(string text)
        {
            if (text == _roundCapNotice)
                RoundCap = true;
        }

        public void AppendTranscript(ChatMessage message)
        {
            _chars += message.Content.Length + (message.Reasoning?.Length ?? 0) + (message.ToolCalls?.Sum(c => c.Name.Length + c.Arguments.Length) ?? 0);
            if (message.Role == ChatRole.Assistant && message.Content.Trim().Length > 0)
                Text = message.Content.Trim();
        }
    }
}
