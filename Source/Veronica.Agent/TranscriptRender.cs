using System.Text;

namespace Veronica.Agent;

// Replayed as chat turns in the model's own tool-call format, a transcript reads to a small model
// as a session still in progress, and it carries on with the session's task. Quoted as plain text
// inside one message, it is something to read about, not to take part in.
public static class TranscriptRender
{
    public const int ResultChars = 1000;

    public static string Render(IReadOnlyList<ChatMessage> transcript, IReadOnlySet<string> keptWhole)
    {
        var parts = new List<string>();
        foreach (var message in transcript)
        {
            switch (message.Role)
            {
                case ChatRole.User:
                    parts.Add($"[user]\n{message.Content.Trim()}");
                    break;
                case ChatRole.Assistant:
                    if (message.Content.Trim().Length > 0)
                        parts.Add($"[assistant]\n{message.Content.Trim()}");
                    foreach (var call in message.ToolCalls ?? [])
                        parts.Add($"[assistant used {call.Name}] {call.Arguments}");
                    break;
                case ChatRole.Tool:
                    var name = message.Name ?? "tool";
                    var content = keptWhole.Contains(name) ? message.Content.Trim() : Cut(message.Content.Trim());
                    parts.Add($"[result of {name}]\n{content}");
                    break;
            }
        }
        return string.Join("\n\n", parts);
    }

    private static string Cut(string text)
    {
        if (text.Length <= ResultChars)
            return text;
        return new StringBuilder(text, 0, ResultChars, ResultChars + 40)
            .Append($"... (cut, {text.Length} characters in all)")
            .ToString();
    }
}
