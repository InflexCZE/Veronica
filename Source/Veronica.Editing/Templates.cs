namespace Veronica.Editing;

// Prefab templates are named by their browser path, e.g. "Blocks/Breakable/Ion Thrusters".
public static class Templates
{
    public const int MaxListed = 50;

    // The first tier with any match wins: exact path, exact last segment, every word present.
    public static List<string> Resolve(IEnumerable<string> templates, string query)
    {
        string[] wanted = Words(query);
        if (wanted.Length == 0)
            return [];

        var all = templates.ToList();
        var exact = all.Where(t => Words(t).SequenceEqual(wanted)).ToList();
        if (exact.Count > 0)
            return exact;

        var leaf = all.Where(t => Words(LastSegment(t)).SequenceEqual(wanted)).ToList();
        if (leaf.Count > 0)
            return leaf;

        return Sorted(all.Where(t => wanted.All(Words(t).Contains)));
    }

    // Discovery is looser than create_prefab: a filter word only has to be part of the path ("thrust").
    public static List<string> Filter(IEnumerable<string> templates, string? filter)
    {
        string[] wanted = Words(filter ?? "");
        return Sorted(templates.Where(t => wanted.All(w => t.Contains(w, StringComparison.OrdinalIgnoreCase))));
    }

    private static List<string> Sorted(IEnumerable<string> templates) =>
        templates.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();

    private static string[] Words(string text) =>
        text.Split(['/', '\\', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(w => w.ToLowerInvariant()).ToArray();

    private static string LastSegment(string path) => path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
}
