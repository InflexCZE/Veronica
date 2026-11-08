using System.Text;
using System.Text.RegularExpressions;

namespace Veronica.Agent.Chat;

public sealed record MarkupSpan(string Text, bool Bold = false, bool Code = false, string? Link = null);

public abstract record MarkupBlock;

public sealed record ProseBlock(IReadOnlyList<MarkupSpan> Spans) : MarkupBlock;

public sealed record CodeBlock(string Code) : MarkupBlock;

// Not full Markdown: fenced code, inline code, bold and bare URLs. The rest stays as typed, line breaks included.
public static class ChatMarkup
{
    // An unclosed fence runs to the end, so a block still streaming already shows as code. A language
    // tag counts only before a line break: "```x = 1```" keeps its code.
    private static readonly Regex Fence = new(@"```(?:[^\n`]*\n)?([\s\S]*?)(?:```\n?|\z)", RegexOptions.CultureInvariant);

    private static readonly Regex BlankLines = new(@"\n[ \t]*\n\s*", RegexOptions.CultureInvariant);

    private static readonly Regex Inline = new(@"(https?://[^\s<>""`]+)|\*\*(.+?)\*\*|`([^`\n]+)`", RegexOptions.CultureInvariant);

    private const string UrlTrailers = ".,;:!?)]}'";

    public static IReadOnlyList<MarkupBlock> Parse(string text)
    {
        var blocks = new List<MarkupBlock>();
        int at = 0;
        foreach (Match fence in Fence.Matches(text))
        {
            AddProse(blocks, text[at..fence.Index], beforeCode: true);
            var code = fence.Groups[1].Value;
            if (code.EndsWith('\n'))
                code = code[..^1];
            blocks.Add(new CodeBlock(code));
            at = fence.Index + fence.Length;
        }
        AddProse(blocks, text[at..], beforeCode: false);
        return blocks;
    }

    public static IReadOnlyList<MarkupSpan> Spans(string text)
    {
        var spans = new List<MarkupSpan>();
        AddSpans(spans, text, bold: false);
        return spans;
    }

    private static void AddProse(List<MarkupBlock> blocks, string text, bool beforeCode)
    {
        if (beforeCode && text.EndsWith('\n'))
            text = text[..^1];
        // One block per paragraph, so a streaming reply rebuilds only its last one.
        foreach (var paragraph in BlankLines.Split(text))
            if (paragraph.Length > 0)
                blocks.Add(new ProseBlock(Spans(paragraph)));
    }

    private static void AddSpans(List<MarkupSpan> spans, string text, bool bold)
    {
        var plain = new StringBuilder();
        int at = 0;
        foreach (Match m in Inline.Matches(text))
        {
            plain.Append(text, at, m.Index - at);
            at = m.Index + m.Length;
            if (m.Groups[1].Success)
            {
                var url = m.Groups[1].Value;
                int keep = url.Length;
                while (keep > 0 && UrlTrailers.Contains(url[keep - 1]))
                    keep--;
                if (keep < "https://x".Length)
                {
                    plain.Append(url);
                    continue;
                }
                Flush(spans, plain, bold);
                spans.Add(new MarkupSpan(url[..keep], bold, Link: url[..keep]));
                plain.Append(url, keep, url.Length - keep);
            }
            else if (m.Groups[2].Success)
            {
                Flush(spans, plain, bold);
                AddSpans(spans, m.Groups[2].Value, bold: true);
            }
            else
            {
                Flush(spans, plain, bold);
                spans.Add(new MarkupSpan(m.Groups[3].Value, bold, Code: true));
            }
        }
        plain.Append(text, at, text.Length - at);
        Flush(spans, plain, bold);
    }

    private static void Flush(List<MarkupSpan> spans, StringBuilder plain, bool bold)
    {
        if (plain.Length == 0)
            return;
        spans.Add(new MarkupSpan(plain.ToString(), bold));
        plain.Clear();
    }
}
