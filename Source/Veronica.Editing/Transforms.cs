using System.Globalization;
using System.Text.Json;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.UI.Attributes;

namespace Veronica.Editing;

// A transform prints and writes as {Position: {...}, Euler: {X, Y, Z}} with Euler in degrees, as the
// inspector shows it; the quaternion behind it never appears.
public static class Transforms
{
    public const string EulerName = "Euler";

    public static string Render(MemberReference position, MemberReference orientation, IContentLinks links) =>
        $"{{{position.Name}: {FieldReader.Describe(position, links)}, {EulerName}: {RenderVector(EulerDegrees(orientation))}}}";

    public static void Write(string path, MemberReference position, MemberReference orientation, JsonElement value)
    {
        string fieldList = $"{position.Name}, {EulerName}";
        Dictionary<string, string> given = value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText(), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.String => Values.ParseInline(value.GetString()!),
            _ => throw new EditError($"'{path}' takes {{{position.Name}: {{X: ..., Y: ..., Z: ...}}, {EulerName}: {{X: ..., Y: ..., Z: ...}}}} with Euler in degrees; either part may be left out."),
        };
        if (given.Count == 0)
            throw new EditError($"Give at least one of {fieldList}.");

        // Every value parses before anything is set, so a bad one changes nothing.
        var positionUpdates = new List<(MemberReference Field, object Value)>();
        Vector3? euler = null;
        foreach (var (name, text) in given)
        {
            if (string.Equals(name, position.Name, StringComparison.OrdinalIgnoreCase))
                positionUpdates.AddRange(ParseParts(FieldPath.Member(path, position.Name), position, text));
            else if (string.Equals(name, EulerName, StringComparison.OrdinalIgnoreCase))
                euler = ParseEuler(FieldPath.Member(path, EulerName), text, EulerDegrees(orientation));
            else
                throw new EditError($"'{name}' is not a field of '{path}'. Fields: {fieldList}.");
        }

        foreach (var (field, parsed) in positionUpdates)
            field.SetValue<object?>(parsed);
        if (euler is { } degrees)
        {
            orientation.SetValue<object?>(Quaternion.CreateFromEuler(ToRadians(degrees)));
            var hint = FindHint(orientation);
            if (!hint.IsDefault)
                hint.SetValue<object?>(degrees);
        }
    }

    // The inspector shows the stored hint while it still matches the quaternion, else angles derived from it.
    private static Vector3 EulerDegrees(MemberReference orientation)
    {
        var quaternion = orientation.GetValue<Quaternion>();
        var hint = FindHint(orientation);
        if (!hint.IsDefault && hint.GetValue<object?>() is Vector3 stored && Quaternion.CreateFromEuler(ToRadians(stored)).Equals(quaternion))
            return stored;
        var radians = quaternion.ConvertToEuler();
        return new Vector3(MathHelper.ToDegrees(radians.X), MathHelper.ToDegrees(radians.Y), MathHelper.ToDegrees(radians.Z));
    }

    // Same search as the inspector's quaternion element: the nearest parent member whose EulerHint names this quaternion.
    private static MemberReference FindHint(MemberReference orientation)
    {
        string quaternionPath = orientation.ToString().Replace(".", "/");
        var member = orientation;
        while (member.TryGetParent(out var parent))
        {
            foreach (var candidate in parent.Members)
            {
                if (candidate.GetMemberAttribute<EulerHintAttribute>() is { } attribute && quaternionPath.EndsWith(attribute.QuaternionName, StringComparison.Ordinal))
                    return candidate;
            }
            member = parent;
        }
        return default;
    }

    private static IEnumerable<(MemberReference, object)> ParseParts(string path, MemberReference member, string text)
    {
        if (!Fields.TryGetInlineFields(member, out var fields))
            throw new EditError($"'{path}' cannot be written here.");
        var parts = Values.ParseInline(text);
        foreach (var (name, part) in parts)
        {
            var field = fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (field.IsDefault)
                throw new EditError($"'{name}' is not a field of '{path}'. Fields: {string.Join(", ", fields.Select(f => f.Name))}.");
            yield return (field, Values.FromText(part, Fields.ValueType(field)));
        }
    }

    private static Vector3 ParseEuler(string path, string text, Vector3 current)
    {
        var parts = Values.ParseInline(text);
        foreach (var (name, part) in parts)
        {
            float angle = (float)Values.FromText(part, typeof(float));
            switch (name.ToUpperInvariant())
            {
                case "X": current.X = angle; break;
                case "Y": current.Y = angle; break;
                case "Z": current.Z = angle; break;
                default: throw new EditError($"'{name}' is not a field of '{path}'. Fields: X, Y, Z.");
            }
        }
        return current;
    }

    private static Vector3 ToRadians(Vector3 degrees) =>
        new(MathHelper.ToRadians(degrees.X), MathHelper.ToRadians(degrees.Y), MathHelper.ToRadians(degrees.Z));

    private static string RenderVector(Vector3 v) =>
        $"{{X: {v.X.ToString(CultureInfo.InvariantCulture)}, Y: {v.Y.ToString(CultureInfo.InvariantCulture)}, Z: {v.Z.ToString(CultureInfo.InvariantCulture)}}}";
}
