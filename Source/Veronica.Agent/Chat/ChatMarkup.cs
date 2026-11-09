using System.Text;
using System.Text.RegularExpressions;

namespace Veronica.Agent.Chat;

public sealed record MarkupSpan(string Text, bool Bold = false, bool Code = false, string? Link = null, bool Italic = false);

public abstract record MarkupBlock
{
    // No blank line between this block and the one before it.
    public bool Joined { get; init; }
}

public sealed record ProseBlock(IReadOnlyList<MarkupSpan> Spans) : MarkupBlock;

public sealed record HeadingBlock(IReadOnlyList<MarkupSpan> Spans) : MarkupBlock;

public sealed record ListItemBlock(int Level, string Marker, IReadOnlyList<MarkupSpan> Spans) : MarkupBlock;

// Header is null when the table has none, or the model left every header cell empty.
public sealed record TableBlock(
    IReadOnlyList<IReadOnlyList<MarkupSpan>>? Header,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<MarkupSpan>>> Rows) : MarkupBlock;

public sealed record CodeBlock(string Code) : MarkupBlock;

// Not full Markdown: what the models write. Fenced and inline code, bold, italic, bare URLs, lists, tables
// and headings. The rest stays as typed, line breaks included. Tolerant of missing blank lines and pipes,
// and of a reply that stops mid-construct while it streams.
public static class ChatMarkup
{
    public const string Bullet = "\u2022";

    // An unclosed fence runs to the end, so a block still streaming already shows as code. A language
    // tag counts only before a line break: "```x = 1```" keeps its code.
    private static readonly Regex Fence = new(@"```(?:[^\n`]*\n)?([\s\S]*?)(?:```\n?|\z)", RegexOptions.CultureInvariant);

    private static readonly Regex Inline = new(
        @"(https?://[^\s<>""`]+)|\*\*(.+?)\*\*|`([^`\n]+)`|(?<![*\w])\*(?![\s*])(.+?)(?<!\s)\*(?![*\w])",
        RegexOptions.CultureInvariant);

    private static readonly Regex ListItem = new(@"^([ \t]*)(?:([-*+\u2022])|(\d{1,3}[.)]))(?:[ \t]+(.*)|$)", RegexOptions.CultureInvariant);

    private static readonly Regex Heading = new(@"^[ \t]{0,3}(#{1,6})([ \t]*)(.*?)(?:[ \t]+#+)?[ \t]*$", RegexOptions.CultureInvariant);

    private static readonly Regex Separator = new(@"^[ \t]*\|?[ \t]*:?-+:?[ \t]*(?:\|[ \t]*(?::?-+:?[ \t]*)?)*$", RegexOptions.CultureInvariant);

    private const string UrlTrailers = ".,;:!?)]}'";

    public static IReadOnlyList<MarkupBlock> Parse(string text)
    {
        text = text.Replace("\r\n", "\n");
        var blocks = new List<MarkupBlock>();
        int at = 0;
        foreach (Match fence in Fence.Matches(text))
        {
            new LineParser(blocks).Run(text[at..fence.Index]);
            var code = fence.Groups[1].Value;
            if (code.EndsWith('\n'))
                code = code[..^1];
            blocks.Add(new CodeBlock(code));
            at = fence.Index + fence.Length;
        }
        new LineParser(blocks).Run(text[at..]);
        return blocks;
    }

    public static IReadOnlyList<MarkupSpan> Spans(string text)
    {
        var spans = new List<MarkupSpan>();
        AddSpans(spans, text, bold: false, italic: false);
        return spans;
    }

    private sealed class LineParser(List<MarkupBlock> blocks)
    {
        private readonly List<string> _prose = [];
        private readonly List<int> _indents = [];
        private (int Level, string Marker, StringBuilder Text)? _item;
        private bool _joined;

        public void Run(string text)
        {
            var lines = text.Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    Flush();
                    _joined = false;
                    i++;
                    continue;
                }
                if (Table(lines, ref i))
                    continue;
                i++;

                var item = ListItem.Match(line);
                if (item.Success)
                {
                    Flush();
                    int indent = Indent(item.Groups[1].Value);
                    while (_indents.Count > 0 && indent < _indents[^1])
                        _indents.RemoveAt(_indents.Count - 1);
                    if (_indents.Count == 0 || indent > _indents[^1])
                        _indents.Add(indent);
                    var marker = item.Groups[2].Success ? Bullet : item.Groups[3].Value;
                    _item = (_indents.Count - 1, marker, new StringBuilder(item.Groups[4].Value.Trim()));
                    continue;
                }

                if (HeadingText(line) is { } heading)
                {
                    Flush();
                    _indents.Clear();
                    Add(new HeadingBlock(Spans(heading)));
                    continue;
                }

