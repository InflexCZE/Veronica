using Veronica.Agent;
using Veronica.Agent.Chat;
using Xunit;

namespace Veronica.Tests;

public sealed class TurnStatsTextTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.0k")]
    [InlineData(5432, "5.4k")]
    public void Tokens_get_a_k_from_a_thousand(long count, string expected) =>
        Assert.Equal(expected, TurnStatsText.Tokens(count));

    [Fact]
    public void No_rounds_no_footer() => Assert.Null(TurnStatsText.Footer([]));

    [Fact]
    public void One_round_reads_like_the_spec()
    {
        var round = new RoundStats(5100, 224, 385, 27) { CachedTokens = 76 };
        Assert.Equal("read 385 tok/s . write 27 tok/s . 5.1k read / 224 written . context 5.4k", TurnStatsText.Footer([round]));
    }

    [Fact]
    public void Rates_are_over_all_rounds_and_context_from_the_last()
    {
        // 1000 tokens in 1 s and 3000 in 2 s read 4000 in 3 s; 100 in 10 s and 100 in 2.5 s write 200 in 12.5 s.
        RoundStats[] rounds =
        [
            new RoundStats(1000, 100, 1000, 10) { CachedTokens = 0 },
            new RoundStats(3000, 100, 1500, 40) { CachedTokens = 1100 },
        ];
        Assert.Equal("read 1333 tok/s . write 16 tok/s . 4.0k read / 200 written . context 4.2k", TurnStatsText.Footer(rounds));
    }

    [Fact]
    public void Unknown_rates_and_context_are_left_out()
    {
        Assert.Equal("120 read / 0 written", TurnStatsText.Footer([new RoundStats(120, 0, 0, 0)]));
    }

    [Fact]
    public void Breakdown_lists_rounds_only_when_there_are_several()
    {
        var one = new RoundStats(100, 10, 100, 10);
        Assert.Null(TurnStatsText.Breakdown([one]));
        Assert.Equal(
            "Round 1: read 100 tok/s . write 10 tok/s . 100 read / 10 written\nRound 2: read 100 tok/s . write 10 tok/s . 100 read / 10 written",
            TurnStatsText.Breakdown([one, one]));
    }
}
