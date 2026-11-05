namespace Veronica.Agent;

// Summarizing failed: the conversation is left as it was.
public sealed class CompactionFailedException(string message) : Exception(message);

// A turn ended at a full context that compaction could not get past. CanCompact: the user may compact again.
public sealed class ContextFullTurnException(string message, bool canCompact) : Exception(message)
{
    public bool CanCompact { get; } = canCompact;
}

// Replaces the chat's transcript with a summary once it no longer fits the context window.
// One compaction is allowed; the allowance comes back with the user's next message or once the model
// closed ItemsPerAllowance plan items since, so a model that compacts without progress stops.
public sealed class Compaction(SkillPlan plan, Func<IReadOnlyList<Skill>> skills)
{
    public const string SessionName = "compaction";

    public const int ItemsPerAllowance = 5;

    private SubAgentRunner? _runner;
    private bool _spent;
    private int _closedAtSpend;

    // By the chat, which runs and shows the summarizer.
    public void Attach(SubAgentRunner runner) => _runner = runner;

    public bool Available => _runner != null;

    public bool Allowed => !_spent || plan.ClosedItems - _closedAtSpend >= ItemsPerAllowance;

    public void UserInteracted() => _spent = false;

    // The message that replaces the whole transcript. Cancellation throws; any other failure is a CompactionFailedException.
    public async Task<ChatMessage> CompactAsync(IReadOnlyList<ChatMessage> transcript, CancellationToken cancellationToken)
    {
        var runner = _runner ?? throw new InvalidOperationException("No compaction runner is attached.");
        _spent = true;
        _closedAtSpend = plan.ClosedItems;
        var request = new SubAgentRequest(
            SessionName,
            Prompts.CompactionTitle,
            Prompts.Compactor,
            Prompts.CompactionBrief(TranscriptRender.Render(transcript, new HashSet<string> { WikiTool.ToolName }), plan.OpenPlans()),
            [],
            Prompts.CompactorLimits,
            0,
            "");
        SubAgentResult result;
        try
        {
            result = await runner(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ContextFullException)
        {
            throw new CompactionFailedException("the conversation is too long even to summarize");
        }
        catch (Exception e)
        {
            throw new CompactionFailedException(e.Message);
        }
        if (string.IsNullOrWhiteSpace(result.Text))
            throw new CompactionFailedException("the summary came back empty");
        var active = plan.ActiveSkill is { } name
            ? skills().FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            : null;
        plan.ForgetReads(active?.Name);
        return new ChatMessage(ChatRole.User, Prompts.Compacted(result.Text.Trim(), LastWikiAnswer(transcript), active, plan.CurrentItem()));
    }

    // Research costs a whole subagent run to redo, and a summary would condense away its exact names and values.
    public static string? LastWikiAnswer(IReadOnlyList<ChatMessage> transcript)
    {
        for (int i = transcript.Count - 1; i >= 0; i--)
        {
            var message = transcript[i];
            if (message.Role != ChatRole.Tool || message.Name != WikiTool.ToolName)
                continue;
            var content = message.Content.Trim();
            if (content is WikiTool.UnavailableOutput or WikiTool.NoQuestionOutput or WikiTool.NoAnswerOutput
                or ClientToolLoop.CancelledToolOutput or ClientToolLoop.CutCallOutput
                || content.StartsWith(WikiTool.ToolName + " failed:", StringComparison.Ordinal))
                continue;
            return content;
        }
        return null;
    }
}
