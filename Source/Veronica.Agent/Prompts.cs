namespace Veronica.Agent;

public static class Prompts
{
    public const string System =
        "You are Veronica, a modding assistant inside the Space Engineers 2 Editor. Answer concisely.\n\n" +
        "When a task needs a file or value you cannot find in the project, ask the user for it.";

    public const string SkillIntro =
        "Skills are step lists for modding tasks. When one covers the task, call skill(name) first and follow it.";

    public static string SystemWith(IReadOnlyList<Skill> skills) =>
        skills.Count == 0 ? System : System + "\n\n" + SkillIntro + SkillFiles.Index(skills);

    public const string LengthNudge =
        "Your reply was cut off: you spent it all thinking. Stop deliberating and write your answer now.";

    public static TurnLimits ChatLimits { get; } = new()
    {
        // Until tool rewards exist (Docs/TODOs/ToolRewards.md), a whole block build has to fit in one turn.
        MaxRounds = 50,
        FreeRoundTools = new HashSet<string> { SkillTool.ToolName },
        MaxTokens = 4000,
        Thinking = true,
        LengthNudge = new LengthNudge(() => LengthNudge, MaxNudges: 2),
    };
}
