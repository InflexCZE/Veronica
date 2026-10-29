namespace Veronica.Editing;

// One step of a field path: a name after a dot, or the text between brackets.
public readonly record struct PathSegment(string Text, bool Bracketed);

public static class FieldPath
{
    private const string RootPrefix = "$Root";

    public static IReadOnlyList<PathSegment> Parse(string path)
    {
        string text = path.Trim();
        if (text == RootPrefix || text.StartsWith(RootPrefix + ".", StringComparison.Ordinal) || text.StartsWith(RootPrefix + "[", StringComparison.Ordinal))
            text = text[RootPrefix.Length..].TrimStart('.');

        var segments = new List<PathSegment>();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                int end = text.IndexOf(']', i + 1);
                if (end < 0)
                    throw new EditError($"'{path}' has a '[' without a closing ']'.");
                string token = text[(i + 1)..end].Trim();
                if (token.Length == 0)
                    throw new EditError($"'{path}' has empty brackets; put an index or key inside.");
                segments.Add(new PathSegment(token, true));
                i = end + 1;
            }
            else
            {
                int end = text.IndexOfAny(['.', '[', ']'], i);
                if (end < 0)
                    end = text.Length;
                string name = text[i..end].Trim();
                if (name.Length == 0 || (end < text.Length && text[end] == ']'))
                    throw new EditError($"'{path}' is not a valid field path, e.g. 'Dummies[0].Name'.");
                segments.Add(new PathSegment(name, false));
                i = end;
            }

            if (i < text.Length && text[i] == '.')
            {
                i++;
                if (i == text.Length || text[i] == '.')
                    throw new EditError($"'{path}' has an empty field name.");
            }
            else if (i < text.Length && text[i] != '[')
            {
                throw new EditError($"'{path}' is not a valid field path, e.g. 'Dummies[0].Name'.");
            }
        }

        return segments;
    }

    public static string Join(IEnumerable<PathSegment> segments)
    {
        string path = "";
        foreach (var segment in segments)
            path = segment.Bracketed ? Element(path, segment.Text) : Member(path, segment.Text);
        return path;
    }

    public static string Member(string parent, string name) => parent.Length == 0 ? name : $"{parent}.{name}";

    public static string Element(string parent, string token) => $"{parent}[{token}]";
}
