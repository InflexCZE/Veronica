using System.Runtime.CompilerServices;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.ObjectBuilders;

namespace Veronica.Editing;

// The engine's side of describe: types by what tools call them, and how they relate.
public interface ITypeCatalog
{
    // The fields of a fresh instance of type, as read sees them; null when the type has no instance (abstract) or no fields.
    MemberReference? Fields(Type type);

    IReadOnlyList<Type> ConcreteTypes(Type type);

    // The definition type a component takes.
    Type? DefinitionOf(Type component);

    // The components that take a definition type.
    IReadOnlyList<Type> ComponentsTaking(Type definition);

    // The name tools use for a type: a definition or component for its object builder.
    Type Shown(Type type);

    string? Summary(Type type);
}

public static class TypeCatalog
{
    // Definitions and components are named as themselves, though their fields are their object builders'.
    public static Type Shown(Type type) => Buildable(type) is { } info && info.BuilderType == type ? info.ObjectType : type;

    public static Type? BuilderOf(Type type) => Buildable(type) is { } info && info.ObjectType == type ? info.BuilderType : null;

    public static bool IsBuilder(Type type) => Buildable(type) is { } info && info.BuilderType == type && info.ObjectType != type;

    private static BuildableTypeInfo? Buildable(Type type)
    {
        try
        {
            return ObjectBuilderIndexer.Instance.TryGetBuildableTypeInfo(type, out var info) ? info : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? Summary(Type type)
    {
        foreach (var candidate in new[] { type, BuilderOf(type) })
        {
            if (candidate != null && AccessibleTypeIndexer.Instance.TryGetAccessibleTypeInfo(candidate, out var info) && DocText.Render(info.Documentation) is { } text)
                return text;
        }
        return null;
    }

    public static bool IsAccessible(Type type) =>
        !type.ContainsGenericParameters && AccessibleTypeIndexer.Instance.TryGetAccessibleTypeInfo(type, out _);

    // A throwaway instance: its defaults show which nested objects exist, and nothing reads it afterwards.
    public static MemberReference? Instance(Type type, bool inPrefab = false)
    {
        if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters || !typeof(IAccessible).IsAssignableFrom(type))
            return null;
        object? instance;
        try
        {
            instance = Activator.CreateInstance(type, nonPublic: true);
        }
        catch (Exception)
        {
            instance = RuntimeHelpers.GetUninitializedObject(type);
        }
        if (instance is not IAccessible accessible)
            return null;
        if (inPrefab)
            Editing.Fields.ShowAsInPrefab(accessible);
        return new ObjectManipulator(accessible, trackChanges: false).Root;
    }
}
