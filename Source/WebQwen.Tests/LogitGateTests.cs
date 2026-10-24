using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace WebQwen.Tests;

// Teacher-forced comparison against the browser build's dumps in Golden\<model id>\:
// every reference token is fed back, so a disagreement at one position cannot
// cascade. Gated on top-1 agreement and KL over the reference top-20.
public sealed class LogitGateTests(ITestOutputHelper output)
{
    private const string ModelId = "Ternary-Bonsai-2-27B-MLC";
    private const int TopN = 20;

    private const double MinTop1 = 0.972;
    private const double MaxKlMedian = 0.002;
    private const double MaxKlP95 = 0.025;
    private const int MaxFirstTokenMisses = 1;

    [Fact]
    [Trait("Category", "Gpu")]
    public async Task MatchesBrowserBuild()
    {
        string modelsDir = FindModelsDir() ?? throw new InvalidOperationException(
            $"no .deps\\models above {AppContext.BaseDirectory}; run Build\\FetchModel.ps1 -ModelId {ModelId}");
        var model = Manifest.Load(Path.Combine(AppContext.BaseDirectory, "manifest.json")).Model(ModelId);
        using var engine = Engine.Create(new ModelFiles(model, Path.Combine(modelsDir, ModelId)));
        await engine.LoadModelAsync(model);

        var report = new StringBuilder();
        report.AppendLine("prompt                 pos    top1      KL p50      KL p95      KL max");
        var allKls = new List<double>();
        int agree = 0, firstMisses = 0;
        var goldenDir = Path.Combine(AppContext.BaseDirectory, "Golden", ModelId);
        var files = Directory.GetFiles(goldenDir, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            var golden = Golden.Load(file);
            var kls = new List<double>();
            int promptAgree = 0;
            await engine.ResetAsync();
            for (int k = 0; k <= golden.Continuation.Length; k++)
            {
                // First the whole prompt, then each reference token: the entry at
                // pos p is the distribution after tokens [0..p].
                int[] feed = k == 0 ? golden.Prompt : [golden.Continuation[k - 1]];
                int pos = golden.Prompt.Length - 1 + k;
                var scores = await engine.ScoreAsync(feed, TopN);
                if (!golden.Positions.TryGetValue(pos, out var reference)) continue;
                bool match = scores.Argmax == reference.Argmax;
                if (match) promptAgree++;
                else if (k == 0) firstMisses++;
                kls.Add(Metrics.KlTopN(reference.Top, scores.Top, TopN));
            }
            agree += promptAgree;
            allKls.AddRange(kls);
            kls.Sort();
            report.AppendLine(
                $"{Path.GetFileNameWithoutExtension(file),-20} {kls.Count,5}  {100.0 * promptAgree / kls.Count,6:F2}%  " +
                $"{Metrics.Quantile(kls, 0.5),10:F6}  {Metrics.Quantile(kls, 0.95),10:F6}  {kls[^1],10:F6}");
        }
        allKls.Sort();
        double top1 = (double)agree / allKls.Count;
        double median = Metrics.Quantile(allKls, 0.5), p95 = Metrics.Quantile(allKls, 0.95);
        report.AppendLine(
            $"ALL                  {allKls.Count,5}  {100 * top1,6:F2}%  {median,10:F6}  {p95,10:F6}  {allKls[^1],10:F6}  " +
            $"first-token misses {firstMisses}/{files.Length}");
        output.WriteLine(report.ToString());

        Assert.True(top1 >= MinTop1, $"top-1 agreement {top1:P2} < {MinTop1:P1}");
        Assert.True(median <= MaxKlMedian, $"median KL {median:F6} > {MaxKlMedian}");
        Assert.True(p95 <= MaxKlP95, $"p95 KL {p95:F6} > {MaxKlP95}");
        Assert.True(firstMisses <= MaxFirstTokenMisses, $"first token missed on {firstMisses} prompts");
    }

    private static string? FindModelsDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, ".deps", "models");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    private sealed record Position(int Argmax, (int Id, double Logprob)[] Top);

    private sealed record Golden(int[] Prompt, int[] Continuation, Dictionary<int, Position> Positions)
    {
        public static Golden Load(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = doc.RootElement;
            int[] Ints(string name) => root.GetProperty(name).EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var positions = new Dictionary<int, Position>();
            foreach (var p in root.GetProperty("positions").EnumerateArray())
            {
                var top = p.GetProperty("top").EnumerateArray().Select(t => (t[0].GetInt32(), t[1].GetDouble())).ToArray();
                positions[p.GetProperty("pos").GetInt32()] = new Position(p.GetProperty("argmax").GetInt32(), top);
            }
            return new Golden(Ints("prompt_tokens"), Ints("continuation_tokens"), positions);
        }
    }
}

// The metrics of WebQwen's bench/quality/compare.mjs.
internal static class Metrics
{
    // KL(P || Q) over the reference top-N ids, both sides renormalised over
    // exactly those ids. An id the candidate did not list gets the candidate's
    // smallest listed logprob.
    public static double KlTopN(IReadOnlyList<(int Id, double Logprob)> reference, IReadOnlyList<(int Id, double Logprob)> candidate, int topN)
    {
        var cand = new Dictionary<int, double>();
        foreach (var (id, lp) in candidate) cand.TryAdd(id, lp);
        double floor = candidate.Count > 0 ? candidate[^1].Logprob : -30;
        var p = reference.Take(topN).ToArray();
        var pl = p.Select(x => x.Logprob).ToArray();
        var ql = p.Select(x => cand.TryGetValue(x.Id, out double lq) ? lq : floor).ToArray();
        double zp = LogSumExp(pl), zq = LogSumExp(ql);
        double kl = 0;
        for (int i = 0; i < pl.Length; i++)
        {
            double lp = pl[i] - zp, lq = ql[i] - zq;
            kl += Math.Exp(lp) * (lp - lq);
        }
        return kl;
    }

    public static double Quantile(IReadOnlyList<double> sorted, double q)
    {
        if (sorted.Count == 0) return double.NaN;
        double idx = (sorted.Count - 1) * q;
        int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
        return lo == hi ? sorted[lo] : sorted[lo] + (sorted[hi] - sorted[lo]) * (idx - lo);
    }

    private static double LogSumExp(double[] xs)
    {
        double m = xs.Max();
        return m + Math.Log(xs.Sum(x => Math.Exp(x - m)));
    }
}
