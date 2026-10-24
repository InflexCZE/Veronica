using System.Text.Json;
using Keen.VRage.Library.Serialization;

namespace Veronica.Editing;

public static class FieldWriter
{
    // Returns "Path: old -> new".
    public static string Write(MemberReference root, string path, JsonElement value, IContentLinks links)
    {
        if (path.Trim().Length == 0)
            throw new EditError("Give the path of the field to write, e.g. 'UIData.Name'.");

        return CreatingParents(root, path, (member, canonical) => WriteField(member, canonical, value, links));
    }

    // Null objects on the path are created for the edit and removed again if it fails.
    internal static string CreatingParents(MemberReference root, string path, Func<MemberReference, string, string> edit)
    {
        var created = new List<(MemberReference Member, string Path)>();
        try
        {
            var (member, canonical) = FieldWalker.Walk(root, path, created);
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

    private static string WriteField(MemberReference member, string canonical, JsonElement value, IContentLinks links)
    {
        if (member.IsRoot)
            throw new EditError("Give the path of a field, e.g. 'UIData.Name'.");
        if (!Fields.IsWritable(member))
            throw new EditError($"'{canonical}' is read-only.");

        string before = FieldReader.Describe(member, links);

        if (value.ValueKind == JsonValueKind.Null)
            Clear(member, canonical);
        else if (Fields.GetReference(member) is { } reference)
            SetReference(member, canonical, reference, value, links);
        else if (member.MemberKind != MemberValueKind.Value)
            throw new EditError($"'{canonical}' is a {member.MemberKind.ToString().ToLowerInvariant()}; write its elements like '{canonical}[0]', or add one with element_add.");
        else if (Fields.IsScalar(Fields.ValueType(member)))
            member.SetValue<object?>(Values.FromJson(value, Fields.ValueType(member)));
        else if (Fields.TryGetInlineFields(member, out var fields))
            WriteInline(canonical, fields, value);
        else if (IsNullStruct(member) && value.ValueKind is JsonValueKind.Object or JsonValueKind.String)
            CreateInline(member, canonical, value);
        else
            throw Composite(member, canonical);

        return $"{canonical}: {before} -> {FieldReader.Describe(member, links)}";
    }

    // Null empties references and nullables and resets plain values; it never removes an object or a list.
    private static void Clear(MemberReference member, string path)
    {
        Type declared = member.DeclaredMemberType;
        bool nullable = Fields.GetReference(member) != null
            || Nullable.GetUnderlyingType(declared) != null
            || declared.IsValueType
            || (member.MemberKind == MemberValueKind.Value && member.IsNullableReferenceType);
        if (!nullable)
            throw new EditError(member.MemberKind == MemberValueKind.Value
                ? $"'{path}' cannot be null; write its fields instead."
                : $"'{path}' cannot be null; write its elements like '{path}[0]'.");
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

    private static void SetReference(MemberReference member, string path, ReferenceSpec spec, JsonElement value, IContentLinks links)
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
