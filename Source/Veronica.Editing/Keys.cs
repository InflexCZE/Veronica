using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;

namespace Veronica.Editing;

// Dictionary keys as the model reads and writes them. A definition key is named by its path, like a definition value.
public static class Keys
{
    public static bool IsDefinition(Type keyType) => typeof(Definition).IsAssignableFrom(keyType);

    public static string Text(object? key, IContentLinks? links) => key switch
    {
        Definition definition when links != null => links.PathOf(definition.Guid) ?? "(missing item)",
        _ => Fields.KeyText(key),
    };

    // A new key for an element to add.
    public static object Parse(MemberReference dictionary, string text, Type keyType, IContentLinks? links)
    {
        if (!IsDefinition(keyType))
            return Values.FromText(text, keyType);
        return links!.CreateDefinitionReference(Resolve(dictionary, text, keyType, links).Id);
    }

    // The dictionary's own key object, or null. Definitions compare by reference, so a definition key is matched by id.
    public static object? Find(MemberReference dictionary, string text, Type keyType, IContentLinks? links)
    {
        if (!IsDefinition(keyType))
        {
            object key = Values.FromText(text, keyType);
            return dictionary.DictionaryContainsKey(key) ? key : null;
        }

        Guid id = Resolve(dictionary, text, keyType, links).Id;
        return dictionary.EnumerateDictionaryKeys()
            .Select(k => k.GetValue<object?>())
            .FirstOrDefault(k => k is Definition d && d.Guid == id);
    }

    private static ContentLink Resolve(MemberReference dictionary, string text, Type keyType, IContentLinks? links)
    {
        if (links == null)
            throw new EditError($"Keys of type {Fields.TypeLabel(keyType)} cannot be addressed here.");
        return links.Find(text, new ReferenceSpec(dictionary, ReferenceKind.Definition, [keyType], []));
    }
}
