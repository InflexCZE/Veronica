using System.Text.Json;
using System.Text.RegularExpressions;

namespace Veronica.Setup.Core;

// A model of the WebQwen manifest, pinned to Hugging Face revisions.
public sealed record ModelRecord(
    string Id,
    string Url,
    string Repo,
    string Revision,
    string Subdir,
    long WeightsBytes,
    int VramRequiredMb,
    string LibUrl,
    string LibSha256,
    long LibBytes)
{
    // What an installed copy must match; a release that pins anything else replaces it.
    public string Pin => $"{Url}|{LibSha256}";

    public long DownloadBytes => WeightsBytes + LibBytes;

    // What modders see in place of the id; the vendored manifest carries it (pull-webqwen re-applies it).
    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    // The graphics card size modders are told to have: the model's own need plus room for the Editor.
    public int? GraphicsCardGb { get; init; }
}

public static partial class ModelCatalog
{
    [GeneratedRegex(@"^https://huggingface\.co/([^/]+/[^/]+)/resolve/([0-9a-f]{40})/(.*?)/?$")]
    private static partial Regex HuggingFaceUrl();

    public static IReadOnlyList<ModelRecord> Parse(string manifest)
    {
        using var doc = JsonDocument.Parse(manifest);
        var models = new List<ModelRecord>();
        foreach (var m in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            string url = m.GetProperty("url").GetString()!;
            var match = HuggingFaceUrl().Match(url);
            if (!match.Success)
                throw new FormatException($"Model url '{url}' is not a pinned Hugging Face folder.");
            var lib = m.GetProperty("lib");
            models.Add(new ModelRecord(
                SafePath.Segment(m.GetProperty("model_id").GetString()!),
                url,
                match.Groups[1].Value,
                match.Groups[2].Value,
                match.Groups[3].Value,
                m.TryGetProperty("weights_bytes", out var w) ? w.GetInt64() : 0,
                m.TryGetProperty("vram_required_MB", out var v) ? v.GetInt32() : 0,
                lib.GetProperty("url").GetString()!,
                lib.GetProperty("sha256").GetString()!,
                lib.TryGetProperty("bytes", out var b) ? b.GetInt64() : -1)
            {
                DisplayName = m.TryGetProperty("display_name", out var n) ? n.GetString() : null,
                Description = m.TryGetProperty("description", out var d) ? d.GetString() : null,
                GraphicsCardGb = m.TryGetProperty("graphics_card_GB", out var g) ? g.GetInt32() : null,
            });
        }
        return models;
    }
}

// One file of a Hugging Face revision: LFS files are checked by sha256, small ones by git blob sha1.
public sealed record HubFile(string Name, long Size, string? Sha256, string? GitSha1);

public static class HubListing
{
    public const int PageLimit = 1000;

    public static string Url(ModelRecord model)
    {
        var uri = new Uri(model.Url);
        return $"{uri.Scheme}://{uri.Authority}/api/models/{model.Repo}/tree/{model.Revision}/{model.Subdir}?recursive=true";
    }

    // The model's files relative to its folder, without the library, which has its own pinned revision.
    public static IReadOnlyList<HubFile> Parse(string json, ModelRecord model)
    {
        using var doc = JsonDocument.Parse(json);
        var files = new List<HubFile>();
        int count = 0;
        string prefix = model.Subdir.Length == 0 ? "" : model.Subdir.TrimEnd('/') + "/";
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            count++;
            if (entry.GetProperty("type").GetString() != "file")
                continue;
            string path = entry.GetProperty("path").GetString()!;
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            string name = path[prefix.Length..];
            if (name == "webgpu.wasm")
                continue;
            string? sha256 = entry.TryGetProperty("lfs", out var lfs) && lfs.ValueKind == JsonValueKind.Object ? lfs.GetProperty("oid").GetString() : null;
            string? sha1 = sha256 == null && entry.TryGetProperty("oid", out var oid) ? oid.GetString() : null;
            files.Add(new HubFile(name, entry.GetProperty("size").GetInt64(), sha256, sha1));
        }
        if (count >= PageLimit)
            throw new FormatException("The Hugging Face listing may be paginated; that is not handled.");
        return files;
    }
}

public readonly record struct DownloadProgress(long Done, long Total, string File);

