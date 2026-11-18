using Veronica.Agent.Claude;
using Xunit;

namespace Veronica.Tests;

public sealed class ClaudeCommandTests
{
    [Fact]
    public void Every_argument_is_quoted_so_cmd_metacharacters_stay_text()
    {
        Assert.Equal("claude \"-p\" \"--tools\" \"\" \"mcp__veronica__*\" \"C:\\A & B\\p.md\"",
            ClaudeCommand.CommandLine(["-p", "--tools", "", "mcp__veronica__*", "C:\\A & B\\p.md"]));
    }

    [Fact]
    public void Trailing_backslashes_are_doubled_so_the_closing_quote_survives()
    {
        Assert.Equal("claude \"C:\\dir\\\\\"", ClaudeCommand.CommandLine(["C:\\dir\\"]));
    }

    [Fact]
    public void An_argument_with_a_quote_is_refused()
    {
        Assert.Throws<ArgumentException>(() => ClaudeCommand.CommandLine(["{\"a\":1}"]));
    }

    [Fact]
    public void Runs_through_cmd_with_the_whole_line_quoted()
    {
        var info = ClaudeCommand.StartInfo(["auth", "status"]);
        Assert.EndsWith("cmd.exe", info.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/d /s /c \"claude \"auth\" \"status\" & exit /b\"", info.Arguments);
    }
}
