namespace Veronica.Agent;

// The chat's transcript and its provider session. One turn at a time.
// The system prompt is built anew on every clear, so edited skill files take effect without a restart.
// With a compaction attached, a turn that hits the end of the context window is summarized and goes on
// from the summary (Compaction); a wall it cannot get past ends the turn with a ContextFullTurnException.
public sealed class Conversation(IProviderSession session, Func<string> systemPrompt, TurnLimits limits, IReadOnlyList<ToolDefinition> tools, Compaction? compaction = null) : IDisposable
{
    private readonly List<ChatMessage> _transcript = [];
    private string _systemPrompt = systemPrompt();

    public IReadOnlyList<ChatMessage> Transcript => _transcript;

    public async Task SendAsync(string text, ITurnHost host, CancellationToken cancellationToken)
    {
        _transcript.Add(new ChatMessage(ChatRole.User, text));
        compaction?.UserInteracted();
        await RunAsync(new TurnRequest(_systemPrompt, _transcript.ToList(), tools, limits), host, cancellationToken).ConfigureAwait(false);
    }

    // The user's choice after a turn ended at a full context: compact now, then the turn goes on from the summary.
    public async Task CompactAsync(ITurnHost host, CancellationToken cancellationToken)
    {
        if (compaction is not { Available: true })
            throw new InvalidOperationException("Compaction is not available with this model provider.");
        compaction.UserInteracted();
        var request = await CompactedRequestAsync(compaction, host, cancellationToken).ConfigureAwait(false);
        await RunAsync(request, host, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(TurnRequest request, ITurnHost host, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await session.RunTurnAsync(request, new Recorder(_transcript, host), cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (ContextFullException) when (compaction is { Available: true } && !cancellationToken.IsCancellationRequested)
            {
                if (!compaction.Allowed)
                    throw new ContextFullTurnException(Prompts.ContextFullAgain, canCompact: true);
                request = await CompactedRequestAsync(compaction, host, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<TurnRequest> CompactedRequestAsync(Compaction compaction, ITurnHost host, CancellationToken cancellationToken)
    {
        ChatMessage compacted;
        try
        {
            compacted = await compaction.CompactAsync(_transcript.ToList(), cancellationToken).ConfigureAwait(false);
        }
        catch (CompactionFailedException e)
        {
            throw new ContextFullTurnException(Prompts.CompactionFailed(e.Message), canCompact: false);
        }
        _transcript.Clear();
        _transcript.Add(compacted);
        host.OnNotice(Prompts.CompactedNotice);
        return new TurnRequest(_systemPrompt, _transcript.ToList(), tools, limits) { Compacted = true };
    }

    public Task ClearAsync()
    {
        var fresh = systemPrompt();
        _transcript.Clear();
        _systemPrompt = fresh;
        compaction?.UserInteracted();
        return session.ResetAsync();
    }

    public void Dispose() => session.Dispose();

    private sealed class Recorder(List<ChatMessage> transcript, ITurnHost host) : ITurnHost
    {
        public void OnRoundStart() => host.OnRoundStart();
        public void OnText(string delta) => host.OnText(delta);
        public void OnReasoning(string delta) => host.OnReasoning(delta);
        public void OnRoundStats(RoundStats stats) => host.OnRoundStats(stats);
        public Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken) =>
            host.OnToolCallAsync(call, context, cancellationToken);
        public void OnNotice(string text) => host.OnNotice(text);

        public void AppendTranscript(ChatMessage message)
        {
            transcript.Add(message);
            host.AppendTranscript(message);
        }
    }
}
