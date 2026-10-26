using System.Reflection;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;

namespace Veronica;

// The skill files the chat offers, reloaded on every /clear.
public sealed class SkillShelf
{
    private IReadOnlyList<Skill> _skills = [];

    public IReadOnlyList<Skill> Current => _skills;

    public string SystemPrompt()
    {
        var load = SkillFiles.Load(SkillsDirectory);
        foreach (var problem in load.Problems)
            Log.Default.WriteLine($"[Veronica] Skill skipped: {problem}");
        _skills = load.Skills;
        return Prompts.SystemWith(_skills);
    }

    private static string? SkillsDirectory =>
        typeof(SkillShelf).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(a => a.Key == "SkillsDir")?.Value;
}
