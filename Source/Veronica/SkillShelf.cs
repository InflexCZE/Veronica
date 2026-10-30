using System.Reflection;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;

namespace Veronica;

// The skill and wiki files the chat offers, reloaded on every /clear.
public sealed class SkillShelf
{
    private IReadOnlyList<Skill> _skills = [];
    private IReadOnlyList<WikiPage> _pages = [];

    public IReadOnlyList<Skill> Current => _skills;

    public IReadOnlyList<WikiPage> Pages => _pages;

    public string SystemPrompt()
    {
        var load = SkillFiles.Load(Directory("SkillsDir"));
        foreach (var problem in load.Problems)
            Log.Default.WriteLine($"[Veronica] Skill skipped: {problem}");
        _skills = load.Skills;

        var wiki = WikiFiles.Load(Directory("WikiDir"));
        foreach (var problem in wiki.Problems)
            Log.Default.WriteLine($"[Veronica] Wiki page skipped: {problem}");
        _pages = wiki.Pages;

        return Prompts.SystemWith(_skills);
    }

    private static string? Directory(string key) =>
        typeof(SkillShelf).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(a => a.Key == key)?.Value;
}
