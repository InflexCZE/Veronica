using System.Text;
using System.Text.Json;

namespace Veronica.Agent.Tools;

// Pieces of the one-line summaries the chat shows for tool calls.
public static class Gist
{
    public static string Clip(string text, int max = 60)
    {
        var flat = new StringBuilder(text.Length);
        foreach (char c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                if (flat.Length > 0 && flat[^1] != ' ')
                    flat.Append(' ');
            }
            else
            {
                flat.Append(c);
            }
        }
        return flat.Length <= max ? flat.ToString() : flat.ToString(0, max - 3).TrimEnd() + "...";
    }

    public static string FirstLine(string text, int max = 80)
    {
        var trimmed = text.Trim();
        int end = trimmed.IndexOf('\n');
        return Clip(end < 0 ? trimmed : trimmed[..end], max);
    }

    // The last part of an item path, as the user knows the file.
    public static string Item(string target)
    {
        var trimmed = target.Trim().TrimEnd('/', '\\');
        int slash = trimmed.LastIndexOfAny(['/', '\\']);
        return Clip(slash < 0 ? trimmed : trimmed[(slash + 1)..], 50);
    }

    public static string Count(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";

    public static string Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => Clip(value.GetString() ?? "", 30),
        JsonValueKind.Null => "null",
        _ => Clip(value.GetRawText(), 30),
    };

    public static string Text(JsonElement arguments, string name, int max = 50) => Clip(ToolArgs.Text(arguments, name), max);

    public static string Capitalized(string phrase) => phrase.Length == 0 ? phrase : char.ToUpperInvariant(phrase[0]) + phrase[1..];
}
