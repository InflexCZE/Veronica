using System.Globalization;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.ObjectBuilders;
using Keen.VRage.Library.UI.PropertyInspector.Visibility;
using Keen.VRage.UI.Shared.ObjectInspector;

namespace Veronica.Editing;

// Adding and removing elements of lists and dictionaries.
public static class Elements
{
    public const int MaxTypeChoices = 50;

    // Adds a default element: appended to a list, or under key in a dictionary or keyed list.
    // Returns its path and its fields, ready for write.
    public static string Add(MemberReference root, string path, string? key, string? typeName, IContentLinks links)
    {
        if (path.Trim().Length == 0)
            throw new EditError("Give the path of the list or dictionary, e.g. 'Dummies'.");
        return FieldWriter.CreatingParents(root, path, (collection, canonical) => AddTo(collection, canonical, key, typeName, links));
    }

    private static string AddTo(MemberReference collection, string canonical, string? key, string? typeName, IContentLinks links)
    {
        if (collection.MemberKind is not (MemberValueKind.List or MemberValueKind.Dictionary))
            throw new EditError($"'{canonical}' is not a list or dictionary.");
        RequireEditable(collection, canonical);

        bool dictionary = collection.MemberKind == MemberValueKind.Dictionary;
        Type? keyType = dictionary
            ? Fields.DictionaryTypes(collection.DeclaredMemberType)?.Key
            : Fields.GeneratesOwnKeys(collection.DeclaredMemberType) ? null : Fields.KeyedListKeyType(collection.DeclaredMemberType);
        Type? elementType = dictionary
            ? Fields.DictionaryTypes(collection.DeclaredMemberType)?.Value
            : Fields.ElementType(collection.DeclaredMemberType);
        if (elementType == null || (dictionary && keyType == null))
            throw new EditError($"The element type of '{canonical}' cannot be read.");

        object? parsedKey = null;
        if (keyType != null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new EditError($"'{canonical}' is keyed; pass key for the new element.");
            parsedKey = Values.FromText(key, keyType);
        }

        object? element = Create(elementType, typeName, canonical);

        if (collection.GetValue<object?>() is null)
        {
            if (!Fields.IsWritable(collection))
                throw new EditError($"'{canonical}' is null and read-only.");
            collection.SetNew();
        }

        string path;
        if (dictionary)
        {
            if (collection.DictionaryContainsKey(parsedKey!))
                throw new EditError($"'{canonical}' already has key '{key}'; write to {FieldPath.Element(canonical, Fields.KeyText(parsedKey))} instead.");
            collection.AddToDictionary(parsedKey!, element);
            path = FieldPath.Element(canonical, Fields.KeyText(parsedKey));
            return $"Added {path}:\n{FieldReader.Dump(collection.GetDictionaryItem(parsedKey!), path, links)}";
        }

        int index = collection.GetCollectionCount();
        if (parsedKey != null)
        {
            if (collection.KeyedListContainsKey(parsedKey))
                throw new EditError($"'{canonical}' already has key '{key}'.");
            collection.InsertIntoKeyedList(index, parsedKey, element);
        }
        else
        {
            collection.InsertIntoList(index, element);
        }

        path = FieldPath.Element(canonical, index.ToString(CultureInfo.InvariantCulture));
        return $"Added {path}:\n{FieldReader.Dump(collection.GetListItem(index), path, links)}";
    }

    // Removes the element at a path read prints, e.g. Dummies[1] or Classes[Ion].
    public static string Remove(MemberReference root, string path)
    {
        if (path.Trim().Length == 0)
            throw new EditError("Give the path of the element to remove, e.g. 'Dummies[1]'.");

        var (element, canonical) = FieldWalker.Walk(root, path);
        if (!element.IsCollectionItem)
            throw new EditError($"'{canonical}' is not a list or dictionary element; remove takes a path like 'Dummies[1]'.");

        var collection = element.GetParent();
        RequireEditable(collection, canonical[..canonical.LastIndexOf('[')]);

        bool list = collection.MemberKind == MemberValueKind.List;
        element.RemoveCollectionItem();

        int left = collection.GetCollectionCount();
        return list
            ? $"Removed {canonical}; the elements after it moved up one index, {left} left."
            : $"Removed {canonical}; {left} left.";
    }

    // A collection without a setter is still edited in place; the inspector locks only marked ones.
    private static void RequireEditable(MemberReference collection, string path)
    {
        if (collection.GetMemberAttribute<ReadOnlyInInspectorAttribute>() != null)
            throw new EditError($"'{path}' is read-only.");
    }

    private static object? Create(Type elementType, string? typeName, string path)
    {
        // A definition element is a reference: it starts empty and is pointed at an item with write.
        if (typeof(Definition).IsAssignableFrom(elementType))
            return null;

        Type type = PickType(elementType, typeName);
        try
        {
            return type == typeof(string) ? "" : Activator.CreateInstance(type);
        }
        catch (Exception e) when (e is MissingMethodException or MemberAccessException or ArgumentException or NotSupportedException)
        {
            throw new EditError($"Elements of '{path}' ({Fields.TypeLabel(type)}) cannot be created empty.");
        }
    }

    private static Type PickType(Type elementType, string? typeName)
    {
        if (!elementType.IsAbstract && !elementType.IsInterface)
            return elementType;

        var choices = ConcreteTypes(elementType);
        if (choices.Count == 0)
            throw new EditError($"The Editor offers no {Fields.TypeLabel(elementType)} type to add.");

        if (string.IsNullOrWhiteSpace(typeName))
            throw new EditError($"Elements are {Fields.TypeLabel(elementType)}; pass type, one of: {string.Join(", ", choices.Select(c => c.Name))}.");

        string wanted = typeName.Trim();
        var byName = choices.Where(c => string.Equals(c.Name, wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        if (byName.Count == 1)
            return byName[0];
        var byFullName = choices.Where(c => string.Equals(c.FullName, wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        if (byFullName.Count == 1)
            return byFullName[0];

        bool collide = byName.Count > 1;
        throw new EditError($"'{wanted}' is {(collide ? "ambiguous" : "not one of the types")}; use one of: {string.Join(", ", (collide ? byName : choices).Select(c => collide ? c.FullName : c.Name))}.");
    }

    // The choices the inspector's add button offers for the collection.
    internal static List<Type> ConcreteTypes(Type elementType) =>
        TypeOptionManager.FindOptions(elementType, IsElementChoice, null, $"Veronica_Element_{elementType}")
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxTypeChoices)
            .ToList();

    private static bool IsElementChoice(Type elementType, Type candidate)
    {
        if (candidate.IsAbstract || candidate.ContainsGenericParameters)
            return false;
        if (candidate.IsAssignableTo(typeof(Definition)) || candidate.IsAssignableTo(typeof(IObjectBuilder)))
            return candidate == elementType;
        return true;
    }
}
