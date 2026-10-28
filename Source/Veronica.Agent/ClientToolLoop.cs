using System.Text;

namespace Veronica.Agent;

// The tool loop for providers whose wire protocol is one model request per round:
// stream, run the tools, stream again.
public static class ClientToolLoop
{
    public const string CancelledToolOutput = "Turn stopped by the user before this tool produced a result.";

    public static string RoundCapNotice(int maxRounds) =>
        $"Stopped after {maxRounds} tool rounds in one turn. Send a new message (e.g. \"continue\") if the work is unfinished.";

    public static string SilentWaitOutput(string tool) =>
        $"You wrote nothing for the user with this call, so there is nothing for them to answer. In one reply, write your question to the user and call {tool}.";

    public static IProviderSession CreateSession(StreamRound stream) => new Session(stream);

    private sealed class Session(StreamRound stream) : IProviderSession
    {
        public Task RunTurnAsync(TurnRequest request, ITurnHost host, CancellationToken cancellationToken) =>
            RunAsync(stream, request, host, cancellationToken);

        public Task ResetAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed record RoundResult(string Text, string Reasoning, IReadOnlyList<ToolCall> ToolCalls, string? FinishReason);

    public static async Task RunAsync(StreamRound stream, TurnRequest request, ITurnHost host, CancellationToken cancellationToken)
    {
        var limits = request.Limits;
        int maxRounds = limits.MaxRounds;
        int hardRoundCap = maxRounds * 3;
        var transcript = new List<ChatMessage>(request.Transcript);

        void Append(ChatMessage message)
        {
            transcript.Add(message);
            host.AppendTranscript(message);
        }

        void AppendToolResult(ToolCall call, string output) =>
            Append(new ChatMessage(ChatRole.Tool, output) { ToolCallId = call.Id, Name = call.Name });

        async Task<RoundResult?> StreamRoundAsync()
        {
            var calls = new SortedDictionary<int, (string Id, StringBuilder Name, StringBuilder Arguments)>();
            var text = new StringBuilder();
            var reasoning = new StringBuilder();
            string? finishReason = null;
            host.OnRoundStart();
            var messages = new List<ChatMessage>(transcript.Count + 1) { new(ChatRole.System, request.SystemPrompt) };
            messages.AddRange(transcript);
            try
            {
                await stream(new RoundRequest(messages, request.Tools, limits.Thinking, limits.MaxTokens), delta =>
                {
                    switch (delta)
                    {
                        case TextDelta d:
                            text.Append(d.Text);
                            host.OnText(d.Text);
                            break;
                        case ReasoningDelta d:
                            reasoning.Append(d.Text);
                            host.OnReasoning(d.Text);
                            break;
                        case StatsDelta d:
                            host.OnRoundStats(d.Stats);
                            break;
                        case ToolCallDelta d:
                            if (!calls.TryGetValue(d.Index, out var call)) call = ("", new StringBuilder(), new StringBuilder());
                            if (!string.IsNullOrEmpty(d.Id)) call.Id = d.Id;
                            call.Name.Append(d.Name);
                            call.Arguments.Append(d.ArgumentsDelta);
                            calls[d.Index] = call;
                            break;
                        case DoneDelta d:
                            finishReason = d.FinishReason;
                            break;
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            var toolCalls = calls.Values.Select(c => new ToolCall(c.Id, c.Name.ToString(), c.Arguments.ToString())).ToList();
            return new RoundResult(text.ToString(), reasoning.ToString(), toolCalls, finishReason);
        }

        int nudgesSent = 0;
        bool endNudgeArmed = true;
        int spentRounds = 0;
        for (int round = 1; round <= hardRoundCap; round++)
        {
            var result = await StreamRoundAsync().ConfigureAwait(false);
            if (result == null)
                return;

            Append(new ChatMessage(ChatRole.Assistant, result.Text)
            {
                Reasoning = result.Reasoning.Length > 0 ? result.Reasoning : null,
                ToolCalls = result.ToolCalls.Count > 0 ? result.ToolCalls : null,
            });

            // A stopped round keeps what it generated, but its tool calls never run.
            if (cancellationToken.IsCancellationRequested)
            {
                foreach (var call in result.ToolCalls)
                    AppendToolResult(call, CancelledToolOutput);
                return;
            }

            if (result.ToolCalls.Count == 0)
            {
                // Only a round cut off before any answer text: nudging after a half-written
                // answer would make the model start it over.
                bool cutWhileThinking = result.FinishReason == "length" && result.Text.Trim().Length == 0;
                if (limits.LengthNudge is { } lengthNudge && cutWhileThinking && (lengthNudge.MaxNudges == null || nudgesSent < lengthNudge.MaxNudges))
                {
                    nudgesSent++;
                    spentRounds++;
                    if (spentRounds >= maxRounds || round == hardRoundCap)
                    {
                        host.OnNotice(RoundCapNotice(maxRounds));
                        return;
                    }
                    Append(new ChatMessage(ChatRole.User, lengthNudge.Message()));
                    continue;
                }

                string? endNudge = endNudgeArmed ? limits.EndNudge?.Message(result.Text) : null;
                if (endNudge != null)
                {
                    endNudgeArmed = false;
                    spentRounds++;
                    if (spentRounds >= maxRounds || round == hardRoundCap)
                    {
                        host.OnNotice(RoundCapNotice(maxRounds));
                        return;
                    }
                    Append(new ChatMessage(ChatRole.User, endNudge));
                    continue;
                }
                return;
            }

            var bookkeeping = limits.EndNudge?.BookkeepingTools;
            if (bookkeeping == null || result.ToolCalls.Any(c => !bookkeeping.Contains(c.Name)))
                endNudgeArmed = true;
            if (!result.ToolCalls.All(c => limits.FreeRoundTools.Contains(c.Name)))
                spentRounds++;
            bool lastRound = spentRounds == maxRounds || round == hardRoundCap;
            // Models narrate as they work, so only the waiting round's own text can be the question.
            bool roundHasText = result.Text.Trim().Length > 0;
            for (int i = 0; i < result.ToolCalls.Count; i++)
            {
                var call = result.ToolCalls[i];
                var toolResult = await host.OnToolCallAsync(call, new ToolCallContext(lastRound), cancellationToken).ConfigureAwait(false);
                if (toolResult == null)
                {
                    // The transcript never carries dangling tool calls into the next turn.
                    for (int j = i; j < result.ToolCalls.Count; j++)
                        AppendToolResult(result.ToolCalls[j], CancelledToolOutput);
                    return;
                }
                bool silentWait = !roundHasText && limits.TurnEndingTools.Contains(call.Name);
                AppendToolResult(call, silentWait ? SilentWaitOutput(call.Name) : toolResult.Output);
                if (toolResult.EarnedReward)
                    spentRounds = Math.Max(0, spentRounds - maxRounds / 2);
            }
            if (roundHasText && result.ToolCalls.Any(c => limits.TurnEndingTools.Contains(c.Name)))
                return;
            if (lastRound)
            {
                host.OnNotice(RoundCapNotice(maxRounds));
                return;
            }
        }
    }
}
