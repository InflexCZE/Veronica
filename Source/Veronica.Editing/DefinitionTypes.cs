namespace Veronica.Editing;

// How create_definition names a definition type: its short name, with or without the "Definition" suffix.
public static class DefinitionTypes
{
    public const int MaxListed = 20;
    private const string Suffix = "Definition";

    public static Type Pick(IReadOnlyCollection<Type> choices, string query)
    {
        string wanted = query.Trim();
        if (wanted.Length == 0)
            throw new EditError("Give a definition type, e.g. BlockKindDefinition.");

        var exact = choices.Where(t => Same(t.Name, wanted) || Same(t.Name, wanted + Suffix) || Same(t.FullName, wanted)).ToList();
        if (exact.Count == 1)
            return exact[0];
        if (exact.Count > 1)
            throw new EditError($"'{wanted}' is ambiguous; use one of: {string.Join(", ", exact.Select(t => t.FullName).Order(StringComparer.OrdinalIgnoreCase))}.");

        string[] words = wanted.Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries);
        var near = choices.Where(t => words.All(w => t.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.Name).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (near.Count == 0)
            throw new EditError($"No definition type is named '{wanted}', and none contains its words; try one word of the type name.");
        string listed = string.Join(", ", near.Take(MaxListed));
        string more = near.Count > MaxListed ? $", ... ({near.Count - MaxListed} more; add a word)" : "";
        throw new EditError($"No definition type is named '{wanted}'; types containing its words: {listed}{more}.");
    }

    private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
