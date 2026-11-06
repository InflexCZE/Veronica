namespace Veronica.Agent;

// The Markdown files the model is given (skills, wiki pages): flat frontmatter, then a body.
public static class Frontmatter
{
    public const string Extension = ".md";

    public const string PendingUpdateExtension = ".new.md";

    // An update's new version of a file the user changed waits beside it until they pick one (Docs/Tech/Distribution.md).
    public static bool IsPendingUpdate(string path) => path.EndsWith(PendingUpdateExtension, StringComparison.OrdinalIgnoreCase);

    // Frontmatter is flat 'key: value' lines between '---' lines; an indented line continues the previous value.
    public static (IReadOnlyDictionary<string, string> Fields, string Body) Parse(string text, params string[] required)
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

        foreach (var key in required)
            if (!fields.TryGetValue(key, out var value) || value.Length == 0)
                throw new FormatException($"frontmatter has no {key}.");

        var body = string.Join("\n", lines.Skip(end + 1)).Trim();
        if (body.Length == 0)
            throw new FormatException("has no body.");
        return (fields, body);
    }

    // A broken file is skipped and reported, so one bad edit does not take the other files down.
    public static (List<T> Items, List<string> Problems) LoadAll<T>(string? directory, Func<string, T> parse, Func<T, string> name, string kind)
    {
        var items = new List<T>();
        var problems = new List<string>();
        if (directory == null || !Directory.Exists(directory))
            return (items, problems);

        List<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(directory, "*" + Extension)
                .Where(p => !IsPendingUpdate(p))
                .Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.Add($"{directory}: {e.Message}");
            return (items, problems);
        }

        foreach (var path in paths)
        {
            var fileName = Path.GetFileName(path);
            try
            {
                var item = parse(File.ReadAllText(path));
                var itemName = name(item);
                if (!string.Equals(itemName, Path.GetFileNameWithoutExtension(path), StringComparison.Ordinal))
                    problems.Add($"{fileName}: name '{itemName}' differs from the file name.");
                else if (items.Any(s => string.Equals(name(s), itemName, StringComparison.OrdinalIgnoreCase)))
                    problems.Add($"{fileName}: another {kind} is already named '{itemName}'.");
                else
                    items.Add(item);
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
        return (items, problems);
    }
}
