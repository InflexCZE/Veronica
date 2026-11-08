using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Veronica.Agent.Chat;

namespace Veronica.Ui;

// Model text as prose and code blocks (ChatMarkup). Unchanged blocks keep their controls, so a streaming reply only rebuilds its last block.
public sealed class MarkupView : StackPanel
{
    public static readonly StyledProperty<IReadOnlyList<MarkupBlock>?> BlocksProperty =
        AvaloniaProperty.Register<MarkupView, IReadOnlyList<MarkupBlock>?>(nameof(Blocks));

    private static readonly FontFamily TextFont = new("Segoe UI, Segoe UI Emoji, Segoe UI Symbol");
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas, Segoe UI Symbol");

    public MarkupView() => Spacing = 8;

    public IReadOnlyList<MarkupBlock>? Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BlocksProperty)
            Rebuild(Blocks ?? []);
    }

    private void Rebuild(IReadOnlyList<MarkupBlock> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            var key = Key(blocks[i]);
            if (i < Children.Count && Equals(Children[i].Tag, key))
                continue;
            var control = Build(blocks[i]);
            control.Tag = key;
            if (i < Children.Count)
                Children[i] = control;
            else
                Children.Add(control);
        }
        while (Children.Count > blocks.Count)
            Children.RemoveAt(Children.Count - 1);
    }

    private static string Key(MarkupBlock block) => block switch
    {
        CodeBlock code => "c" + code.Code,
        ProseBlock prose => "p" + string.Concat(prose.Spans.Select(s => $"{(s.Bold ? 1 : 0)}{(s.Code ? 1 : 0)}{s.Link != null}{s.Text.Length}:{s.Text}")),
        _ => "",
    };

    private static Control Build(MarkupBlock block)
    {
        if (block is CodeBlock code)
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

        var prose = (ProseBlock)block;
        var text = new SelectableTextBlock
        {
            FontFamily = TextFont,
            TextWrapping = TextWrapping.Wrap,
            Inlines = new InlineCollection(),
        };
        Resource(text, TextBlock.FontSizeProperty, "MediumFontSize");
        Resource(text, TextBlock.ForegroundProperty, "PrimaryTextColor");
        foreach (var span in prose.Spans)
            text.Inlines.Add(Inline(span));
        return text;
    }

    private static Inline Inline(MarkupSpan span)
    {
        if (span.Link is { } url)
        {
            var link = new TextBlock
            {
                Text = span.Text,
                FontFamily = TextFont,
                FontWeight = span.Bold ? FontWeight.Bold : FontWeight.Normal,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            Resource(link, TextBlock.FontSizeProperty, "MediumFontSize");
            Resource(link, TextBlock.ForegroundProperty, "HyperlinkColor");
            link.Tapped += (_, _) => Open(url);
            return new InlineUIContainer(link) { BaselineAlignment = BaselineAlignment.TextBottom };
        }

        var run = new Run(span.Text);
        if (span.Bold)
            run.FontWeight = FontWeight.Bold;
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
