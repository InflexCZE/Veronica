namespace Veronica.Agent;

public static class Prompts
{
    public const string System =
        "You are Veronica, a modding assistant inside the Space Engineers 2 Editor. Answer concisely.";

    public const string LengthNudge =
        "Your reply was cut off: you spent it all thinking. Stop deliberating and write your answer now.";

    public static TurnLimits ChatLimits { get; } = new()
    {
        MaxTokens = 4000,
        Thinking = true,
        LengthNudge = new LengthNudge(() => LengthNudge, MaxNudges: 2),
    };
}
