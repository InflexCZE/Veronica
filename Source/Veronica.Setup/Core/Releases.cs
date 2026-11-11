using System.IO.Compression;
using System.Net.Http.Headers;
using Veronica.Agent;

namespace Veronica.Setup.Core;

public static class ReleaseFeed
{
    public static async Task<Release> LatestAsync(HttpClient http, string url, CancellationToken cancellationToken) =>
        Release.Parse(await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false));

    public static HttpClient CreateClient(HttpMessageHandler? handler, SemVer? version, Action<string>? log = null)
    {
        var http = new HttpClient(new RetryHandler(handler ?? new HttpClientHandler()) { Log = log });
        http.Timeout = Timeout.InfiniteTimeSpan;
        // GitHub's API refuses requests without a user agent.
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Veronica", (version ?? new SemVer(0, 0, 0)).ToString()));
        return http;
    }
}

public sealed record InstallResult(SemVer Version, IReadOnlyList<MergeConflict> Conflicts, IReadOnlyList<ModelRecord> ModelsToReplace);

// Installs or updates Runtime, Skills and Wiki from a release's Veronica.zip (Docs/Tech/Distribution.md).
// The zip is downloaded and unpacked beside the install first, so a failure before the swap leaves the old
// Runtime working.
public sealed class ReleaseInstaller(HttpClient http, IFiles files, InstallLayout layout)
{
    public async Task<InstallResult> InstallAsync(Release release, IProgress<DownloadProgress>? progress, Func<Task> beforeSwap, CancellationToken cancellationToken)
    {
        var asset = release.Asset(Release.ZipAsset) ?? throw new DownloadException($"Release {release.Tag} has no {Release.ZipAsset}.");
        string zipPath = Path.Combine(layout.Downloads, Release.ZipAsset);
        files.Delete(zipPath);
        await DownloadAsync(asset, zipPath, progress, cancellationToken).ConfigureAwait(false);

        var content = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        files.DeleteDirectory(layout.RuntimeNew);
        using (var stream = files.OpenRead(zipPath))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    continue;
                string name = entry.FullName.Replace('\\', '/');
                int slash = name.IndexOf('/');
                string folder = slash > 0 ? name[..slash] : "";
                if (string.Equals(folder, "Runtime", StringComparison.OrdinalIgnoreCase))
                {
                    files.WriteAllBytes(SafePath.Under(layout.RuntimeNew, name[(slash + 1)..]), Read(entry));
                }
                else if (InstallLayout.ContentFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    SafePath.Under(layout.Root, name);
                    content[name] = Read(entry);
                }
            }
        }
        if (!files.FileExists(Path.Combine(layout.RuntimeNew, "manifest.json")))
            throw new DownloadException($"{Release.ZipAsset} of {release.Tag} has no Runtime/manifest.json.");

        cancellationToken.ThrowIfCancellationRequested();
        await beforeSwap().ConfigureAwait(false);
        SwapRuntime();

        var record = InstalledRecord.Load(files, layout);
        var pending = ContentMerge.Pending(files, layout);
        var plan = ContentMerge.Plan(record.Files, ContentMerge.Snapshot(files, layout), content, pending);
        ContentMerge.Apply(files, layout, plan);
        var dropped = ContentMerge.Dropped(record.Dropped, pending, plan);
        var shipped = content.ToDictionary(p => p.Key, p => Hashes.Sha256(p.Value), StringComparer.OrdinalIgnoreCase);
        var updated = record with { Version = release.Version, Files = shipped, Dropped = dropped.ToList() };
        updated.Save(files, layout);
        files.Delete(zipPath);

        var manifest = ModelCatalog.Parse(files.ReadAllText(layout.Manifest));
        var replace = manifest.Where(m => record.Models.TryGetValue(m.Id, out var pin) && pin != m.Pin).ToList();
        return new InstallResult(release.Version, ContentMerge.Conflicts(files, layout, updated), replace);
    }

    private void SwapRuntime()
    {
        files.DeleteDirectory(layout.RuntimeOld);
        bool hadRuntime = files.DirectoryExists(layout.Runtime);
        if (hadRuntime)
            files.MoveDirectory(layout.Runtime, layout.RuntimeOld);
        try
        {
            files.MoveDirectory(layout.RuntimeNew, layout.Runtime);
        }
        catch
        {
            if (hadRuntime && !files.DirectoryExists(layout.Runtime))
                files.MoveDirectory(layout.RuntimeOld, layout.Runtime);
            throw;
        }
        try
        {
            files.DeleteDirectory(layout.RuntimeOld);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private async Task DownloadAsync(ReleaseAsset asset, string path, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? asset.Size;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var sink = files.OpenAppend(path);
        var buffer = new byte[1 << 16];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await sink.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            done += read;
            progress?.Report(new DownloadProgress(done, total, asset.Name));
        }
    }

    private static byte[] Read(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}

// Windows refuses to overwrite a running exe but lets it be renamed (Docs/Tech/Distribution.md, Updates).
public sealed class SelfUpdate(HttpClient http, IFiles files, InstallLayout layout)
{
    public const string OldSuffix = ".old";

    public static bool IsNeeded(Release release, SemVer? running) =>
        release.Asset(Release.ExeAsset) != null && (running == null || release.Version > running.Value);

    // Returns the downloaded exe; Swap puts it in place.
    public async Task<string> DownloadAsync(Release release, CancellationToken cancellationToken)
    {
        var asset = release.Asset(Release.ExeAsset)!;
        string download = Path.Combine(layout.Downloads, Release.ExeAsset);
        files.Delete(download);
        using (var response = await http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var sink = files.OpenAppend(download);
            await source.CopyToAsync(sink, cancellationToken).ConfigureAwait(false);
        }
        if (asset.Size >= 0 && files.Length(download) != asset.Size)
            throw new DownloadException($"The downloaded {Release.ExeAsset} has {files.Length(download)} bytes, the release lists {asset.Size}.");
        return download;
    }

    public void Swap(string runningExe, string download)
    {
        files.Move(runningExe, runningExe + OldSuffix, overwrite: true);
        try
        {
            files.Move(download, layout.Exe, overwrite: true);
        }
        catch
        {
            if (!files.FileExists(runningExe))
                files.Move(runningExe + OldSuffix, runningExe, overwrite: false);
            throw;
        }
    }

    // The previous exe, renamed aside by the update that started this one.
    public void CleanUp(string runningExe)
    {
        foreach (var old in new[] { runningExe + OldSuffix, layout.Exe + OldSuffix })
        {
            try
            {
                files.Delete(old);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
