using System.IO.Compression;
using System.Text;
using Veronica.Agent;
using Veronica.Setup.Core;
using Xunit;

namespace Veronica.Setup.Tests;

public class ModelDownloaderTests
{
    private const string Rev = "bf4c77f054f290b3901fc3e2d06dc0e40d2095fa";
    private const string LibRev = "7e1ed3c1825b61c42fbb672f91b336a3984009c5";
    private const string Base = "https://huggingface.co/InflexCZE/Tiny-MLC/resolve/" + Rev + "/T3/";
    private const string LibUrl = "https://huggingface.co/InflexCZE/Tiny-MLC/resolve/" + LibRev + "/T3/webgpu.wasm";
    private const string ListingUrl = "https://huggingface.co/api/models/InflexCZE/Tiny-MLC/tree/" + Rev + "/T3?recursive=true";

    private static readonly InstallLayout Layout = new(@"C:\V");
    private static readonly byte[] Shard = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();
    private static readonly byte[] Config = Encoding.UTF8.GetBytes("{\"context_window_size\": 4096}");
    private static readonly byte[] Lib = Encoding.UTF8.GetBytes("wasm library bytes");

    private static ModelRecord Model() => ModelCatalog.Parse($$"""
        {
          "models": [
            {
              "model_id": "Tiny-MLC",
              "url": "{{Base}}",
              "weights_bytes": {{Shard.Length + Config.Length}},
              "vram_required_MB": 6900,
              "lib": { "url": "{{LibUrl}}", "sha256": "{{Hashes.Sha256(Lib)}}", "bytes": {{Lib.Length}} }
            }
          ]
        }
        """).Single();

    private static string GitSha1(byte[] bytes) => Hashes.GitBlobSha1(new MemoryStream(bytes), bytes.Length);

    private static string Listing() => $$"""
        [
          { "type": "directory", "oid": "x", "size": 0, "path": "T3" },
          { "type": "file", "oid": "{{GitSha1(Config)}}", "size": {{Config.Length}}, "path": "T3/mlc-chat-config.json" },
          { "type": "file", "oid": "0123", "size": {{Shard.Length}}, "path": "T3/params_shard_0.bin",
            "lfs": { "oid": "{{Hashes.Sha256(Shard)}}", "size": {{Shard.Length}}, "pointerSize": 134 } },
          { "type": "file", "oid": "4567", "size": 9, "path": "T3/webgpu.wasm" }
        ]
        """;

    private static (MemoryFiles Files, FakeHttp Http) Serve(byte[]? shard = null, int cuts = 0, bool honoursRange = true)
    {
        var http = new FakeHttp();
        http.Serve(ListingUrl, Listing());
        http.Serve(Base + "mlc-chat-config.json", Config);
        http.Serve(Base + "params_shard_0.bin", shard ?? Shard, honoursRange, cuts, cutAfter: 1200);
        http.Serve(LibUrl, Lib);
        return (new MemoryFiles(), http);
    }

    private static void AssertInstalled(MemoryFiles files)
    {
        Assert.Equal(Shard, files.ReadAllBytes(@"C:\V\Models\Tiny-MLC\params_shard_0.bin"));
        Assert.Equal(Config, files.ReadAllBytes(@"C:\V\Models\Tiny-MLC\mlc-chat-config.json"));
        Assert.Equal(Lib, files.ReadAllBytes(@"C:\V\Models\Tiny-MLC\webgpu.wasm"));        Assert.False(files.DirectoryExists(@"C:\V\Downloads\Tiny-MLC"));
    }

    [Fact]
    public async Task Downloads_Verifies_AndMovesIntoModels()
    {
        var (files, http) = Serve();
        var reports = new List<DownloadProgress>();

        await new ModelDownloader(http.Client(), files, Layout).DownloadAsync(Model(), new SyncProgress(reports.Add), CancellationToken.None);

        AssertInstalled(files);
        Assert.Equal(Shard.Length + Config.Length + Lib.Length, reports[^1].Done);
        Assert.Equal(reports[^1].Total, reports[^1].Done);
    }

