namespace Veronica.Editing;

// How tools name an engine type: its short name, with or without its kind's suffix ("Definition", "Component").
public static class DefinitionTypes
{
    public const int MaxListed = 20;

    public static Type Pick(IReadOnlyCollection<Type> choices, string query) =>
        Pick(choices, query, "Definition", "definition type", "BlockKindDefinition");

    public static Type Pick(IReadOnlyCollection<Type> choices, string query, string suffix, string noun, string example)
    {
        string wanted = query.Trim();
        if (wanted.Length == 0)
            throw new EditError($"Give a {noun}, e.g. {example}.");

        choices = choices.Where(t => !TypeCatalog.IsLeftover(t)).ToList();
        var exact = choices.Where(t => Same(t.Name, wanted) || Same(t.Name, wanted + suffix) || Same(t.FullName, wanted)).ToList();
        if (exact.Count == 1)
            return exact[0];
        // An internal type sharing a public type's name is not one content uses, even without the leftover mark.
        var visible = exact.Where(t => t.IsVisible).ToList();
        if (visible.Count == 1)
            return visible[0];
        if (exact.Count > 1)
            throw new EditError($"'{wanted}' is ambiguous; use one of: {string.Join(", ", exact.Select(t => t.FullName).Order(StringComparer.OrdinalIgnoreCase))}.");

        string[] words = wanted.Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries);
        var near = choices.Where(t => words.All(w => t.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.Name).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (near.Count == 0)
            throw new EditError($"No {noun} is named '{wanted}', and none contains its words; try one word of the type name.");
        string listed = string.Join(", ", near.Take(MaxListed));
        string more = near.Count > MaxListed ? $", ... ({near.Count - MaxListed} more; add a word)" : "";
        throw new EditError($"No {noun} is named '{wanted}'; types containing its words: {listed}{more}.");
    }

    private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
