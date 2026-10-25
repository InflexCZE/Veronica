using Keen.VRage.Library.Serialization;

namespace Veronica.Editing;

// Indented text, one "Path = value (type)" line per field. Every path is usable in read and write as printed.
public sealed class FieldReader
{
    public const int MaxLines = 150;
    public const int MaxElements = 5;
    public const int MaxDepth = 3;

    private readonly IContentLinks _links;
    private readonly int _maxLines;
    private readonly List<string> _lines = [];
    private bool _truncated;

    private FieldReader(IContentLinks links, int maxLines)
    {
        _links = links;
        _maxLines = maxLines;
    }

    public static string Read(MemberReference root, string path, IContentLinks links, int maxLines = MaxLines)
    {
        var (member, canonical) = path.Trim().Length == 0 ? (root, "") : FieldWalker.Walk(root, path);
        return Dump(member, canonical, links, maxLines);
    }

    public static string Dump(MemberReference member, string path, IContentLinks links, int maxLines = MaxLines)
    {
        var reader = new FieldReader(links, maxLines);
        reader.DumpRequested(member, path);
        return reader.Text();
    }

    // One line for a field, as write reports it: the value, or the struct inline, or the collection size.
    public static string Describe(MemberReference member, IContentLinks links)
    {
        if (member.MemberKind != MemberValueKind.Value)
        {
            object? collection = member.GetValue<object?>();
            return collection is null ? "null" : $"{member.MemberKind.ToString().ToLowerInvariant()} of {member.GetCollectionCount()}";
        }
        if (Fields.IsScalar(Fields.ValueType(member)))
            return RenderScalar(member, links);
        if (Fields.TryGetInlineFields(member, out var fields))
            return RenderInline(fields, links);
        return member.GetValue<object?>() is null ? "null" : $"{{{string.Join(", ", Fields.VisibleMembers(member).Select(m => m.Name))}}}";
    }

    private void DumpRequested(MemberReference member, string path)
    {
        if (member.IsRoot)
        {
            DumpFields(member, path, 0);
            return;
        }

        if (member.MemberKind != MemberValueKind.Value)
        {
            DumpCollection(member, path, 0);
            return;
        }

        if (Fields.IsScalar(Fields.ValueType(member)))
        {
            ScalarLine(member, path);
            if (Fields.ValueType(member) is { IsEnum: true } enumType)
                Add($"  values: {string.Join(", ", Enum.GetNames(enumType))}");
            return;
        }

        if (member.GetValue<object?>() is null || Fields.TryGetInlineFields(member, out _))
        {
            DumpField(member, path, 0);
            return;
        }

        DumpFields(member, path, 0);
    }

    private void DumpFields(MemberReference parent, string path, int depth)
    {
        foreach (var child in Fields.VisibleMembers(parent))
        {
            if (_truncated)
                return;
            var field = Fields.Unwrap(child);
            if (Fields.IsComputed(field))
                continue;
            DumpField(field, FieldPath.Member(path, child.Name), depth);
        }
    }

    private void DumpField(MemberReference member, string path, int depth)
    {
        try
        {
            if (member.MemberKind != MemberValueKind.Value)
            {
                DumpCollection(member, path, depth);
                return;
            }

            if (Fields.IsScalar(Fields.ValueType(member)))
            {
                ScalarLine(member, path);
                return;
            }

            string label = Label(member);
            if (member.GetValue<object?>() is null)
            {
                Add($"{path} = null ({label})");
                return;
            }

            if (Fields.TryGetInlineFields(member, out var fields))
            {
                Add($"{path} = {RenderInline(fields, _links)} ({label})");
                return;
            }

            if (depth >= MaxDepth)
            {
                Add($"{path}: {label} {{{string.Join(", ", Fields.VisibleMembers(member).Select(m => m.Name))}}} (read \"{path}\")");
                return;
            }

            DumpFields(member, path, depth + 1);
        }
        catch (Exception e) when (e is not EditError)
        {
            Add($"{path} = (unreadable)");
        }
    }

    private void DumpCollection(MemberReference member, string path, int depth)
    {
        if (member.GetValue<object?>() is null)
        {
            Add($"{path} = null ({Label(member)})");
            return;
        }

        int count = member.GetCollectionCount();
        switch (member.MemberKind)
        {
            case MemberValueKind.List:
            {
                string element = Fields.ElementType(member.CurrentMemberType) is { } type ? Fields.TypeLabel(type) : "?";
                Add(count == 0 ? $"{path}: empty list of {element}" : $"{path}: list of {count} {element}");
                int shown = Math.Min(count, MaxElements);
                for (int i = 0; i < shown && !_truncated; i++)
                    DumpField(member.GetListItem(i), FieldPath.Element(path, i.ToString()), depth + 1);
                if (shown < count)
                    Add($"{path}: ... (+{count - shown} more, read \"{FieldPath.Element(path, shown.ToString())}\" to \"{FieldPath.Element(path, (count - 1).ToString())}\" one by one)");
                break;
            }
            case MemberValueKind.Dictionary:
            {
                Add(count == 0 ? $"{path}: empty dictionary" : $"{path}: dictionary of {count}");
                var keys = member.EnumerateDictionaryKeys().Select(k => k.GetValue<object?>()).ToList();
                int shown = Math.Min(keys.Count, MaxElements);
                for (int i = 0; i < shown && !_truncated; i++)
                    DumpField(member.GetDictionaryItem(keys[i]!), FieldPath.Element(path, Fields.KeyText(keys[i])), depth + 1);
                if (shown < keys.Count)
                    Add($"{path}: ... (+{keys.Count - shown} more: {FieldWalker.ListKeys(member, shown)}; read \"{path}[<key>]\")");
                break;
            }
            default:
                Add($"{path}: {member.MemberKind.ToString().ToLowerInvariant()} of {count} (not readable here)");
                break;
        }
    }

    private void ScalarLine(MemberReference member, string path)
    {
        string label = Label(member) + (Fields.IsWritable(member) ? "" : ", read-only");
        string value;
        try
        {
            value = RenderScalar(member, _links);
        }
        catch (Exception)
        {
            value = "(unreadable)";
        }
        Add($"{path} = {value} ({label})");
    }

    private static string RenderScalar(MemberReference member, IContentLinks links)
    {
        var reference = Fields.GetReference(member);
        object? value = member.GetValue<object?>();
        if (reference != null && value is null)
            return "none";
        return Values.Render(value, links, reference?.Kind == ReferenceKind.AssetGuid);
    }

    private static string RenderInline(List<MemberReference> fields, IContentLinks links) =>
        $"{{{string.Join(", ", fields.Select(f => $"{f.Name}: {RenderScalar(f, links)}"))}}}";

    private static string Label(MemberReference member)
    {
        Type declared = member.DeclaredMemberType;
        Type current = member.CurrentMemberType;
        string label = Fields.TypeLabel(declared);
        // Polymorphic fields show what they hold, since that decides their fields.
        return current != declared && Nullable.GetUnderlyingType(declared) != current ? $"{label}: {Fields.TypeLabel(current)}" : label;
    }

    private void Add(string line)
    {
        if (_truncated)
            return;
        if (_lines.Count == _maxLines)
        {
            _truncated = true;
            return;
        }
        _lines.Add(line);
    }

    private string Text()
    {
        if (_lines.Count == 0)
            _lines.Add("(no visible fields)");
        if (_truncated)
            _lines.Add($"... output cut at {_maxLines} lines; read a narrower path.");
        return string.Join("\n", _lines);
    }
}
