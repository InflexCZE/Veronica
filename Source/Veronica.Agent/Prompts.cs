namespace Veronica.Agent;

public static class Prompts
{
    public const string System =
        "You are Veronica, a modding assistant inside the Space Engineers 2 Editor. Answer concisely.\n\n" +
        "Messages in <harness> tags come from the Editor, not the user; the user has not seen them.";

    public const string AskForMissing =
        "When a task needs a file or value you cannot find in the project, ask the user for it.";

    public const string HowWeWork =
        "How we work:\n" +
        "1. Understand the task. Ask the user for what you cannot find in the project.\n" +
        "2. Read the skills that may cover it with skill(name).\n" +
        "3. Activate the one that fits the task best with activate_skill(name), before you ask the user anything. The Editor then gives you its plan one item at a time.\n" +
        "4. Work on the current item, using the skill's steps. When it needs something from the user, a choice that is theirs (a name or text players will see, a folder, a value they did not give) " +
        "or a step they do in the Editor, ask for exactly that with wait_for_user, then continue. Ask only for what this item needs. " +
        "When its work is done, call mark_completed with a short proof of what you did; " +
        "when it does not apply, call mark_skipped with the reason. The Editor then names the next item.\n\n" +
        "Skills:";

    // Without skills there is no plan to work, so only the ask-the-user rule of "How we work" is left.
    public static string SystemWith(IReadOnlyList<Skill> skills) =>
        skills.Count == 0 ? System + "\n\n" + AskForMissing : System + "\n\n" + HowWeWork + SkillFiles.Index(skills);

    public const string LengthNudge =
        "Your reply was cut off: you spent it all thinking. Stop deliberating and write your answer now.";

    public static TurnLimits ChatLimits(SkillPlan plan) => new()
    {
        // Until tool rewards exist (Docs/TODOs/ToolRewards.md), a whole block build has to fit in one turn.
        MaxRounds = 50,
        FreeRoundTools = SkillPlan.ToolNames,
        MaxTokens = 4000,
        ThinkingBudget = 4000,
        Thinking = true,
        LengthNudge = new LengthNudge(() => LengthNudge + plan.CurrentItemNote(), MaxNudges: 2),
        EndNudge = new EndNudge(_ => plan.EndNudge(), SkillPlan.ToolNames),
        TurnEndingTools = new HashSet<string> { WaitForUserTool.ToolName },
    };
}
