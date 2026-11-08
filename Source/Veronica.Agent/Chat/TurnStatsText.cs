using System.Globalization;

namespace Veronica.Agent.Chat;

// The footer under a finished turn: speeds over all its rounds, the context as the last round left it.
public static class TurnStatsText
{
    private const string Separator = " . ";

    public static string Tokens(long count) =>
        count < 1000 ? count.ToString(CultureInfo.InvariantCulture) : (count / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k";

    // Null without rounds.
    public static string? Footer(IReadOnlyList<RoundStats> rounds)
    {
        if (rounds.Count == 0)
            return null;

        var parts = new List<string>();
        if (Rate(rounds, r => r.PromptTokens, r => r.PrefillTokensPerSecond) is { } read)
            parts.Add($"read {read} tok/s");
        if (Rate(rounds, r => r.CompletionTokens, r => r.DecodeTokensPerSecond) is { } write)
            parts.Add($"write {write} tok/s");
        parts.Add($"{Tokens(rounds.Sum(r => (long)r.PromptTokens))} read / {Tokens(rounds.Sum(r => (long)r.CompletionTokens))} written");
        if (rounds[^1].ContextTokens is { } context)
            parts.Add($"context {Tokens(context)}");
        return string.Join(Separator, parts);
    }

    // Null below two rounds: the footer already says it all.
    public static string? Breakdown(IReadOnlyList<RoundStats> rounds) =>
        rounds.Count < 2
            ? null
            : string.Join("\n", rounds.Select((r, i) => $"Round {i + 1}: {Footer([r])}"));

    private static long? Rate(IReadOnlyList<RoundStats> rounds, Func<RoundStats, int> tokens, Func<RoundStats, double> perSecond)
    {
        double count = 0;
        double seconds = 0;
        foreach (var r in rounds)
        {
            double rate = perSecond(r);
            if (rate <= 0 || tokens(r) <= 0)
                continue;
            count += tokens(r);
            seconds += tokens(r) / rate;
        }
        return seconds > 0 ? (long)Math.Round(count / seconds) : null;
    }
}
