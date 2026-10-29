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

    // A folder path that is only a project name ("Vanilla", "Vanilla/") names that project's root.
    public static string? ProjectRoot(IEnumerable<string> projectNames, string folderPath)
    {
        string[] segments = Segments(folderPath);
        return segments.Length == 1 ? projectNames.FirstOrDefault(p => Same(p, segments[0])) : null;
    }

    // For a name that matched nothing: the few items whose last segment is spelled almost the same, so a slip such
    // as "X.Client" for "X_Client" is answered with the real name. Separators and case do not count; preferred
    // items (the mod's) come first on ties. Nothing is offered when nothing is close.
    public static List<T> Nearest<T>(IEnumerable<T> items, Func<T, string> path, Func<T, bool> preferred, string query, int max = 3)
    {
        string wanted = Spelling(Segments(query).LastOrDefault() ?? "");
        if (wanted.Length == 0)
            return [];
        // Below the name's own length, so a short name is never 'near' every other short name.
        int limit = Math.Min(Math.Max(2, wanted.Length / 4), (wanted.Length - 1) / 2);
        if (limit < 1)
            return [];
        return items
            .Select(i => (Item: i, Distance: Distance(wanted, path(i), limit)))
            .Where(c => c.Distance <= limit)
            .OrderBy(c => c.Distance)
            .ThenBy(c => preferred(c.Item) ? 0 : 1)
            .ThenBy(c => path(c.Item), StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(c => c.Item)
            .ToList();
    }

    // The last segment with and without its extension, so "X.Client" meets "X_Client.partialdef" either way.
    private static int Distance(string wanted, string candidatePath, int limit)
    {
        string last = Segments(candidatePath).LastOrDefault() ?? "";
        return Math.Min(Levenshtein(wanted, Spelling(last), limit), Levenshtein(wanted, Spelling(WithoutExtension(last)), limit));
    }

    private static string Spelling(string name)
    {
        var chars = name.ToLowerInvariant().Where(c => c is not ('.' or '_' or '-' or ' ')).ToArray();
        return new string(chars);
    }

    // Edit distance, given up (limit + 1) once it cannot stay within the limit.
    private static int Levenshtein(string a, string b, int limit)
    {
        if (Math.Abs(a.Length - b.Length) > limit)
            return limit + 1;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            int best = current[0];
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                best = Math.Min(best, current[j]);
            }
            if (best > limit)
                return limit + 1;
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    // find_content: every word of the query is part of the file name or the definition type or one of its base types.
    public static bool QueryMatches(string query, string fileName, IReadOnlyList<string> typeNames)
    {
        string[] words = query.Split([' ', '\t', '/', '\\', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(w =>
            fileName.Contains(w, StringComparison.OrdinalIgnoreCase)
            || typeNames.Any(t => t.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    private static string WithoutExtension(string path)
    {
        int dot = path.LastIndexOf('.');
        return dot > path.LastIndexOf('\\') ? path[..dot] : path;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
