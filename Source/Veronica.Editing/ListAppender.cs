using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.ObjectBuilders;
using Keen.VRage.UI.Shared.ObjectInspector;

namespace Veronica.Editing;

public static class ListAppender
{
    public const int MaxTypeChoices = 50;

    // Appends a default element. Returns its path and its fields, ready for write.
    public static string Append(MemberReference root, string listPath, string? typeName, IContentLinks links)
    {
        if (listPath.Trim().Length == 0)
            throw new EditError("Give the path of the list, e.g. 'Dummies'.");

        return FieldWriter.CreatingParents(root, listPath, (list, canonical) => AppendTo(list, canonical, typeName, links));
    }

    private static string AppendTo(MemberReference list, string canonical, string? typeName, IContentLinks links)
    {
        if (list.MemberKind != MemberValueKind.List)
            throw new EditError($"'{canonical}' is not a list.");
        // Keyed lists take a key with every element; the tools have no way to give one.
        if (Fields.KeyedListKeyType(list.DeclaredMemberType) != null)
            throw new EditError($"'{canonical}' is a keyed list; elements cannot be added to it here.");

        Type elementType = Fields.ElementType(list.DeclaredMemberType)
            ?? throw new EditError($"The element type of '{canonical}' cannot be read.");
        Type type = typeof(Definition).IsAssignableFrom(elementType) ? elementType : PickType(elementType, typeName);

        object? element;
        try
        {
            // A definition element is a reference: it starts empty and is pointed at an item with write.
            element = type == typeof(string) ? ""
                : typeof(Definition).IsAssignableFrom(type) ? null
                : Activator.CreateInstance(type);
        }
        catch (Exception e) when (e is MissingMethodException or MemberAccessException or ArgumentException or NotSupportedException)
        {
            throw new EditError($"Elements of '{canonical}' ({Fields.TypeLabel(type)}) cannot be created empty.");
        }

        if (list.GetValue<object?>() is null)
        {
            if (!Fields.IsWritable(list))
                throw new EditError($"'{canonical}' is null and read-only.");
            list.SetNew();
        }

        int index = list.GetCollectionCount();
        list.InsertIntoList(index, element);

        string path = FieldPath.Element(canonical, index.ToString());
        return $"Added {path}:\n{FieldReader.Dump(list.GetListItem(index), path, links)}";
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

    // The choices the inspector's add button offers for the list.
    internal static List<Type> ConcreteTypes(Type elementType) =>
        TypeOptionManager.FindOptions(elementType, IsElementChoice, null, $"Veronica_ListElement_{elementType}")
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
