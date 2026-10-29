namespace Veronica.Editing;

public sealed record ComponentEntry(Guid Id, string TypeName, IReadOnlyCollection<string> Tags);

// How tools name a prefab's components; the model never sees their Guids. A component goes by its type's short name,
// by a tag of its own when its type is there more than once, else by the shortest unique start of its Guid.
public static class ComponentNames
{
    public const string Collection = "Components";
    private const int MinHex = 8;
    private const string Suffix = "Component";

    public static string PathOf(string name) => FieldPath.Element(Collection, name);

    public static Dictionary<Guid, string> Assign(IReadOnlyList<ComponentEntry> components)
    {
        var byType = components.GroupBy(c => c.TypeName, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<Guid, string>();
        // A tag spelled like a type present, with or without the suffix, would read as that type.
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in components)
        {
            taken.Add(c.TypeName);
            if (c.TypeName.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
                taken.Add(c.TypeName[..^Suffix.Length]);
        }

        foreach (var c in components.Where(c => byType[c.TypeName] == 1))
            names[c.Id] = c.TypeName;

        foreach (var c in components.Where(c => !names.ContainsKey(c.Id)))
        {
            if (c.Tags.Order(StringComparer.Ordinal).FirstOrDefault(t => t.Length > 0 && !t.StartsWith('#') && !taken.Contains(t)) is { } tag)
            {
                names[c.Id] = tag;
                taken.Add(tag);
            }
        }

        foreach (var c in components.Where(c => !names.ContainsKey(c.Id)))
            names[c.Id] = "#" + Hex(c.Id, components);

        return names;
    }

    public static ComponentEntry Find(IReadOnlyList<ComponentEntry> components, string token)
    {
        string wanted = token.Trim();
        var names = Assign(components);

        var named = components.Where(c => string.Equals(names[c.Id], wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 1)
            return named[0];

        if (wanted.StartsWith('#'))
        {
            string hex = wanted[1..].Replace("-", "");
            var started = components.Where(c => hex.Length > 0 && c.Id.ToString("N").StartsWith(hex, StringComparison.OrdinalIgnoreCase)).ToList();
            if (started.Count == 1)
                return started[0];
        }

        var typed = components.Where(c => string.Equals(c.TypeName, wanted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.TypeName, wanted + Suffix, StringComparison.OrdinalIgnoreCase)).ToList();
        if (typed.Count == 1)
            return typed[0];
        if (typed.Count > 1)
            throw new EditError($"There are {typed.Count} {typed[0].TypeName}s; name one: {string.Join(", ", typed.Select(c => PathOf(names[c.Id])))}.");

        var tagged = components.Where(c => c.Tags.Contains(wanted, StringComparer.OrdinalIgnoreCase)).ToList();
        if (tagged.Count == 1)
            return tagged[0];

        throw new EditError(components.Count == 0
            ? $"There is no component '{wanted}'; the entity has none."
            : $"There is no component '{wanted}'. Components: {string.Join(", ", components.Select(c => names[c.Id]).Order(StringComparer.OrdinalIgnoreCase))}.");
    }

    // A path under Components splits into the component's name and the path inside it; null for any other path.
    public static (string Name, string Inner)? Split(string path)
    {
        var segments = FieldPath.Parse(path);
        if (segments.Count < 2 || segments[0].Bracketed || !string.Equals(segments[0].Text, Collection, StringComparison.OrdinalIgnoreCase))
            return null;
        return (segments[1].Text, FieldPath.Join(segments.Skip(2)));
    }

    public static bool IsCollection(string path)
    {
        var segments = FieldPath.Parse(path);
        return segments.Count == 1 && !segments[0].Bracketed && string.Equals(segments[0].Text, Collection, StringComparison.OrdinalIgnoreCase);
    }

    private static string Hex(Guid id, IReadOnlyList<ComponentEntry> components)
    {
        string own = id.ToString("N");
        var others = components.Where(c => c.Id != id).Select(c => c.Id.ToString("N")).ToList();
        int length = MinHex;
        while (length < own.Length && others.Any(o => o.StartsWith(own[..length], StringComparison.Ordinal)))
            length++;
        return own[..length];
    }
}
