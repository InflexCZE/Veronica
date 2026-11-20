using System.Runtime.CompilerServices;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.ObjectBuilders;
using Keen.VRage.Library.UI.Attributes;

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

    // The interfaces describe offers that a type implements.
    IReadOnlyList<Type> Interfaces(Type type);
}

public static class TypeCatalog
{
    // Engine plumbing nearly every component implements and no component asks for as a dependency: listing them
    // would fill every component's description with names a modder never uses.
    public static readonly IReadOnlySet<string> PlumbingInterfaces = new HashSet<string>(StringComparer.Ordinal)
    {
        "Keen.VRage.Core.Game.Systems.IInSceneListener",
        "Keen.VRage.Core.Game.Systems.ISessionConfigurator",
        "Keen.VRage.Core.Game.Systems.IEntityBundleListener",
        "Keen.VRage.Library.Serialization.IAccessible",
        "Keen.VRage.Core.DebugMenu.IDebugMenuEvents",
        "Keen.VRage.Core.Game.GameSystems.SpaceProbe.IDebugListenerAcceptor",
        "Keen.VRage.Multiplayer.Transport.OutOfProcess.Server.IConnectedClientsListener",
        "Keen.VRage.Library.Definitions.IDefinitionLoadingServiceProvider",
        "Keen.Game2.Simulation.RuntimeSystems.CoreScenes.ITransitionListener",
    };

    public static bool IsPlumbing(Type type) => type.IsInterface && type.FullName is { } name && PlumbingInterfaces.Contains(name);

    // An interface describe offers: a public game interface that is not plumbing.
    public static bool IsOffered(Type type) =>
        type.IsInterface && type.IsVisible && !type.ContainsGenericParameters && type.Namespace?.StartsWith("Keen.", StringComparison.Ordinal) == true && !IsPlumbing(type);

    // The game keeps such types only so old files still load; they share names with the real types.
    public static bool IsLeftover(Type type)
    {
        if (type.IsVisible)
            return false;
        try
        {
            return type.GetCustomAttributes(typeof(TypeSearchInfo), inherit: false).OfType<TypeSearchInfo>().Any(a => a.HideInCreationMenu);
        }
        catch (Exception)
        {
            return false;
        }
    }

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
