namespace Veronica.Editing;

// How the model names content items and templates.
public static class ContentNames
{
    public static string Normalize(string path) => path.Trim().Replace('/', '\\').Trim('\\');

    public static string[] Segments(string folderPath) =>
        Normalize(folderPath).Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // A project-relative path, or a bare file name; the extension is optional either way.
    public static bool Matches(string projectRelativePath, string query)
    {
        string path = Normalize(projectRelativePath);
        string wanted = Normalize(query);
        if (wanted.Length == 0)
            return false;

        if (Same(path, wanted) || Same(WithoutExtension(path), wanted))
            return true;
        if (wanted.Contains('\\'))
            return false;

        string name = Path.GetFileName(path);
        return Same(name, wanted) || Same(Path.GetFileNameWithoutExtension(name), wanted);
    }

    // The end of the path on whole segments, e.g. Ion\100\Model names Assets\Blocks\Ion\100\Model.def.
    public static bool EndsWithPath(string projectRelativePath, string query)
    {
        string[] wanted = Segments(query);
        string[] path = Segments(projectRelativePath);
        if (wanted.Length < 2 || wanted.Length > path.Length)
            return false;
        int offset = path.Length - wanted.Length;
        for (int i = 0; i < wanted.Length - 1; i++)
            if (!Same(path[offset + i], wanted[i]))
                return false;
        string last = path[^1];
        return Same(last, wanted[^1]) || Same(WithoutExtension(last), wanted[^1]);
    }

    // An exact name always wins. Then a leading project name ("Vanilla\...") narrows the search to that project,
    // and last a path ending finds its one item anywhere. A project named like a folder never lets a narrowed
    // guess beat an exact path.
    public static List<T> Pick<T>(IReadOnlyCollection<T> items, Func<T, string> path, Func<T, string> project, string query)
    {
        var exact = items.Where(i => Matches(path(i), query)).ToList();
        if (exact.Count > 0)
            return exact;

        string[] segments = Segments(query);
        if (segments.Length > 1 && items.Any(i => Same(project(i), segments[0])))
        {
            string rest = string.Join('\\', segments[1..]);
            var pool = items.Where(i => Same(project(i), segments[0])).ToList();
            var named = pool.Where(i => Matches(path(i), rest)).ToList();
            if (named.Count == 0)
                named = pool.Where(i => EndsWithPath(path(i), rest)).ToList();
            if (named.Count > 0)
                return named;
        }
        return items.Where(i => EndsWithPath(path(i), query)).ToList();
    }

    // find_content: every word of the query is part of the file name or the definition type.
    public static bool QueryMatches(string query, string fileName, string? typeName)
    {
        string[] words = query.Split([' ', '\t', '/', '\\', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(w =>
            fileName.Contains(w, StringComparison.OrdinalIgnoreCase)
            || (typeName?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static string WithoutExtension(string path)
    {
        int dot = path.LastIndexOf('.');
        return dot > path.LastIndexOf('\\') ? path[..dot] : path;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
