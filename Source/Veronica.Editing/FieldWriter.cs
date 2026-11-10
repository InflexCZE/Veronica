using System.Text.Json;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.Validation;

namespace Veronica.Editing;

public static class FieldWriter
{
    // Returns "Path: old -> new".
    public static string Write(MemberReference root, string path, JsonElement value, IContentLinks links, string basePath = "", IDefinitionLocatorStrategy? definitions = null)
    {
        if (path.Trim().Length == 0)
            throw new EditError(basePath.Length == 0
                ? "Give the path of the field to write, e.g. 'UIData.Name'."
                : $"Give the path of a field of '{basePath}', e.g. '{basePath}.Name'.");
        if (Transforms.WalkEuler(root, path, basePath, links) is { } euler)
            return Transforms.WriteEuler(euler, value);

        return CreatingParents(root, path, (member, canonical) => WriteField(member, canonical, value, links, definitions), links, basePath);
    }

    internal static string WriteMember(MemberReference member, string canonical, JsonElement value, IContentLinks links) =>
        WriteField(member, canonical, value, links, null);

    private static EditError SetEdit(string path) =>
        new($"'{path}' is a set; add an element with element_add (the element as key) or remove one with element_remove(\"{path}[<element>]\").");

    // Null objects on the path are created for the edit and removed again if it fails.
    internal static string CreatingParents(MemberReference root, string path, Func<MemberReference, string, string> edit, IContentLinks? links, string basePath = "")
    {
        var created = new List<(MemberReference Member, string Path)>();
        try
        {
            var (member, canonical) = FieldWalker.Walk(root, path, created, basePath, links);
            string reply = edit(member, canonical);
            return created.Count == 0 ? reply : $"{reply} (created {string.Join(", ", created.Select(c => c.Path))})";
        }
        catch (Exception)
        {
            if (created.Count > 0)
                created[0].Member.SetDefault();
            throw;
        }
    }

    private static string WriteField(MemberReference member, string canonical, JsonElement value, IContentLinks links, IDefinitionLocatorStrategy? definitions)
    {
        if (member.IsRoot)
            throw new EditError("Give the path of a field, e.g. 'UIData.Name'.");
        if (member.IsCollectionItem && member.GetParent().MemberKind == MemberValueKind.Set)
        {
            string set = canonical[..canonical.LastIndexOf('[')];
            throw new EditError($"'{canonical}' is an element of the set '{set}' and cannot be changed; remove it with element_remove and add the new one with element_add.");
        }
        if (member.MemberKind == MemberValueKind.Set)
            throw SetEdit(canonical);
        if (!Fields.IsWritable(member))
            throw new EditError($"'{canonical}' is read-only.");

        string before = FieldReader.Describe(member, links);
        ContentLink? link = null;

        if (value.ValueKind == JsonValueKind.Null)
            Clear(member, canonical);
        else if (Fields.GetReference(member) is { } reference)
            link = SetReference(member, canonical, reference, value, links);
        else if (member.MemberKind != MemberValueKind.Value)
            throw new EditError($"'{canonical}' is a {member.MemberKind.ToString().ToLowerInvariant()}; write its elements like '{canonical}[0]', or add one with element_add.");
        else if (Fields.IsScalar(Fields.ValueType(member)))
            member.SetValue<object?>(Values.FromJson(value, Fields.ValueType(member)));
        else if (Fields.TryGetTransform(member, out var position, out var orientation))
            Transforms.Write(canonical, position, orientation, value);
        else if (Fields.TryGetInlineFields(member, out var fields))
            WriteInline(canonical, fields, value);
        else if (IsNullStruct(member) && value.ValueKind is JsonValueKind.Object or JsonValueKind.String)
            CreateInline(member, canonical, value);
        else
            throw Composite(member, canonical);

        string hint = ValueChecks.Hint(member, definitions) is { } found ? $" (game validation: {found})" : "";
        return Reply(canonical, before, FieldReader.Describe(member, links), link, hint);
    }

    public static string Reply(string path, string before, string after, ContentLink? link, string hint = "")
    {
        string reply = (after == before ? $"{path}: {after} (unchanged)" : $"{path}: {before} -> {after}") + hint;
        if (link?.InPlaceOf is { } overridePath)
            reply += $"\n'{overridePath}' is an override of {link.Path}; the field points at the original, which already gets the override's values, and so does everything else that uses it. For values only this one gets, point at a copy (create_partial kind copy).";
        return reply;
    }

