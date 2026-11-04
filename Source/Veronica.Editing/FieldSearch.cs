namespace Veronica.Editing;

public static class FieldSearch
{
    public const int MaxMatches = 20;

    public static string Search(IReadOnlyList<FieldEntry> entries, string value, IReadOnlySet<Guid> items, string scope)
    {
        string wanted = Unquote(value.Trim());
        var exact = entries.Where(e => e.Link is { } id && items.Contains(id) || Forms(e).Any(f => string.Equals(f, wanted, StringComparison.OrdinalIgnoreCase))).ToList();
        if (exact.Count > 0)
            return List(exact);

        var similar = wanted.Length == 0 ? [] : entries.Where(e => e.Text.Contains(wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        if (similar.Count > 0)
            return "No exact match; similar:\n" + List(similar);

        string searched = entries.Count == 1 ? "1 value searched" : $"{entries.Count} values searched";
        return $"Nothing in {scope} matches \"{wanted}\" ({searched}).";
    }

    private static string List(List<FieldEntry> matches)
    {
        var lines = matches.Take(MaxMatches).Select(m => m.Line).ToList();
        if (matches.Count > MaxMatches)
            lines.Add($"... and {matches.Count - MaxMatches} more, not shown. Only the first {MaxMatches} of {matches.Count} matches are listed: narrow the search with a path or a more exact value.");
        return string.Join("\n", lines);
    }

    // A reference prints as the item's path; its file name, with or without extension, names it as well.
    private static IEnumerable<string> Forms(FieldEntry entry)
    {
        string text = Unquote(entry.Text);
        yield return text;
        if (entry.Link == null)
            yield break;
        string name = text[(text.LastIndexOfAny(['/', '\\']) + 1)..];
        yield return name;
        yield return Path.GetFileNameWithoutExtension(name);
    }

    private static string Unquote(string text) =>
        text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;
}
