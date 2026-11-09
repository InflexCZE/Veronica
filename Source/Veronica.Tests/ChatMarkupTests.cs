using Veronica.Agent.Chat;
using Xunit;

namespace Veronica.Tests;

public sealed class ChatMarkupTests
{
    private static string Plain(IEnumerable<MarkupSpan> spans) => string.Concat(spans.Select(s => s.Text));

    private static List<string> Cells(IReadOnlyList<IReadOnlyList<MarkupSpan>> row) => row.Select(Plain).ToList();

    [Fact]
    public void Plain_text_is_one_prose_block_with_line_breaks_kept()
    {
        var block = Assert.IsType<ProseBlock>(Assert.Single(ChatMarkup.Parse("one\ntwo\nthree")));
        Assert.Equal([new MarkupSpan("one\ntwo\nthree")], block.Spans);
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
            blocks.Select(b => Plain(Assert.IsType<ProseBlock>(b).Spans)));
    }

    [Fact]
    public void Italic_is_a_span_and_may_hold_code_and_end_in_bold()
    {
        Assert.Equal(
        [
            new MarkupSpan("a "),
            new MarkupSpan("new", Italic: true),
            new MarkupSpan(" one"),
        ], ChatMarkup.Spans("a *new* one"));
        Assert.Equal(
        [
            new MarkupSpan("Lighting > ", Italic: true),
            new MarkupSpan("Interior", Bold: true, Italic: true),
        ], ChatMarkup.Spans("*Lighting > **Interior***"));
        Assert.Contains(new MarkupSpan("Thrust", Code: true, Italic: true), ChatMarkup.Spans("*the `Thrust` field*"));
    }

    [Fact]
    public void Stars_that_are_not_emphasis_stay_literal()
    {
        Assert.Equal([new MarkupSpan("44 N*m, 2 * 3 * 4, *.def and *.mwm")], ChatMarkup.Spans("44 N*m, 2 * 3 * 4, *.def and *.mwm"));
    }

    [Fact]
    public void A_list_right_after_text_becomes_items_that_hug_it()
    {
        var blocks = ChatMarkup.Parse("Pick one:\n- **Point** lamp\n* Spot\n+ Tube\n1. first\n2) second");
        Assert.IsType<ProseBlock>(blocks[0]);
        var items = blocks.Skip(1).Select(b => Assert.IsType<ListItemBlock>(b)).ToList();
        Assert.Equal([ChatMarkup.Bullet, ChatMarkup.Bullet, ChatMarkup.Bullet, "1.", "2)"], items.Select(i => i.Marker));
        Assert.Equal(["Point lamp", "Spot", "Tube", "first", "second"], items.Select(i => Plain(i.Spans)));
        Assert.True(items[0].Spans[0].Bold);
        Assert.All(items, i => Assert.True(i.Joined));
    }

    [Fact]
    public void Indent_sets_the_nesting_level_whatever_its_width()
    {
        var blocks = ChatMarkup.Parse("1. top\n   - three spaces\n       - seven\n   - back\n2. top again\n\n3. after a blank line");
        var items = blocks.Select(b => Assert.IsType<ListItemBlock>(b)).ToList();
        Assert.Equal([0, 1, 2, 1, 0, 0], items.Select(i => i.Level));
        Assert.False(items[5].Joined);
    }

    [Fact]
    public void An_indented_line_continues_its_item_and_an_unindented_one_ends_the_list()
    {
        var blocks = ChatMarkup.Parse("1. Barrel\n   Put it at the tip.\nThen tell me.");
        Assert.Equal("Barrel\nPut it at the tip.", Plain(Assert.IsType<ListItemBlock>(blocks[0]).Spans));
        Assert.Equal("Then tell me.", Plain(Assert.IsType<ProseBlock>(blocks[1]).Spans));
    }

    [Fact]
    public void Headings_need_a_space_or_two_hashes()
    {
        var blocks = ChatMarkup.Parse("## The gate ##\n###Power\n#4DABF7 is the blue\n# ");
        Assert.Equal("The gate", Plain(Assert.IsType<HeadingBlock>(blocks[0]).Spans));
        Assert.Equal("Power", Plain(Assert.IsType<HeadingBlock>(blocks[1]).Spans));
        Assert.Equal("#4DABF7 is the blue\n# ", Plain(Assert.IsType<ProseBlock>(blocks[2]).Spans));
    }

    [Fact]
    public void A_table_needs_no_blank_lines_and_its_cells_carry_markup()
    {
        var blocks = ChatMarkup.Parse("Values:\n| Field | Value |\n|---|:-:|\n| **Name** | `My Light` |\n| Range | 10 m |\nDone.");
        Assert.Equal(3, blocks.Count);
        var table = Assert.IsType<TableBlock>(blocks[1]);
        Assert.Equal(["Field", "Value"], Cells(table.Header!));
        Assert.Equal(["Name", "My Light"], Cells(table.Rows[0]));
        Assert.True(table.Rows[0][0][0].Bold);
        Assert.True(table.Rows[0][1][0].Code);
        Assert.Equal("Done.", Plain(Assert.IsType<ProseBlock>(blocks[2]).Spans));
    }

    [Fact]
    public void Table_rows_are_fitted_to_the_header()
    {
        var table = Assert.IsType<TableBlock>(Assert.Single(ChatMarkup.Parse(
            "  | A | B | C |\n|---|---|---|\n| no end pipe | x\n| one |\n| 1 | 2 | 3 | 4 |\n| `a|b` | c\\|d | e |")));
        Assert.Equal(["no end pipe", "x", ""], Cells(table.Rows[0]));
        Assert.Equal(["one", "", ""], Cells(table.Rows[1]));
        Assert.Equal(["1", "2", "3 | 4"], Cells(table.Rows[2]));
        Assert.Equal(["a|b", "c|d", "e"], Cells(table.Rows[3]));
    }

    [Fact]
    public void An_empty_header_means_no_header_and_framed_rows_need_no_separator()
    {
        var headless = Assert.IsType<TableBlock>(Assert.Single(ChatMarkup.Parse("| | |\n|---|---|\n| Files | 13 |")));
        Assert.Null(headless.Header);
        Assert.Equal(["Files", "13"], Cells(headless.Rows[0]));

        var framed = Assert.IsType<TableBlock>(Assert.Single(ChatMarkup.Parse("| Part | Value |\n| Name | My Reactor |")));
        Assert.Equal(["Part", "Value"], Cells(framed.Header!));
        Assert.Single(framed.Rows);
    }

    [Fact]
    public void A_table_or_list_cut_off_while_streaming_still_parses()
    {
        Assert.IsType<ProseBlock>(Assert.Single(ChatMarkup.Parse("| Field | Value |")));
        var started = Assert.IsType<TableBlock>(Assert.Single(ChatMarkup.Parse("| Field | Value |\n|-")));
        Assert.Empty(started.Rows);
        var half = Assert.IsType<TableBlock>(Assert.Single(ChatMarkup.Parse("| Field | Value |\n|---|---|\n| Na")));
        Assert.Equal(["Na", ""], Cells(half.Rows[0]));
        var item = Assert.IsType<ListItemBlock>(Assert.Single(ChatMarkup.Parse("1.")));
        Assert.Empty(item.Spans);
    }
}
