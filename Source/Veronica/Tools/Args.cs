using System.Text.Json;
using Veronica.Editing;

namespace Veronica.Tools;

internal static class Args
{
    public static string Text(JsonElement args, string name) =>
        OptionalText(args, name) is { Length: > 0 } text ? text : throw new EditError($"{name} is missing.");

    // Models sometimes send numbers where text is expected; take their raw form.
    public static string? OptionalText(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!.Trim(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            JsonValueKind.Null => null,
            _ => throw new EditError($"{name} must be text."),
        };
    }

    public static JsonElement Value(JsonElement args, string name) =>
        args.TryGetProperty(name, out var value) ? value : throw new EditError($"{name} is missing; use null to clear a field.");
}