    [Fact]
    public async Task CutConnection_ResumesWithARangeRequest()
    {
        var (files, http) = Serve(cuts: 2);

        await new ModelDownloader(http.Client(), files, Layout) { Delay = Record([]) }.DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
        var ranges = http.Requests.Where(r => r.RequestUri!.ToString().EndsWith("params_shard_0.bin")).Select(r => r.Headers.Range?.Ranges.Single().From).ToList();
        Assert.Equal([null, 1200L, 2400L], ranges);
    }

    [Fact]
    public async Task CutConnection_WaitsLongerBeforeEachResume()
    {
        var (files, http) = Serve(cuts: 2);
        var waits = new List<TimeSpan>();

        await new ModelDownloader(http.Client(), files, Layout) { Delay = Record(waits) }.DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], waits);
    }

    [Fact]
    public async Task OverloadedListing_IsRetriedWithBackoff()
    {
        var (files, http) = Serve();
        http.Serve(ListingUrl, Listing()).FailuresLeft = 3;
        var waits = new List<TimeSpan>();
        var log = new List<string>();

        await new ModelDownloader(Retrying(http, waits, log), files, Layout).DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], waits);
        Assert.Equal(3, log.Count);
        Assert.Contains("429", log[0]);
    }

    [Fact]
    public async Task RetryAfter_IsWaitedInFull()
    {
        var (files, http) = Serve();
        var route = http.Serve(Base + "params_shard_0.bin", Shard);
        route.FailuresLeft = 1;
        route.FailWith = System.Net.HttpStatusCode.ServiceUnavailable;
        route.RetryAfter = TimeSpan.FromSeconds(45);
        var waits = new List<TimeSpan>();

        await new ModelDownloader(Retrying(http, waits), files, Layout).DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
        Assert.Equal([TimeSpan.FromSeconds(45)], waits);
    }

    [Fact]
    public async Task RetryAfterBeyondTheBudget_FailsAtOnce()
    {
        var (files, http) = Serve();
        var route = http.Serve(Base + "params_shard_0.bin", Shard);
        route.FailuresLeft = 1;
        route.RetryAfter = RetryHandler.Budget + TimeSpan.FromSeconds(1);
        var waits = new List<TimeSpan>();

        var e = await Assert.ThrowsAsync<HttpRequestException>(() => new ModelDownloader(Retrying(http, waits), files, Layout).DownloadAsync(Model(), null, CancellationToken.None));

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, e.StatusCode);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task RefusedConnection_IsRetriedByTheClient()
    {
        var (files, http) = Serve();
        http.Serve(ListingUrl, Listing()).DropsLeft = 2;
        var waits = new List<TimeSpan>();

        await new ModelDownloader(Retrying(http, waits), files, Layout).DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], waits);
    }

    [Fact]
    public async Task RetryAfterARedirect_StartsFromTheOriginalUrl_AndLogsOnlyIt()
    {
        var (files, http) = Serve();
        const string Signed = "https://cdn.example/shard?X-Amz-Signature=secret";
        var redirecting = new Redirecting(http, Base + "params_shard_0.bin", Signed);
        var log = new List<string>();
        var client = new HttpClient(new RetryHandler(redirecting) { Delay = Record([]), Log = log.Add });

        await new ModelDownloader(client, files, Layout).DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
        Assert.Equal([Base + "params_shard_0.bin", Base + "params_shard_0.bin"], redirecting.Seen);
        Assert.DoesNotContain(log, l => l.Contains("secret"));
    }

    // Stands in for the redirect handler: it rewrites RequestUri to the target, which then fails once.
    private sealed class Redirecting(FakeHttp inner, string from, string to) : DelegatingHandler(inner)
    {
        private bool _failed;

        public List<string> Seen { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.ToString() != from)
                return base.SendAsync(request, cancellationToken);
            Seen.Add(request.RequestUri.ToString());
            if (_failed)
                return base.SendAsync(request, cancellationToken);
            _failed = true;
            request.RequestUri = new Uri(to);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable) { RequestMessage = request });
        }
    }

    [Fact]
    public async Task LastingOverload_GivesUpAfterTheHandlersRetries_WithoutTheDownloaderRetryingAgain()
    {
        var (files, http) = Serve();
        http.Serve(Base + "params_shard_0.bin", Shard).FailuresLeft = int.MaxValue;
        var waits = new List<TimeSpan>();

        var e = await Assert.ThrowsAsync<HttpRequestException>(() => new ModelDownloader(Retrying(http, waits), files, Layout) { Delay = Record(waits) }.DownloadAsync(Model(), null, CancellationToken.None));

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, e.StatusCode);
        Assert.Equal(new[] { 1, 2, 4, 8, 16, 30, 30, 30 }.Select(s => TimeSpan.FromSeconds(s)), waits);
        Assert.Equal(RetryHandler.Retries + 1, http.Requests.Count(r => r.RequestUri!.ToString().EndsWith("params_shard_0.bin")));
        Assert.False(files.DirectoryExists(@"C:\V\Models\Tiny-MLC"));
    }

    [Fact]
    public async Task MissingFile_FailsWithoutRetrying()
    {
        var (files, http) = Serve();
        var route = http.Serve(Base + "params_shard_0.bin", Shard);
        route.FailuresLeft = 1;
        route.FailWith = System.Net.HttpStatusCode.NotFound;
        var waits = new List<TimeSpan>();

        await Assert.ThrowsAsync<HttpRequestException>(() => new ModelDownloader(Retrying(http, waits), files, Layout) { Delay = Record(waits) }.DownloadAsync(Model(), null, CancellationToken.None));

        Assert.Empty(waits);
        Assert.Single(http.Requests, r => r.RequestUri!.ToString().EndsWith("params_shard_0.bin"));
    }

    private static Func<TimeSpan, CancellationToken, Task> Record(List<TimeSpan> waits) => (wait, _) =>
    {
        waits.Add(wait);
        return Task.CompletedTask;
    };

    private static HttpClient Retrying(FakeHttp http, List<TimeSpan> waits, List<string>? log = null) =>
        new(new RetryHandler(http) { Delay = Record(waits), Log = log == null ? null : log.Add });

    [Fact]
    public async Task PartialFileFromAnEarlierRun_IsContinued()
    {
        var (files, http) = Serve();
        files.WriteAllBytes(@"C:\V\Downloads\Tiny-MLC\params_shard_0.bin.part", Shard[..3000]);

        await new ModelDownloader(http.Client(), files, Layout).DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
        Assert.Equal(3000L, http.Requests.Single(r => r.RequestUri!.ToString().EndsWith("params_shard_0.bin")).Headers.Range!.Ranges.Single().From);
    }

    [Fact]
    public async Task ServerIgnoringTheRange_StartsTheFileOver()
    {
        var (files, http) = Serve(honoursRange: false);
        files.WriteAllBytes(@"C:\V\Downloads\Tiny-MLC\params_shard_0.bin.part", Shard[..3000]);

        await new ModelDownloader(http.Client(), files, Layout).DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
    }

    [Fact]
    public async Task CorruptFile_FailsAndInstallsNothing()
    {
        var corrupt = Shard.ToArray();
        corrupt[10] ^= 0xFF;
        var (files, http) = Serve(shard: corrupt);

        await Assert.ThrowsAsync<DownloadException>(() => new ModelDownloader(http.Client(), files, Layout).DownloadAsync(Model(), null, CancellationToken.None));

        Assert.False(files.DirectoryExists(@"C:\V\Models\Tiny-MLC"));
        Assert.False(files.FileExists(@"C:\V\Downloads\Tiny-MLC\params_shard_0.bin.part"));
    }

    [Fact]
    public async Task StagingLeftovers_DoNotReachTheModelFolder()
    {
        var (files, http) = Serve();
        files.WriteAllText(@"C:\V\Downloads\Tiny-MLC\params_shard_9.bin", "earlier revision");
        files.WriteAllText(@"C:\V\Downloads\Tiny-MLC\old.bin.part", "half");

        await new ModelDownloader(http.Client(), files, Layout).DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
        Assert.Equal(3, files.Files(@"C:\V\Models\Tiny-MLC", recursive: true).Count());
    }

    [Fact]
    public async Task ListingNameLeavingTheFolder_IsRefused()
    {
        var (files, http) = Serve();
        http.Serve(ListingUrl, Listing().Replace("T3/mlc-chat-config.json", "T3/../../evil.json"));

        await Assert.ThrowsAsync<FormatException>(() => new ModelDownloader(http.Client(), files, Layout).DownloadAsync(Model(), null, CancellationToken.None));

        Assert.Empty(files.All);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("C:x")]
    public void ModelIdThatIsNotOneFolderName_IsRefused(string id) =>
        Assert.Throws<FormatException>(() => ModelCatalog.Parse("{ \"models\": [ { \"model_id\": \"" + id + "\", \"url\": \"" + Base + "\", \"lib\": { \"url\": \"" + LibUrl + "\", \"sha256\": \"ab\" } } ] }"));

    [Fact]
    public async Task NewRevision_ReplacesTheInstalledFolder()
    {
        var (files, http) = Serve();
        files.WriteAllText(@"C:\V\Models\Tiny-MLC\stale.bin", "old revision");

        await new ModelDownloader(http.Client(), files, Layout).DownloadAsync(Model(), null, CancellationToken.None);

        AssertInstalled(files);
        Assert.False(files.FileExists(@"C:\V\Models\Tiny-MLC\stale.bin"));
    }

    private sealed class SyncProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}

