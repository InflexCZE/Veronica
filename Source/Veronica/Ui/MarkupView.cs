using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Veronica.Agent.Chat;

namespace Veronica.Ui;

// Model text as blocks (ChatMarkup). Unchanged blocks keep their controls, so a streaming reply only rebuilds its last block.
public sealed class MarkupView : StackPanel
{
    public static readonly StyledProperty<IReadOnlyList<MarkupBlock>?> BlocksProperty =
        AvaloniaProperty.Register<MarkupView, IReadOnlyList<MarkupBlock>?>(nameof(Blocks));

    public static readonly StyledProperty<bool> SmallProperty =
        AvaloniaProperty.Register<MarkupView, bool>(nameof(Small));

    private static readonly FontFamily TextFont = new("Segoe UI, Segoe UI Emoji, Segoe UI Symbol");
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas, Segoe UI Symbol");

    public IReadOnlyList<MarkupBlock>? Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    public bool Small
    {
        get => GetValue(SmallProperty);
        set => SetValue(SmallProperty, value);
    }

    private string TextSize => Small ? "SmallFontSize" : "MediumFontSize";

    private string HeadingSize => Small ? "MediumFontSize" : "LargeFontSize";

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SmallProperty)
            Children.Clear();
        if (change.Property == BlocksProperty || change.Property == SmallProperty)
            Rebuild(Blocks ?? []);
    }

    private void Rebuild(IReadOnlyList<MarkupBlock> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            var key = Key(blocks[i]);
            if (i >= Children.Count || !Equals(Children[i].Tag, key))
            {
                var control = Build(blocks[i]);
                control.Tag = key;
                if (i < Children.Count)
                    Children[i] = control;
                else
                    Children.Add(control);
            }
            var margin = Children[i].Margin;
            Children[i].Margin = new Thickness(margin.Left, i == 0 ? 0 : Gap(blocks[i]), margin.Right, margin.Bottom);
        }
        while (Children.Count > blocks.Count)
            Children.RemoveAt(Children.Count - 1);
    }

    private static double Gap(MarkupBlock block) => block switch
    {
        HeadingBlock => 14,
        ListItemBlock when block.Joined => 2,
        _ => 8,
    };

    private static string Key(MarkupBlock block) => block switch
    {
        CodeBlock code => "c" + code.Code,
        ProseBlock prose => "p" + Key(prose.Spans),
        HeadingBlock heading => "h" + Key(heading.Spans),
        ListItemBlock item => $"l{item.Level}:{item.Marker.Length}:{item.Marker}" + Key(item.Spans),
        TableBlock table => "t" + (table.Header == null ? "-" : Key(table.Header))
            + string.Concat(table.Rows.Select(r => "r" + Key(r))),
        _ => "",
    };

    private static string Key(IReadOnlyList<IReadOnlyList<MarkupSpan>> cells) =>
        $"{cells.Count}:" + string.Concat(cells.Select(c => "|" + Key(c)));

    private static string Key(IReadOnlyList<MarkupSpan> spans) =>
        $"{spans.Count}:" + string.Concat(spans.Select(s =>
            $"{(s.Bold ? 1 : 0)}{(s.Italic ? 1 : 0)}{(s.Code ? 1 : 0)}{s.Link != null}{s.Text.Length}:{s.Text}"));

    private Control Build(MarkupBlock block) => block switch
    {
        CodeBlock code => BuildCode(code),
        HeadingBlock heading => BuildText(heading.Spans, HeadingSize, bold: true),
        ListItemBlock item => BuildItem(item),
        TableBlock table => BuildTable(table),
        ProseBlock prose => BuildText(prose.Spans, TextSize, bold: false),
        _ => new Panel(),
    };

    private Control BuildCode(CodeBlock code)
    {
        var codeText = new SelectableTextBlock
        {
            Text = code.Code,
            FontFamily = MonoFont,
            TextWrapping = TextWrapping.Wrap,
        };
        Resource(codeText, TextBlock.FontSizeProperty, "SmallFontSize");
        Resource(codeText, TextBlock.ForegroundProperty, "PrimaryTextColor");
        var box = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = codeText,
        };
        Resource(box, Border.BackgroundProperty, "SemiLightGrayColor");
        return box;
    }

    private Control BuildItem(ListItemBlock item)
    {
        var marker = new TextBlock
        {
            Text = item.Marker,
            FontFamily = TextFont,
            MinWidth = 14,
            Margin = new Thickness(0, 0, 4, 0),
        };
        Resource(marker, TextBlock.FontSizeProperty, TextSize);
        Resource(marker, TextBlock.ForegroundProperty, "PrimaryTextColor");
        var text = BuildText(item.Spans, TextSize, bold: false);
        Grid.SetColumn(text, 1);
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(4 + 16 * item.Level, 0, 0, 0),
        };
        row.Children.Add(marker);
        row.Children.Add(text);
        return row;
    }

    private Control BuildTable(TableBlock table)
    {
        var lines = new List<(IReadOnlyList<IReadOnlyList<MarkupSpan>> Cells, bool Header)>();
        if (table.Header != null)
            lines.Add((table.Header, true));
        lines.AddRange(table.Rows.Select(r => (r, false)));
        int columns = lines.Count == 0 ? 0 : lines.Max(l => l.Cells.Count);

        var grid = new Grid();
        // Star widths follow each column's longest cell, clamped so a short column keeps room and a long one wraps.
        for (int c = 0; c < columns; c++)
        {
            int widest = lines.Max(l => c < l.Cells.Count ? l.Cells[c].Sum(s => s.Text.Length) : 0);
            grid.ColumnDefinitions.Add(new ColumnDefinition(Math.Clamp(widest, 4, 40), GridUnitType.Star));
        }
        for (int r = 0; r < lines.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var (cells, header) = lines[r];
            for (int c = 0; c < columns; c++)
            {
                IReadOnlyList<MarkupSpan> spans = c < cells.Count ? cells[c] : [];
                var cell = new Border
                {
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(6, 3),
                    Child = BuildText(spans, TextSize, bold: header),
                };
                Resource(cell, Border.BorderBrushProperty, "LightestGrayColor");
                if (header)
                    Resource(cell, Border.BackgroundProperty, "SemiLightGrayColor");
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        var frame = new Border
        {
            BorderThickness = new Thickness(1, 1, 0, 0),
            Child = grid,
        };
        Resource(frame, Border.BorderBrushProperty, "LightestGrayColor");
        return frame;
    }

    private SelectableTextBlock BuildText(IReadOnlyList<MarkupSpan> spans, string size, bool bold)
    {
        var text = new SelectableTextBlock
        {
            FontFamily = TextFont,
            TextWrapping = TextWrapping.Wrap,
            Inlines = new InlineCollection(),
        };
        if (bold)
            text.FontWeight = FontWeight.Bold;
        Resource(text, TextBlock.FontSizeProperty, size);
        Resource(text, TextBlock.ForegroundProperty, "PrimaryTextColor");
        foreach (var span in spans)
            text.Inlines.Add(Inline(span, size));
        return text;
    }

    private static Inline Inline(MarkupSpan span, string size)
    {
        if (span.Link is { } url)
        {
            var link = new TextBlock
            {
                Text = span.Text,
                FontFamily = TextFont,
                FontWeight = span.Bold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = span.Italic ? FontStyle.Italic : FontStyle.Normal,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            Resource(link, TextBlock.FontSizeProperty, size);
            Resource(link, TextBlock.ForegroundProperty, "HyperlinkColor");
            link.Tapped += (_, _) => Open(url);
            return new InlineUIContainer(link) { BaselineAlignment = BaselineAlignment.TextBottom };
        }

        var run = new Run(span.Text);
        if (span.Bold)
            run.FontWeight = FontWeight.Bold;
        if (span.Italic)
            run.FontStyle = FontStyle.Italic;
        if (span.Code)
        {
            run.FontFamily = MonoFont;
            Resource(run, TextElement.BackgroundProperty, "SemiLightGrayColor");
        }
        return run;
    }

    private static void Open(string url)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }

    // Built before the view is in the tree, so resources resolve once it is attached, and follow the theme.
    private static void Resource(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
