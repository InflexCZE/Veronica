using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace WebQwen;

// Model records of a WebQwen manifest.
public sealed class Manifest
{
    private readonly JsonElement _root;

    private Manifest(JsonElement root) => _root = root;

    public static Manifest Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        return new Manifest(doc.RootElement.Clone());
    }

    public IEnumerable<string> ModelIds => _root.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("model_id").GetString()!);

    public ModelSpec Model(string modelId, int contextWindowSize = 0)
    {
        foreach (var m in _root.GetProperty("models").EnumerateArray())
        {
            if (m.GetProperty("model_id").GetString() != modelId) continue;
            var lib = m.GetProperty("lib");
            return new ModelSpec(
                modelId,
                m.GetProperty("url").GetString()!,
                lib.GetProperty("url").GetString()!,
                lib.GetProperty("sha256").GetString()!,
                m.TryGetProperty("required_features", out var f) ? f.EnumerateArray().Select(x => x.GetString()!).ToArray() : [],
                contextWindowSize);
        }
        throw new KeyNotFoundException($"the manifest has no model {modelId}");
    }
}

// Serves a model's URLs from a local folder: the files under its model URL,
// and its library as webgpu.wasm. Nothing outside the folder is reachable.
public sealed class ModelFiles : IResourceProvider
{
    private readonly string _modelUrl;
    private readonly string _libUrl;
    private readonly string _directory;

    public ModelFiles(ModelSpec model, string directory)
    {
        _modelUrl = model.ModelUrl.EndsWith('/') ? model.ModelUrl : model.ModelUrl + "/";
        _libUrl = model.LibUrl;
        _directory = Path.GetFullPath(directory);
    }

    private string? PathOf(string url)
    {
        if (url == _libUrl) return Path.Combine(_directory, "webgpu.wasm");
        if (!url.StartsWith(_modelUrl, StringComparison.Ordinal)) return null;
        string relative = url[_modelUrl.Length..];
        int query = relative.IndexOfAny(['?', '#']);
        if (query >= 0) relative = relative[..query];
        string path = Path.GetFullPath(Path.Combine(_directory, Uri.UnescapeDataString(relative)));
        return path.StartsWith(_directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    public long Size(string url)
    {
        string? path = PathOf(url);
        return path != null && File.Exists(path) ? new FileInfo(path).Length : -1;
    }

    public long Read(string url, long offset, Span<byte> destination)
    {
        string? path = PathOf(url);
        if (path == null) return -1;
        using SafeFileHandle handle = File.OpenHandle(path);
        long total = 0;
        while (total < destination.Length)
        {
            int read = RandomAccess.Read(handle, destination[(int)total..], offset + total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
