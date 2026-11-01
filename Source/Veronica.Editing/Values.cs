using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Extensions;
using Keen.VRage.Library.Localization;
using Keen.VRage.Library.Utils;

namespace Veronica.Editing;

public static class Values
{
    public static object FromJson(JsonElement value, Type type) => value.ValueKind switch
    {
        JsonValueKind.String => FromText(value.GetString()!, type),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => FromText(value.GetRawText(), type),
        _ => throw new EditError($"Expected a {Fields.TypeLabel(type)} value, got {Describe(value.ValueKind)}."),
    };

    public static object FromText(string text, Type type)
    {
        if (type == typeof(string))
            return text;

        string trimmed = text.Trim();
        if (type == typeof(bool))
            return bool.TryParse(trimmed, out bool b) ? b : throw Invalid(text, type);
        if (type.IsEnum)
            return ParseEnum(trimmed, type);
        if (type == typeof(StringId))
            return StringId.Get(trimmed);
        if (type == typeof(LocKey))
            return new LocKey { TextId = StringId.Get(trimmed) };
        if (type == typeof(Guid))
            return Guid.TryParse(trimmed, out var guid) ? guid : throw Invalid(text, type);

        try
        {
            // Invariant parsing reads a comma as a thousands separator: "0,5" would become 5.
            if (trimmed.Contains(',') && type != typeof(char))
                throw new EditError($"'{text}' is not a valid {Fields.TypeLabel(type)}; use a dot for decimals.");
            if (type.IsPrimitive || type == typeof(decimal))
                return Convert.ChangeType(trimmed, type, CultureInfo.InvariantCulture);

            var parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(IFormatProvider)]);
            if (parse != null)
                return parse.Invoke(null, [trimmed, CultureInfo.InvariantCulture])!;
            parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
            if (parse != null)
                return parse.Invoke(null, [trimmed])!;
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or TargetInvocationException)
        {
            throw Invalid(text, type);
        }

        throw new EditError($"Values of type {Fields.TypeLabel(type)} cannot be written directly.");
    }

    // Engine number-like structs (FixedPoint) parse from text like the primitives.
    public static bool HasParse(Type type) =>
        Fields.HasPublicStaticMethod(type, "Parse", typeof(string), typeof(IFormatProvider))
        || Fields.HasPublicStaticMethod(type, "Parse", typeof(string));

    private static object ParseEnum(string text, Type type)
    {
        if (Enum.TryParse(type, text, ignoreCase: true, out var value)
            && (Enum.IsDefined(type, value!) || type.IsDefined(typeof(FlagsAttribute))))
            return value!;
        throw new EditError($"'{text}' is not a {type.Name}. Values: {string.Join(", ", Enum.GetNames(type))}.");
    }

    private static EditError Invalid(string text, Type type) =>
        new($"'{text}' is not a valid {Fields.TypeLabel(type)}.");

    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "a list",
        _ => kind.ToString().ToLowerInvariant(),
    };

    public static string Render(object? value, IContentLinks links, bool assetGuid = false) => value switch
    {
        null => "null",
        string s => Quote(s),
        bool b => b ? "true" : "false",
        Enum e => e.ToString(),
        StringId id => Quote(id.ToString()),
        LocKey loc => Quote(loc.TextId.ToString()),
        Definition definition => RenderLink(definition.Guid, links),
        IResourceHandle handle => handle.TryGetGuid() is { } id ? RenderLink(id, links) : handle.Key == 0 ? "none" : "(generated resource)",
        Guid guid when assetGuid => RenderLink(guid, links),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null",
    };

    // The item a reference value points at, or null when the value is no reference or points at nothing.
    public static Guid? LinkId(object? value, bool assetGuid = false)
    {
        Guid? id = value switch
        {
            Definition definition => definition.Guid,
            IResourceHandle handle => handle.TryGetGuid(),
            Guid guid when assetGuid => guid,
            _ => null,
        };
        return id == Guid.Empty ? null : id;
    }

    private static string RenderLink(Guid id, IContentLinks links)
    {
        if (id == Guid.Empty)
            return "none";
        return links.PathOf(id) ?? "(missing item)";
    }

    public static string Quote(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.Append('"').ToString();
    }

    // The one-line struct form read prints: {X: 0.1, Y: 0.1}. Values may be quoted or nested {...}.
    public static Dictionary<string, string> ParseInline(string text)
    {
        string body = text.Trim();
        if (body.Length < 2 || body[0] != '{' || body[^1] != '}')
            throw new EditError($"Expected {{Field: value, ...}}, got '{text}'.");

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in SplitTopLevel(body[1..^1]))
        {
            if (part.Trim().Length == 0)
                continue;
            int colon = part.IndexOfAny([':', '=']);
            if (colon <= 0)
                throw new EditError($"Expected 'Field: value' in '{part.Trim()}'.");
            string name = part[..colon].Trim().Trim('"');
            string value = part[(colon + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1];
            if (!fields.TryAdd(name, value))
                throw new EditError($"'{name}' is given twice.");
        }
        return fields;
    }

    private static IEnumerable<string> SplitTopLevel(string text)
    {
        bool quoted = false;
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
                quoted = !quoted;
            else if (quoted)
                continue;
            else if (text[i] == '{')
                depth++;
            else if (text[i] == '}')
                depth--;
            else if (text[i] == ',' && depth == 0)
            {
                yield return text[start..i];
                start = i + 1;
            }
        }
        yield return text[start..];
    }
}
