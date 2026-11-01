using System.Text;
using System.Xml;

namespace Veronica.Editing;

// A doc comment's summary as plain text, by the rules of the inspector's tooltips (XmlToRichTextConverter):
// a cref is its short name, <br/> a new line, any other tag its text. That converter returns Avalonia inlines.
public static class DocText
{
    public static string? Render(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        var document = new XmlDocument();
        try
        {
            document.LoadXml($"<root>{xml}</root>");
        }
        catch (XmlException)
        {
            return Squash(xml);
        }

        XmlNode node = document.DocumentElement!.SelectSingleNode("summary") ?? document.DocumentElement;
        var builder = new StringBuilder();
        Append(node, builder);
        string text = string.Join("\n", builder.ToString().Split('\n').Select(l => Squash(l)).Where(l => l.Length > 0));
        return text.Length == 0 ? null : text;
    }

    private static void Append(XmlNode node, StringBuilder builder)
    {
        foreach (XmlNode child in node)
        {
            switch (child.Name)
            {
                case "#text":
                case "#cdata-section":
                case "#whitespace":
                case "#significant-whitespace":
                    Word(builder, child.InnerText);
                    break;
                case "see":
                case "seealso":
                    if (Attribute(child, "cref") is { } cref)
                        Word(builder, ShortName(cref));
                    // A link to a web document the model cannot open; its title alone reads as a stray line.
                    else if (Attribute(child, "href") != null)
                        break;
                    else if (Attribute(child, "langword") is { } word)
                        Word(builder, word);
                    else
                        Append(child, builder);
                    break;
                case "paramref":
                case "typeparamref":
                    if (Attribute(child, "name") is { } name)
                        Word(builder, name);
                    break;
                case "br":
                    TrimEnd(builder);
                    builder.Append('\n');
                    break;
                case "para":
                    TrimEnd(builder);
                    if (builder.Length > 0)
                        builder.Append('\n');
                    Append(child, builder);
                    builder.Append('\n');
                    break;
                default:
                    Append(child, builder);
                    break;
            }
        }
    }

    private static void Word(StringBuilder builder, string text)
    {
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            if (text.Length > 0 && builder.Length > 0 && !char.IsWhiteSpace(builder[^1]))
                builder.Append(' ');
            return;
        }
        if (builder.Length > 0 && !char.IsWhiteSpace(builder[^1]) && builder[^1] is not ('(' or '[') && !StartsWithPunctuation(trimmed))
            builder.Append(' ');
        builder.Append(trimmed);
        if (char.IsWhiteSpace(text[^1]))
            builder.Append(' ');
    }

    private static bool StartsWithPunctuation(string text) => text[0] is '.' or ',' or ';' or ':' or ')' or '!' or '?';

    private static void TrimEnd(StringBuilder builder)
    {
        while (builder.Length > 0 && builder[^1] == ' ')
            builder.Length--;
    }

    private static string? Attribute(XmlNode node, string name) =>
        node.Attributes?.GetNamedItem(name)?.Value is { Length: > 0 } value ? value : null;

    // "T:Keen.Game2.ThrusterComponent" and "M:A.B.Run(System.Int32)" name ThrusterComponent and Run.
    public static string ShortName(string cref)
    {
        string name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        int paren = name.IndexOf('(');
        if (paren >= 0)
            name = name[..paren];
        int brace = name.IndexOf('{');
        if (brace >= 0)
            name = name[..brace];
        int backtick = name.IndexOf('`');
        if (backtick >= 0)
            name = name[..backtick];
        return name.Split('.').Last().Trim();
    }

    private static string Squash(string text) => string.Join(" ", text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
}
