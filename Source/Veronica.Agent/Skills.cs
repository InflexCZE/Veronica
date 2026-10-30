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
    public const string Extension = ".md";

    // A broken file is skipped and reported, so one bad edit does not take the other skills down.
    public static SkillLoad Load(string? directory)
    {
        var skills = new List<Skill>();
        var problems = new List<string>();
        if (directory == null || !Directory.Exists(directory))
            return new SkillLoad(skills, problems);

        List<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(directory, "*" + Extension).Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.Add($"{directory}: {e.Message}");
            return new SkillLoad(skills, problems);
        }

        foreach (var path in paths)
        {
            var fileName = Path.GetFileName(path);
            try
            {
                var skill = Parse(File.ReadAllText(path));
                if (!string.Equals(skill.Name, Path.GetFileNameWithoutExtension(path), StringComparison.Ordinal))
                    problems.Add($"{fileName}: name '{skill.Name}' differs from the file name.");
                else if (skills.Any(s => string.Equals(s.Name, skill.Name, StringComparison.OrdinalIgnoreCase)))
                    problems.Add($"{fileName}: another skill is already named '{skill.Name}'.");
                else
                    skills.Add(skill);
            }
            catch (FormatException e)
            {
                problems.Add($"{fileName}: {e.Message}");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problems.Add($"{fileName}: {e.Message}");
            }
        }
        return new SkillLoad(skills, problems);
    }

    // Frontmatter is flat 'key: value' lines between '---' lines; an indented line continues the previous value.
    public static Skill Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
            throw new FormatException("does not start with a '---' frontmatter line.");

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        string? last = null;
        int end = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim() == "---")
            {
                end = i;
                break;
            }
            if (line.Trim().Length == 0)
                continue;
            if (char.IsWhiteSpace(line[0]) && last != null)
            {
                fields[last] = fields[last] + " " + line.Trim();
                continue;
            }
            int colon = line.IndexOf(':');
            if (colon <= 0)
                throw new FormatException($"frontmatter line {i + 1} is not 'key: value'.");
            last = line[..colon].Trim();
            fields[last] = line[(colon + 1)..].Trim();
        }
        if (end < 0)
            throw new FormatException("frontmatter has no closing '---' line.");

        if (!fields.TryGetValue("name", out var name) || name.Length == 0)
            throw new FormatException("frontmatter has no name.");
        if (!fields.TryGetValue("description", out var description) || description.Length == 0)
            throw new FormatException("frontmatter has no description.");

        var body = string.Join("\n", lines.Skip(end + 1)).Trim();
        if (body.Length == 0)
            throw new FormatException("has no body.");
        return new Skill(name, description, body, ParsePlan(body));
    }

    public const string PlanHeading = "Plan:";

    // The plan is the "- item" lines right below the heading, up to the next blank line. Any other
    // line in that block fails the file, so a wrapped item cannot silently cut the plan short.
    // A skill without the heading is a reference: read on demand, never activated.
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

public sealed class SkillTool(Func<IReadOnlyList<Skill>> skills, SkillPlan plan) : ITool
{
    public const string ToolName = "skill";

    public string Name => ToolName;

    public string Description => "Returns a skill from the system prompt's skill list: what it needs and its steps.";

    public string Parameters => """
        {"type":"object","properties":{"name":{"type":"string","description":"Skill name, e.g. create-block"}},"required":["name"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var skill = Find(skills(), arguments, out var notFound);
        if (skill == null)
            return Task.FromResult(new ToolResult(notFound));
        plan.MarkRead(skill.Name);
        return Task.FromResult(new ToolResult(skill.Overview));
    }

    public static Skill? Find(IReadOnlyList<Skill> all, JsonElement arguments, out string notFound)
    {
        var name = arguments.TryGetProperty("name", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : "";
        var names = all.Count == 0 ? "there are none" : "the skills are: " + string.Join(", ", all.Select(s => s.Name));
        notFound = $"There is no skill named '{name}'; {names}.";
        return all.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
