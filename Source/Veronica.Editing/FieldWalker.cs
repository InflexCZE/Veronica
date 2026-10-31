using System.Globalization;
using Keen.VRage.Library.Serialization;

namespace Veronica.Editing;

public static class FieldWalker
{
    private const int MaxKeysListed = 30;

    // Resolves a field path from the definition root. Path is the canonical form of what was walked.
    // basePath is how paths print above root, when root is not the definition's own root.
    public static (MemberReference Member, string Path) Walk(MemberReference root, string path, string basePath = "", IContentLinks? links = null) => Walk(root, path, null, basePath, links);

    // For writes: null objects on the way are created like mkdir -p, and listed so a failed write can undo them.
    public static (MemberReference Member, string Path) Walk(MemberReference root, string path, List<(MemberReference Member, string Path)>? created, string basePath = "", IContentLinks? links = null)
    {
        var member = root;
        string canonical = basePath;
        foreach (var segment in FieldPath.Parse(path))
        {
            switch (member.MemberKind)
            {
                case MemberValueKind.List:
                    (member, canonical) = StepIntoList(member, canonical, segment);
                    break;
                case MemberValueKind.Dictionary:
                    (member, canonical) = StepIntoDictionary(member, canonical, segment.Text, links);
                    break;
                case MemberValueKind.Value:
                    if (segment.Bracketed)
                        throw new EditError($"{Where(canonical)} is not a list or dictionary; remove the [{segment.Text}].");
                    // A reference holds the engine's runtime object, not the referenced file's data; walking it read
                    // placeholders and uninitialized arrays.
                    if (!member.IsRoot && Fields.GetReference(member) != null)
                        throw new EditError($"'{canonical}' is a reference to another item; read \"{canonical}\" to see which, then read that item for its fields.");
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
        // A transform's quaternion is never addressed: its angles are the Euler path (Transforms.WalkEuler).
        bool transform = Fields.TryGetTransform(parent, out var position, out var orientation);
        if (transform)
            visible.RemoveAll(m => m.Name == orientation.Name);

        var exact = visible.Where(m => m.Name == name).ToList();
        if (exact.Count == 1)
            return exact[0];

        var loose = visible.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (loose.Count == 1)
            return loose[0];

        // A transform wrapper lists Position as its own field, so it is reached through the wrapper too.
        if (transform && string.Equals(position.Name, name, StringComparison.OrdinalIgnoreCase))
            return position;

        string fields = transform ? Transforms.FieldNames(position)
            : visible.Count == 0 ? "(none)" : string.Join(", ", visible.Select(m => m.Name));
        throw new EditError($"'{name}' is not a field of {Where(parentPath)}. Fields: {fields}.");
    }

    private static (MemberReference, string) StepIntoList(MemberReference list, string path, PathSegment segment)
    {
        if (!segment.Bracketed)
            throw new EditError($"'{path}' is a list; address an element like '{path}[0]'.");
        RequireValue(list, path);

        int count = Fields.CollectionCount(list);
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

    private static (MemberReference, string) StepIntoDictionary(MemberReference dictionary, string path, string keyText, IContentLinks? links)
    {
        RequireValue(dictionary, path);
        var types = Fields.DictionaryTypes(dictionary.CurrentMemberType)
            ?? throw new EditError($"'{path}' has a key type that cannot be read.");

        object key = Keys.Find(dictionary, keyText, types.Key, links)
            ?? throw new EditError($"'{path}' has no key '{keyText}'. Keys: {ListKeys(dictionary, links)}.");
        return (dictionary.GetDictionaryItem(key), FieldPath.Element(path, Keys.Text(key, links)));
    }

    public static string ListKeys(MemberReference dictionary, IContentLinks? links, int skip = 0)
    {
        var keys = dictionary.EnumerateDictionaryKeys().Skip(skip).Select(k => Keys.Text(k.GetValue<object?>(), links)).ToList();
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
