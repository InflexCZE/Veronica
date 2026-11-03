using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;

namespace Veronica.Editing;

// Dictionary keys and set elements as the model reads and writes them. A definition is named by its path, like a definition value.
public static class Keys
{
    public static bool IsDefinition(Type keyType) => typeof(Definition).IsAssignableFrom(keyType);

    public static string Text(object? key, IContentLinks? links) => key switch
    {
        Definition definition when links != null => links.PathOf(definition.Guid) ?? "(missing item)",
        _ => Fields.KeyText(key),
    };

    // A new key, or a new set element, to add.
    public static object Parse(MemberReference collection, string text, Type keyType, IContentLinks? links)
    {
        if (!IsDefinition(keyType))
            return Values.FromText(text, keyType);
        return links!.CreateDefinitionReference(Resolve(collection, text, keyType, links).Id);
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

    // The set's own element, or null; matched like a dictionary key.
    public static object? FindInSet(MemberReference set, string text, Type itemType, IContentLinks? links)
    {
        if (!IsDefinition(itemType))
        {
            object item = Values.FromText(text, itemType);
            return set.SetContainsValue(item) ? item : null;
        }

        Guid id = Resolve(set, text, itemType, links).Id;
        return Fields.SetItems(set).FirstOrDefault(i => i is Definition d && d.Guid == id);
    }

    private static ContentLink Resolve(MemberReference collection, string text, Type keyType, IContentLinks? links)
    {
        if (links == null)
            throw new EditError($"Keys of type {Fields.TypeLabel(keyType)} cannot be addressed here.");
        return links.Find(text, new ReferenceSpec(collection, ReferenceKind.Definition, [keyType], []));
    }
}