    // Null empties references and nullables and resets plain values; it never removes an object or a list.
    private static void Clear(MemberReference member, string path)
    {
        // A transform's default has a zero quaternion, an invalid rotation that read would show as no rotation.
        if (Nullable.GetUnderlyingType(member.DeclaredMemberType) == null && Fields.TryGetTransform(member, out _, out _))
            throw new EditError($"'{path}' cannot be null; to reset it write {{Position: {{X: 0, Y: 0, Z: 0}}, Euler: {{X: 0, Y: 0, Z: 0}}}}.");
        Type declared = member.DeclaredMemberType;
        bool nullable = Fields.GetReference(member) != null
            || Nullable.GetUnderlyingType(declared) != null
            || declared.IsValueType
            || (member.MemberKind == MemberValueKind.Value && member.IsNullableReferenceType);
        if (!nullable)
            throw new EditError(member.MemberKind switch
            {
                MemberValueKind.Value => $"'{path}' cannot be null; write its fields instead.",
                _ => $"'{path}' cannot be null; write its elements like '{path}[0]'.",
            });
        member.SetDefault();
    }

    private static bool IsNullStruct(MemberReference member) =>
        Nullable.GetUnderlyingType(member.DeclaredMemberType) != null && member.GetValue<object?>() is null;

    // A null nullable struct takes the one-line form too; fields left out keep the struct's defaults.
    private static void CreateInline(MemberReference member, string path, JsonElement value)
    {
        member.SetNew(initializeNullableValueTypes: true);
        try
        {
            if (!Fields.TryGetInlineFields(member, out var fields))
                throw Composite(member, path);
            WriteInline(path, fields, value);
        }
        catch (Exception)
        {
            member.SetDefault();
            throw;
        }
    }

    private static EditError Composite(MemberReference member, string path)
    {
        var writable = Fields.VisibleMembers(member).Where(Fields.IsWritable).Select(m => FieldPath.Member(path, m.Name)).ToList();
        return writable.Count == 0
            ? new EditError($"'{path}' has no fields that can be written here.")
            : new EditError($"'{path}' has fields of its own; write them one by one: {string.Join(", ", writable)}.");
    }

    private static ContentLink SetReference(MemberReference member, string path, ReferenceSpec spec, JsonElement value, IContentLinks links)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString()!.Trim().Length == 0)
            throw new EditError($"'{path}' is a reference; give the name or path of the item to point at, or null to clear it.");

        var link = links.Find(value.GetString()!.Trim(), spec);
        object reference = spec.Kind switch
        {
            ReferenceKind.Definition => links.CreateDefinitionReference(link.Id),
            ReferenceKind.Handle => Activator.CreateInstance(Fields.ValueType(member), link.Id)!,
            _ => link.Id,
        };
        member.SetValue<object?>(reference);
        return link;
    }

    private static void WriteInline(string path, List<MemberReference> fields, JsonElement value)
    {
        Dictionary<string, string> given;
        if (value.ValueKind == JsonValueKind.Object)
            given = value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText(), StringComparer.OrdinalIgnoreCase);
        else if (value.ValueKind == JsonValueKind.String)
            given = Values.ParseInline(value.GetString()!);
        else
            throw new EditError($"'{path}' takes {{{string.Join(", ", fields.Select(f => $"{f.Name}: ..."))}}}, or write one field like '{path}.{fields[0].Name}'.");

        if (given.Count == 0)
            throw new EditError($"Give at least one of {string.Join(", ", fields.Select(f => f.Name))}.");

        // Every value parses before anything is set, so a bad one changes nothing.
        var updates = new List<(MemberReference Field, object Value)>();
        foreach (var (name, text) in given)
        {
            var field = fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (field.IsDefault)
                throw new EditError($"'{name}' is not a field of '{path}'. Fields: {string.Join(", ", fields.Select(f => f.Name))}.");
            updates.Add((field, Values.FromText(text, Fields.ValueType(field))));
        }

        foreach (var (field, parsed) in updates)
            field.SetValue<object?>(parsed);
    }
}
