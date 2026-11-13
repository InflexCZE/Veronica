using System.Text;
using System.Text.Json;
using Veronica.Agent.Tools;

namespace Veronica.Agent;

public sealed record Skill(string Name, string Description, string Body, IReadOnlyList<string> Plan)
{
    // What the model reads: the steps without the plan, which the Editor hands out one item at a time.
    public string Overview => SkillFiles.WithoutPlan(Body);
}

public sealed record SkillLoad(IReadOnlyList<Skill> Skills, IReadOnlyList<string> Problems);

public static class SkillFiles
{
    public static SkillLoad Load(string? directory)
    {
        var (skills, problems) = Frontmatter.LoadAll(directory, Parse, s => s.Name, "skill");
        return new SkillLoad(skills, problems);
    }

    public static Skill Parse(string text)
    {
        var (fields, body) = Frontmatter.Parse(text, "name", "description");
        return new Skill(fields["name"], fields["description"], body, ParsePlan(body));
    }

    public const string PlanHeading = "Plan:";

    // The plan is the "- item" lines right below the heading, up to the next blank line. Any other
    // line in that block fails the file, so a wrapped item cannot silently cut the plan short.
    // A skill without the heading cannot be activated.
    private static IReadOnlyList<string> ParsePlan(string body)
    {
        var lines = body.Split('\n');
        int heading = Array.FindIndex(lines, l => l.Trim() == PlanHeading);
        if (heading < 0)
            return [];
        var block = lines.Skip(heading + 1).TakeWhile(l => l.Trim().Length > 0).ToList();
        if (block.Count == 0 || block.Any(l => !l.StartsWith("- ", StringComparison.Ordinal) || l[2..].Trim().Length == 0))
            throw new FormatException($"'{PlanHeading}' needs one '- item' line per item right below it, ended by a blank line.");
        return block.Select(l => l[2..].Trim()).ToList();
    }

    public static string WithoutPlan(string body)
    {
        var lines = body.Split('\n').ToList();
        int heading = lines.FindIndex(l => l.Trim() == PlanHeading);
        if (heading < 0)
            return body;
        int end = heading + 1;
        while (end < lines.Count && lines[end].Trim().Length > 0)
            end++;
        while (end < lines.Count && lines[end].Trim().Length == 0)
            end++;
        lines.RemoveRange(heading, end - heading);
        return string.Join("\n", lines).Trim();
    }

    public static string Index(IReadOnlyList<Skill> skills)
    {
        var index = new StringBuilder();
        foreach (var skill in skills)
            index.Append("\n- ").Append(skill.Name).Append(": ").Append(skill.Description);
        return index.ToString();
    }
}

public sealed class SkillTool(Func<IReadOnlyList<Skill>> skills, Func<SkillPlan> plans) : ITool
{
    public const string ToolName = "skill";

    public SkillTool(Func<IReadOnlyList<Skill>> skills, SkillPlan plan)
        : this(skills, () => plan)
    {
    }

    public string Name => ToolName;

    public string Description => "Shows what a skill from the system prompt's skill list needs and its steps, to decide whether it fits the task; to do its work, activate it with activate_skill.";

    public string Parameters => """
        {"type":"object","properties":{"name":{"type":"string","description":"Skill name, e.g. create-block"}},"required":["name"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var skill = Find(skills(), arguments, out var notFound);
        if (skill == null)
            return Task.FromResult(new ToolResult(notFound, Refused: true) { Summary = $"Could not open skill {Gist.Text(arguments, "name")}: there is no such skill" });
        plans().MarkRead(skill.Name);
        return Task.FromResult(new ToolResult(skill.Overview) { Summary = $"Opened skill: {skill.Name}" });
    }

    public string Attempt(JsonElement arguments) => $"open skill {Gist.Text(arguments, "name")}";

    public static Skill? Find(IReadOnlyList<Skill> all, JsonElement arguments, out string notFound)
    {
        var name = arguments.TryGetProperty("name", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : "";
        var names = all.Count == 0 ? "there are none" : "the skills are: " + string.Join(", ", all.Select(s => s.Name));
        notFound = $"There is no skill named '{name}'; {names}.";
        return all.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
