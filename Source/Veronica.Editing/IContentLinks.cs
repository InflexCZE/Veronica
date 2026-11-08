using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;

namespace Veronica.Editing;

public enum ReferenceKind
{
    Definition,
    Handle,
    AssetGuid,
}

// What a reference field accepts. Field is there for project-side rules this layer cannot see.
public sealed record ReferenceSpec(MemberReference Field, ReferenceKind Kind, IReadOnlyList<Type> DefinitionTypes, IReadOnlyList<string> Extensions);

public sealed record ContentLink(Guid Id, string Path);

// An override merges into its original and names no definition of its own, so pointing at it can only mean the original.
public static class OverrideLinks
{
    public static List<T> InPlaceOfOverrides<T>(IEnumerable<T> named, Func<T, T?> originalOf, Func<T, Guid> id) where T : class =>
        named.Select(item => originalOf(item) ?? item).DistinctBy(id).ToList();
}

// The project's side of references: names to items and back. The model never sees an id.
public interface IContentLinks
{
    // Null when the project has no item with this id.
    string? PathOf(Guid id);

    // Throws EditError, listing candidates, when the name is unknown, ambiguous or incompatible.
    ContentLink Find(string name, ReferenceSpec spec);

    Definition CreateDefinitionReference(Guid id);
}
