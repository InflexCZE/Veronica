using System.Globalization;
using Keen.VRage.Library.Serialization;

namespace Veronica.Editing;

public static class FieldWalker
{
    private const int MaxKeysListed = 30;

    // Resolves a field path from the definition root. Path is the canonical form of what was walked.
    public static (MemberReference Member, string Path) Walk(MemberReference root, string path) => Walk(root, path, null);

    // For writes: null objects on the way are created like mkdir -p, and listed so a failed write can undo them.
    public static (MemberReference Member, string Path) Walk(MemberReference root, string path, List<(MemberReference Member, string Path)>? created)
    {
        var member = root;
        string canonical = "";
        foreach (var segment in FieldPath.Parse(path))
        {
            switch (member.MemberKind)
            {
                case MemberValueKind.List:
                    (member, canonical) = StepIntoList(member, canonical, segment);
                    break;
                case MemberValueKind.Dictionary:
                    (member, canonical) = StepIntoDictionary(member, canonical, segment.Text);
                    break;
                case MemberValueKind.Value:
                    if (segment.Bracketed)
                        throw new EditError($"{Where(canonical)} is not a list or dictionary; remove the [{segment.Text}].");
                    if (created != null && !member.IsRoot && member.GetValue<object?>() is null)
                        Create(member, canonical, created);
                    RequireValue(member, canonical);
                    member = Child(member, canonical, segment.Text);
                    canonical = FieldPath.Member(canonical, member.Name);
                    member = Fields.Unwrap(member);
                    break;
                default:
                    throw new EditError($"'{canonical}' is a {member.MemberKind.ToString().ToLowerInvariant()}; its items cannot be addressed.");
            }
        }
        return (member, canonical);
    }

    private static void Create(MemberReference member, string path, List<(MemberReference, string)> created)
    {
        Type type = Fields.ValueType(member);
        if (type.IsAbstract || type.IsInterface)
        {
            var choices = Elements.ConcreteTypes(type);
            throw new EditError($"'{path}' is null and holds a {Fields.TypeLabel(type)}, which cannot be created here"
                + (choices.Count == 0 ? "." : $" (types: {string.Join(", ", choices.Select(c => c.Name))})."));
        }
        if (!Fields.IsWritable(member))
            throw new EditError($"'{path}' is null and read-only.");

        try
        {
            member.SetNew(initializeNullableValueTypes: true);
        }
        catch (Exception e) when (e is MissingMethodException or MemberAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            throw new EditError($"'{path}' is null and a {Fields.TypeLabel(type)} cannot be created empty.");
        }
        created.Add((member, path));
    }

    private static MemberReference Child(MemberReference parent, string parentPath, string name)
    {
        var visible = Fields.VisibleMembers(parent);
        var exact = visible.Where(m => m.Name == name).ToList();
        if (exact.Count == 1)
            return exact[0];

        var loose = visible.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (loose.Count == 1)
            return loose[0];

        string fields = visible.Count == 0 ? "(none)" : string.Join(", ", visible.Select(m => m.Name));
        throw new EditError($"'{name}' is not a field of {Where(parentPath)}. Fields: {fields}.");
    }

    private static (MemberReference, string) StepIntoList(MemberReference list, string path, PathSegment segment)
    {
        if (!segment.Bracketed)
            throw new EditError($"'{path}' is a list; address an element like '{path}[0]'.");
        RequireValue(list, path);

        int count = list.GetCollectionCount();
        if (int.TryParse(segment.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
        {
            if (index < 0 || index >= count)
                throw new EditError(count == 0
                    ? $"'{path}' is empty."
                    : $"'{path}' has {count} element{(count == 1 ? "" : "s")}, [0] to [{count - 1}].");
            return (list.GetListItem(index), FieldPath.Element(path, index.ToString(CultureInfo.InvariantCulture)));
        }

        if (Fields.KeyedListKeyType(list.CurrentMemberType) is { } keyType)
        {
            object key = Values.FromText(segment.Text, keyType);
            if (!list.KeyedListContainsKey(key))
                throw new EditError($"'{path}' has no element with key '{segment.Text}'.");
            var item = list.GetKeyedListItem(key);
            return (item, FieldPath.Element(path, item.GetListIndex().ToString(CultureInfo.InvariantCulture)));
        }

        throw new EditError(count == 0
            ? $"'{segment.Text}' is not an index, and '{path}' is empty."
            : $"'{segment.Text}' is not an index; '{path}' takes [0] to [{count - 1}].");
    }

    private static (MemberReference, string) StepIntoDictionary(MemberReference dictionary, string path, string keyText)
    {
        RequireValue(dictionary, path);
        var types = Fields.DictionaryTypes(dictionary.CurrentMemberType)
            ?? throw new EditError($"'{path}' has a key type that cannot be read.");

        object key = Values.FromText(keyText, types.Key);
        if (!dictionary.DictionaryContainsKey(key))
            throw new EditError($"'{path}' has no key '{keyText}'. Keys: {ListKeys(dictionary)}.");
        return (dictionary.GetDictionaryItem(key), FieldPath.Element(path, Fields.KeyText(key)));
    }

    public static string ListKeys(MemberReference dictionary, int skip = 0)
    {
        var keys = dictionary.EnumerateDictionaryKeys().Skip(skip).Select(k => Fields.KeyText(k.GetValue<object?>())).ToList();
        if (keys.Count == 0)
            return "(none)";
        string shown = string.Join(", ", keys.Take(MaxKeysListed));
        return keys.Count > MaxKeysListed ? shown + ", ..." : shown;
    }

    private static void RequireValue(MemberReference member, string path)
    {
        if (!member.IsRoot && member.GetValue<object?>() is null)
            throw new EditError($"'{path}' is null.");
    }

    private static string Where(string path) => path.Length == 0 ? "the definition" : $"'{path}'";
}
