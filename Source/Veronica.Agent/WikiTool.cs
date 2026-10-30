using System.Text.Json;
using Veronica.Agent.Tools;

namespace Veronica.Agent;

// Asks a researcher subagent, so its reading never enters the chat's transcript.
public sealed class WikiTool(Func<IReadOnlyList<WikiPage>> pages, Func<IReadOnlyList<Skill>> skills, IReadOnlyList<ITool> projectTools, SkillPlan plan) : ITool
{
    public const string ToolName = "wiki";

    // Room left for the answer once the researcher's prompt has grown past the reading limit.
    public const int AnswerReserve = 10_000;

    private SubAgentRunner? _runner;
    private Func<IReadOnlyList<ChatMessage>> _transcript = () => [];

    public string Name => ToolName;

    public string Description =>
        "Asks a researcher who reads the wiki (how the game and the Editor work), the skills and the project, and sees this conversation. " +
        "It answers with facts, what it found in the project, and the next step.";

    public string Parameters => """
        {"type":"object","properties":{"question":{"type":"string","description":"One direct question about what you need to know for the task"}},"required":["question"]}
        """;

    // By the chat that owns the transcript the researcher reads; it runs and shows the research.
    public void Attach(SubAgentRunner runner, Func<IReadOnlyList<ChatMessage>> transcript)
    {
        _runner = runner;
        _transcript = transcript;
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (_runner is not { } runner)
            return new ToolResult("The wiki is not available with this model provider.");
        var question = ToolArgs.Text(arguments, "question");
        if (question.Length == 0)
            return new ToolResult("wiki needs a question.");

        var request = new SubAgentRequest(
            ToolName,
            "Wiki research: " + question,
            Prompts.Researcher(pages(), skills(), projectTools.Select(t => t.Name).ToList()),
            Prompts.ResearchBrief(TranscriptRender.Render(_transcript(), new HashSet<string> { ToolName }), plan.CurrentItem(), question),
            [new WikiPageTool(pages), new SkillTool(skills, new SkillPlan()), .. projectTools],
            Prompts.ResearcherLimits,
            AnswerReserve,
            Prompts.ResearchContextFull);
        var result = await runner(request, cancellationToken).ConfigureAwait(false);
        if (result.Text == null)
            return new ToolResult("The research ended without an answer. Continue without it.");
        return new ToolResult(result.RoundCap ? result.Text + "\n\n(The research ran out of rounds; this may be incomplete.)" : result.Text);
    }
}