                if (_item is { } open && Indent(line) > 0)
                {
                    open.Text.Append('\n').Append(line.Trim());
                    continue;
                }

                if (_item != null)
                    Flush();
                _indents.Clear();
                _prose.Add(line.TrimStart());
            }
            Flush();
        }

        private bool Table(string[] lines, ref int i)
        {
            var first = lines[i];
            if (!first.Contains('|') || Separator.IsMatch(first))
                return false;
            bool separated = i + 1 < lines.Length && Separator.IsMatch(lines[i + 1]) && lines[i + 1].Contains('|');
            // Two framed rows without a separator still read as a table.
            bool framed = !separated && i + 1 < lines.Length && Framed(first) && Framed(lines[i + 1]);
            if (!separated && !framed)
                return false;

            Flush();
            _indents.Clear();
            var header = Cells(first);
            int j = i + (separated ? 2 : 1);
            var raw = new List<List<string>>();
            for (; j < lines.Length && lines[j].Contains('|'); j++)
                if (!Separator.IsMatch(lines[j]))
                    raw.Add(Cells(lines[j]));
            i = j;

            int columns = header.Count;
            var rows = raw.Select(r => (IReadOnlyList<IReadOnlyList<MarkupSpan>>)Fit(r, columns).Select(Spans).ToList()).ToList();
            var head = header.All(c => c.Length == 0) ? null : header.Select(Spans).ToList();
            Add(new TableBlock(head, rows));
            return true;
        }

        private void Flush()
        {
            if (_item is { } item)
            {
                Add(new ListItemBlock(item.Level, item.Marker, Spans(item.Text.ToString())));
                _item = null;
            }
            if (_prose.Count > 0)
            {
                Add(new ProseBlock(Spans(string.Join("\n", _prose))));
                _prose.Clear();
            }
        }

        private void Add(MarkupBlock block)
        {
            blocks.Add(block with { Joined = _joined });
            _joined = true;
        }
    }

    private static string? HeadingText(string line)
    {
        var m = Heading.Match(line);
        if (!m.Success)
            return null;
        // "#4DABF7" is a colour, not a heading: without a space it takes at least two hashes.
        if (m.Groups[2].Length == 0 && m.Groups[1].Length < 2)
            return null;
        var text = m.Groups[3].Value.Trim();
        return text.Length > 0 ? text : null;
    }

    private static int Indent(string line)
    {
        int width = 0;
        foreach (var c in line)
        {
            if (c == ' ')
                width++;
            else if (c == '\t')
                width += 4;
            else
                break;
        }
        return width;
    }

    private static bool Framed(string line)
    {
        var t = line.Trim();
        return t.Length > 1 && t[0] == '|' && t[^1] == '|';
    }

    private static List<string> Cells(string line)
    {
        var t = line.Trim();
        if (t.StartsWith('|'))
            t = t[1..];
        if (t.EndsWith('|') && !t.EndsWith("\\|"))
            t = t[..^1];
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool code = false;
        for (int k = 0; k < t.Length; k++)
        {
            char c = t[k];
            if (c == '\\' && k + 1 < t.Length && t[k + 1] == '|')
            {
                cell.Append('|');
                k++;
                continue;
            }
            if (c == '`')
                code = !code;
            if (c == '|' && !code)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }
            cell.Append(c);
        }
        cells.Add(cell.ToString().Trim());
        return cells;
    }

    private static List<string> Fit(List<string> cells, int columns)
    {
        if (cells.Count > columns)
            return [.. cells.Take(columns - 1), string.Join(" | ", cells.Skip(columns - 1))];
        while (cells.Count < columns)
            cells.Add("");
        return cells;
    }

    private static void AddSpans(List<MarkupSpan> spans, string text, bool bold, bool italic)
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
                Flush(spans, plain, bold, italic);
                spans.Add(new MarkupSpan(url[..keep], bold, Link: url[..keep], Italic: italic));
                plain.Append(url, keep, url.Length - keep);
            }
            else if (m.Groups[2].Success)
            {
                Flush(spans, plain, bold, italic);
                AddSpans(spans, m.Groups[2].Value, bold: true, italic);
            }
            else if (m.Groups[3].Success)
            {
                Flush(spans, plain, bold, italic);
                spans.Add(new MarkupSpan(m.Groups[3].Value, bold, Code: true, Italic: italic));
            }
            else
            {
                Flush(spans, plain, bold, italic);
                AddSpans(spans, m.Groups[4].Value, bold, italic: true);
            }
        }
        plain.Append(text, at, text.Length - at);
        Flush(spans, plain, bold, italic);
    }

    private static void Flush(List<MarkupSpan> spans, StringBuilder plain, bool bold, bool italic)
    {
        if (plain.Length == 0)
            return;
        spans.Add(new MarkupSpan(plain.ToString(), bold, Italic: italic));
        plain.Clear();
    }
}
