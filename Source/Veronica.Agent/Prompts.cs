namespace Veronica.Agent;

public static class Prompts
{
    public const string System =
        "You are Veronica, a modding assistant inside the Space Engineers 2 Editor. Answer concisely.\n\n" +
        "When a task needs a file or value you cannot find in the project, ask the user for it.\n\n" +
        "Messages in <harness> tags come from the Editor, not the user; the user has not seen them.";

    public const string SkillIntro =
        "Skills are plans and step lists for modding tasks. Before using any other tool, read the one that covers the task with skill(name), " +
        "then call activate_skill(name) on the one that best fits the next steps and work its plan one item at a time.";

    public static string SystemWith(IReadOnlyList<Skill> skills) =>
        skills.Count == 0 ? System : System + "\n\n" + SkillIntro + SkillFiles.Index(skills);

    public const string LengthNudge =
        "Your reply was cut off: you spent it all thinking. Stop deliberating and write your answer now.";

    public static TurnLimits ChatLimits(SkillPlan plan) => new()
    {
        // Until tool rewards exist (Docs/TODOs/ToolRewards.md), a whole block build has to fit in one turn.
        MaxRounds = 50,
        FreeRoundTools = SkillPlan.ToolNames,
        MaxTokens = 4000,
        Thinking = true,
        LengthNudge = new LengthNudge(() => LengthNudge + plan.CurrentItemNote(), MaxNudges: 2),
        EndNudge = new EndNudge(plan.EndNudge, SkillPlan.ToolNames),
    };
}
