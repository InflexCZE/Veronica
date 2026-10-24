namespace Veronica.Agent;

// The chat's transcript and its provider session. One turn at a time.
public sealed class Conversation(IProviderSession session, string systemPrompt, TurnLimits limits) : IDisposable
{
    private readonly List<ChatMessage> _transcript = [];

    public IReadOnlyList<ChatMessage> Transcript => _transcript;

    public async Task SendAsync(string text, ITurnHost host, CancellationToken cancellationToken)
    {
        _transcript.Add(new ChatMessage(ChatRole.User, text));
        var request = new TurnRequest(systemPrompt, _transcript.ToList(), [], limits);
        await session.RunTurnAsync(request, new Recorder(_transcript, host), cancellationToken).ConfigureAwait(false);
    }

    public Task ClearAsync()
    {
        _transcript.Clear();
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