public class ReleaseInstallerTests
{
    private const string ZipUrl = "http://gitea/Inflex/Veronica/releases/download/v1.1.0/Veronica.zip";
    private static readonly InstallLayout Layout = new(@"C:\V");

    private const string Manifest = """
        {
          "models": [
            {
              "model_id": "Tiny-MLC",
              "url": "https://huggingface.co/InflexCZE/Tiny-MLC/resolve/bf4c77f054f290b3901fc3e2d06dc0e40d2095fa/T3/",
              "weights_bytes": 10,
              "vram_required_MB": 6900,
              "lib": { "url": "https://huggingface.co/InflexCZE/Tiny-MLC/resolve/7e1ed3c1825b61c42fbb672f91b336a3984009c5/T3/webgpu.wasm", "sha256": "ab", "bytes": 2 }
            }
          ]
        }
        """;

    private static byte[] Zip(params (string Name, string Text)[] entries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(text));
            }
        }
        return memory.ToArray();
    }

    private static Release ReleaseOf(string version) => Release.Parse($$"""
        { "tag_name": "v{{version}}", "assets": [ { "name": "Veronica.zip", "size": -1, "browser_download_url": "{{ZipUrl}}" } ] }
        """);

    private static Task<InstallResult> Install(MemoryFiles files, string version, params (string, string)[] entries)
    {
        var http = new FakeHttp();
        http.Serve(ZipUrl, Zip(entries));
        return new ReleaseInstaller(http.Client(), files, Layout).InstallAsync(ReleaseOf(version), null, () => Task.CompletedTask, CancellationToken.None);
    }

    [Fact]
    public async Task FreshInstall_ExtractsRuntimeAndContent_AndRecordsWhatItShipped()
    {
        var files = new MemoryFiles();

        var result = await Install(files, "1.0.0",
            ("Runtime/Veronica.dll", "dll1"), ("Runtime/manifest.json", Manifest), ("Skills/a.md", "a1"), ("Wiki/p.md", "p1"));

        Assert.Equal("dll1", files.ReadAllText(@"C:\V\Runtime\Veronica.dll"));
        Assert.Equal("a1", files.ReadAllText(@"C:\V\Skills\a.md"));
        Assert.Empty(result.Conflicts);
        Assert.False(files.FileExists(@"C:\V\Downloads\Veronica.zip"));
        var record = InstalledRecord.Load(files, Layout);
        Assert.Equal(new SemVer(1, 0, 0), record.Version);
        Assert.Equal(Hashes.Sha256(Encoding.UTF8.GetBytes("p1")), record.Files["Wiki/p.md"]);
    }

    [Fact]
    public async Task Update_ReplacesRuntime_MergesContent_AndNamesModelsToReplace()
    {
        var files = new MemoryFiles();
        await Install(files, "1.0.0",
            ("Runtime/Veronica.dll", "dll1"), ("Runtime/old.dll", "gone"), ("Runtime/manifest.json", Manifest),
            ("Skills/a.md", "a1"), ("Skills/b.md", "b1"), ("Wiki/p.md", "p1"));
        files.WriteAllText(@"C:\V\Skills\a.md", "my a");
        files.WriteAllText(@"C:\V\Skills\mine.md", "own skill");
        InstalledRecord.Load(files, Layout).WithModel("Tiny-MLC", "an older pin").Save(files, Layout);

        var result = await Install(files, "1.1.0",
            ("Runtime/Veronica.dll", "dll2"), ("Runtime/manifest.json", Manifest), ("Skills/a.md", "a2"), ("Skills/b.md", "b2"));

        Assert.Equal("dll2", files.ReadAllText(@"C:\V\Runtime\Veronica.dll"));
        Assert.False(files.FileExists(@"C:\V\Runtime\old.dll"));
        Assert.Equal("my a", files.ReadAllText(@"C:\V\Skills\a.md"));
        Assert.Equal("a2", files.ReadAllText(@"C:\V\Skills\a.new.md"));
        Assert.Equal("b2", files.ReadAllText(@"C:\V\Skills\b.md"));
        Assert.Equal("own skill", files.ReadAllText(@"C:\V\Skills\mine.md"));
        Assert.False(files.FileExists(@"C:\V\Wiki\p.md"));
        Assert.Equal([@"C:\V\Skills\a.md"], result.Conflicts.Select(c => c.Mine));
        Assert.Equal(["Tiny-MLC"], result.ModelsToReplace.Select(m => m.Id));
        Assert.Equal("an older pin", InstalledRecord.Load(files, Layout).Models["Tiny-MLC"]);
    }

    [Fact]
    public void SelfUpdate_RenamesTheRunningExeAside_AndCleansUpAfterTheRestart()
    {
        var files = new MemoryFiles();
        files.WriteAllText(@"C:\V\Veronica.exe", "running");
        files.WriteAllText(@"C:\V\Downloads\Veronica.exe", "new");
        var update = new SelfUpdate(new FakeHttp().Client(), files, Layout);

        update.Swap(@"C:\V\Veronica.exe", @"C:\V\Downloads\Veronica.exe");

        Assert.Equal("new", files.ReadAllText(@"C:\V\Veronica.exe"));
        Assert.Equal("running", files.ReadAllText(@"C:\V\Veronica.exe.old"));
        update.CleanUp(@"C:\V\Veronica.exe");
        Assert.False(files.FileExists(@"C:\V\Veronica.exe.old"));
    }

    [Fact]
    public async Task FailedRuntimeSwap_RestoresTheOldRuntime()
    {
        var files = new MemoryFiles();
        await Install(files, "1.0.0", ("Runtime/Veronica.dll", "dll1"), ("Runtime/manifest.json", Manifest), ("Skills/a.md", "a1"));
        bool failed = false;
        files.FailMoveTo = to =>
        {
            if (failed || !to.Equals(@"C:\V\Runtime", StringComparison.OrdinalIgnoreCase) || !files.FileExists(@"C:\V\Runtime.new\Veronica.dll"))
                return false;
            failed = true;
            return true;
        };

        await Assert.ThrowsAsync<IOException>(() => Install(files, "1.1.0", ("Runtime/Veronica.dll", "dll2"), ("Runtime/manifest.json", Manifest), ("Skills/a.md", "a2")));

        Assert.Equal("dll1", files.ReadAllText(@"C:\V\Runtime\Veronica.dll"));
        Assert.Equal("a1", files.ReadAllText(@"C:\V\Skills\a.md"));
        Assert.Equal(new SemVer(1, 0, 0), InstalledRecord.Load(files, Layout).Version);
    }

    [Fact]
    public async Task EditorCheckRunsBeforeTheSwap_WithTheNewRuntimeAlreadyUnpacked()
    {
        var files = new MemoryFiles();
        var http = new FakeHttp();
        http.Serve(ZipUrl, Zip(("Runtime/Veronica.dll", "dll1"), ("Runtime/manifest.json", Manifest)));
        bool unpacked = false;

        await new ReleaseInstaller(http.Client(), files, Layout).InstallAsync(ReleaseOf("1.0.0"), null, () =>
        {
            unpacked = files.FileExists(@"C:\V\Runtime.new\Veronica.dll") && !files.DirectoryExists(@"C:\V\Runtime");
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.True(unpacked);
        Assert.False(files.DirectoryExists(@"C:\V\Runtime.new"));
    }

    [Theory]
    [InlineData("Runtime/../evil.dll")]
    [InlineData("Skills/../../evil.md")]
    [InlineData("Wiki/C:/evil.md")]
    public async Task ZipEntryLeavingItsFolder_IsRefused(string entry)
    {
        var files = new MemoryFiles();

        await Assert.ThrowsAsync<FormatException>(() => Install(files, "1.0.0", ("Runtime/manifest.json", Manifest), (entry, "x")));

        Assert.False(files.DirectoryExists(@"C:\V\Runtime"));
        Assert.DoesNotContain(files.All.Keys, k => k.Contains("evil"));
    }

    [Fact]
    public void SelfUpdate_FailedMoveIn_PutsTheRunningExeBack()
    {
        var files = new MemoryFiles();
        files.WriteAllText(@"C:\V\Veronica.exe", "running");
        files.WriteAllText(@"C:\V\Downloads\Veronica.exe", "new");
        bool failed = false;
        files.FailMoveTo = to =>
        {
            if (failed || !to.Equals(@"C:\V\Veronica.exe", StringComparison.OrdinalIgnoreCase))
                return false;
            failed = true;
            return true;
        };

        Assert.Throws<IOException>(() => new SelfUpdate(new FakeHttp().Client(), files, Layout).Swap(@"C:\V\Veronica.exe", @"C:\V\Downloads\Veronica.exe"));

        files.FailMoveTo = null;
        Assert.Equal("running", files.ReadAllText(@"C:\V\Veronica.exe"));
        Assert.False(files.FileExists(@"C:\V\Veronica.exe.old"));
    }

    [Fact]
    public void SelfUpdate_IsNeededOnlyForANewerReleaseCarryingAnExe()
    {
        var withExe = Release.Parse("""{ "tag_name": "v1.1.0", "assets": [ { "name": "Veronica.exe", "browser_download_url": "http://x/Veronica.exe" } ] }""");

        Assert.True(SelfUpdate.IsNeeded(withExe, new SemVer(1, 0, 0)));
        Assert.False(SelfUpdate.IsNeeded(withExe, new SemVer(1, 1, 0)));
        Assert.False(SelfUpdate.IsNeeded(ReleaseOf("2.0.0"), new SemVer(1, 0, 0)));
    }

    [Fact]
    public void ForeignEntries_ListsWhatIsNotVeronicas()
    {
        var foreign = InstallLayout.ForeignEntries(["Runtime", "Models", "Veronica (1).exe", "Veronica.settings.json", "holiday.jpg", "Downloads", "setup.zip"], "Veronica (1).exe");

        Assert.Equal(["holiday.jpg", "setup.zip"], foreign);
    }
}