public sealed class DownloadException(string message) : Exception(message);

// Downloads a model into Downloads\<id>, verifies every file, then moves it into Models\<id>
// (Docs/Tech/Distribution.md, Model). A partial file resumes where it stopped, in this run or the next.
public sealed class ModelDownloader(HttpClient http, IFiles files, InstallLayout layout)
{
    public int Attempts { get; init; } = 5;

    public async Task DownloadAsync(ModelRecord model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        string listing = await http.GetStringAsync(HubListing.Url(model), cancellationToken).ConfigureAwait(false);
        var hubFiles = HubListing.Parse(listing, model);
        var all = hubFiles.Select(f => (Url: model.Url.TrimEnd('/') + "/" + f.Name, f.Name, f.Size, Check: (Func<Stream, long, bool>)((s, n) => Matches(f, s, n))))
            .Append((Url: model.LibUrl, Name: "webgpu.wasm", Size: model.LibBytes, Check: (s, _) => string.Equals(Hashes.Sha256(s), model.LibSha256, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        string staging = Path.Combine(layout.Downloads, model.Id);
        var targets = all.Select(f => SafePath.Under(staging, f.Name)).ToList();
        long total = all.Sum(f => Math.Max(0, f.Size));
        long done = 0;
        for (int i = 0; i < all.Count; i++)
        {
            var file = all[i];
            string target = targets[i];
            long before = done;
            await FetchAsync(file.Url, target, file.Size, file.Check, bytes => progress?.Report(new DownloadProgress(before + bytes, total, file.Name)), cancellationToken).ConfigureAwait(false);
            done += Math.Max(0, file.Size);
            progress?.Report(new DownloadProgress(done, total, file.Name));
        }

        // Leftovers of an earlier revision's download must not land in the model's folder.
        var keep = targets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in files.Files(staging, recursive: true).Where(f => !keep.Contains(Path.GetFullPath(f))).ToList())
            files.Delete(stale);

        string folder = layout.ModelFolder(model.Id);
        files.DeleteDirectory(folder);
        files.MoveDirectory(staging, folder);
    }

    private async Task FetchAsync(string url, string target, long size, Func<Stream, long, bool> check, Action<long> report, CancellationToken cancellationToken)
    {
        if (files.FileExists(target) && Verified(target, size, check))
            return;
        files.Delete(target);
        string part = target + ".part";
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await ResumeAsync(url, part, size, report, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (Exception e) when (e is IOException or HttpRequestException && attempt < Attempts && !cancellationToken.IsCancellationRequested)
            {
            }
        }
        if (!Verified(part, size, check))
        {
            files.Delete(part);
            throw new DownloadException($"{Path.GetFileName(target)} does not match its pinned revision ({url}).");
        }
        files.Move(part, target, overwrite: true);
    }

    private async Task ResumeAsync(string url, string part, long size, Action<long> report, CancellationToken cancellationToken)
    {
        long have = files.FileExists(part) ? files.Length(part) : 0;
        if (size >= 0 && have > size)
        {
            files.Delete(part);
            have = 0;
        }
        if (size >= 0 && have == size)
            return;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (have > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(have, null);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        // A server that ignores the range sends the whole file again.
        if (have > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            files.Delete(part);
            have = 0;
        }
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var sink = files.OpenAppend(part);
        var buffer = new byte[1 << 16];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            // No token on the write: a FileStream write cancelled before it runs has already moved the file
            // position, so the next write leaves a gap of zeros that the resumed .part keeps.
            await sink.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
            have += read;
            report(have);
        }
        if (size >= 0 && have < size)
            throw new IOException($"The download of {url} ended at {have} of {size} bytes.");
    }

    private bool Verified(string path, long size, Func<Stream, long, bool> check)
    {
        long length = files.Length(path);
        if (size >= 0 && length != size)
            return false;
        using var stream = files.OpenRead(path);
        return check(stream, length);
    }

    private static bool Matches(HubFile file, Stream stream, long length) =>
        file.Sha256 != null
            ? string.Equals(Hashes.Sha256(stream), file.Sha256, StringComparison.OrdinalIgnoreCase)
            : string.Equals(Hashes.GitBlobSha1(stream, length), file.GitSha1, StringComparison.OrdinalIgnoreCase);
}
