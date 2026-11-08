using Veronica.Agent.Chat;
using Xunit;

namespace Veronica.Tests;

public sealed class ChatMarkupTests
{
    [Fact]
    public void Plain_text_is_one_prose_block_with_line_breaks_kept()
    {
        var block = Assert.IsType<ProseBlock>(Assert.Single(ChatMarkup.Parse("one\n- two\n# three")));
        Assert.Equal([new MarkupSpan("one\n- two\n# three")], block.Spans);
    }

    [Fact]
    public void Fenced_code_splits_the_prose_and_drops_the_language()
    {
        var blocks = ChatMarkup.Parse("Before:\n```csharp\nvar x = 1;\n```\nAfter.");
        Assert.Equal(3, blocks.Count);
        Assert.Equal([new MarkupSpan("Before:")], Assert.IsType<ProseBlock>(blocks[0]).Spans);
        Assert.Equal("var x = 1;", Assert.IsType<CodeBlock>(blocks[1]).Code);
        Assert.Equal([new MarkupSpan("After.")], Assert.IsType<ProseBlock>(blocks[2]).Spans);
    }

    [Fact]
    public void An_unclosed_fence_is_code_to_the_end_while_it_streams()
    {
        var blocks = ChatMarkup.Parse("Here:\n```\nline 1\nline 2");
        Assert.Equal("line 1\nline 2", Assert.IsType<CodeBlock>(blocks[^1]).Code);
    }

    [Fact]
    public void Inline_code_bold_and_links_become_spans()
    {
        var spans = ChatMarkup.Spans("Set **MaxSpeed** in `Door.def`, see https://example.com/x.");
        Assert.Equal(
        [
            new MarkupSpan("Set "),
            new MarkupSpan("MaxSpeed", Bold: true),
            new MarkupSpan(" in "),
            new MarkupSpan("Door.def", Code: true),
            new MarkupSpan(", see "),
            new MarkupSpan("https://example.com/x", Link: "https://example.com/x"),
            new MarkupSpan("."),
        ], spans);
    }

    [Fact]
    public void Code_inside_bold_stays_bold()
    {
        var spans = ChatMarkup.Spans("**use `Ion` here**");
        Assert.Equal(
        [
            new MarkupSpan("use ", Bold: true),
            new MarkupSpan("Ion", Bold: true, Code: true),
            new MarkupSpan(" here", Bold: true),
        ], spans);
    }

    [Fact]
    public void An_unclosed_bold_or_backtick_stays_literal()
    {
        Assert.Equal([new MarkupSpan("**half and `open")], ChatMarkup.Spans("**half and `open"));
    }

    [Fact]
    public void A_fence_on_one_line_keeps_its_code()
    {
        var blocks = ChatMarkup.Parse("use ```x = 1``` here");
        Assert.Equal("x = 1", Assert.IsType<CodeBlock>(blocks[1]).Code);
    }

    [Fact]
    public void Blank_lines_split_prose_into_paragraphs()
    {
        var blocks = ChatMarkup.Parse("One.\n\nTwo\nlines.\n \n\nThree.");
        Assert.Equal(["One.", "Two\nlines.", "Three."],
            blocks.Select(b => string.Concat(Assert.IsType<ProseBlock>(b).Spans.Select(s => s.Text))));
    }
}
