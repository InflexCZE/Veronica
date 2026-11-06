using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Veronica.Agent;

namespace Veronica.Setup.Core;

// Veronica.installed.json: what Veronica.exe itself installed, read by nobody else. Files maps every
// shipped skill and wiki file ("Skills/name.md") to its sha256; Models maps a model id to the pin it was
// downloaded at, so a release that pins a new revision replaces it; Dropped lists the waiting conflicts
// whose empty name.new.md stands for a file the release removed.
public sealed record InstalledRecord(
    SemVer? Version,
    IReadOnlyDictionary<string, string> Files,
    IReadOnlyDictionary<string, string> Models,
    IReadOnlyList<string> Dropped)
{
    public static InstalledRecord Empty { get; } = new(null, new Dictionary<string, string>(), new Dictionary<string, string>(), []);

    public bool IsDropped(string path) => Dropped.Contains(path, StringComparer.OrdinalIgnoreCase);

    public static InstalledRecord Parse(string text)
    {
        var root = JsonNode.Parse(text) as JsonObject ?? throw new FormatException("Veronica.installed.json is not a JSON object.");
        SemVer? version = SemVer.TryParse(root["Version"]?.GetValue<string>(), out var v) ? v : null;
        var dropped = root["Dropped"] is JsonArray array
            ? array.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToList()
            : [];
        return new InstalledRecord(version, Map(root["Files"]), Map(root["Models"]), dropped);
    }

    private static Dictionary<string, string> Map(JsonNode? node)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (node is JsonObject obj)
        {
            foreach (var (key, value) in obj)
            {
                if (value is JsonValue s && s.TryGetValue<string>(out var text))
                    map[key] = text;
            }
        }
        return map;
    }

    public string Serialize()
    {
        var root = new JsonObject
        {
            ["Version"] = Version?.ToString(),
            ["Files"] = Object(Files),
            ["Models"] = Object(Models),
            ["Dropped"] = new JsonArray(Dropped.Order(StringComparer.OrdinalIgnoreCase).Select(p => (JsonNode?)p).ToArray()),
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject Object(IReadOnlyDictionary<string, string> map)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in map.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            obj[key] = value;
        return obj;
    }

    public static InstalledRecord Load(IFiles files, InstallLayout layout) => Load(files, layout, out _);

    // A record that cannot be read counts as none: the next update rewrites it, treating every skill and
    // wiki file on disk as the user's.
    public static InstalledRecord Load(IFiles files, InstallLayout layout, out string? warning)
    {
        warning = null;
        try
        {
            return files.FileExists(layout.InstalledFile) ? Parse(files.ReadAllText(layout.InstalledFile)) : Empty;
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            warning = $"{layout.InstalledFile} could not be read ({e.Message}); treated as empty.";
            return Empty;
        }
    }

    public void Save(IFiles files, InstallLayout layout) => files.WriteAllText(layout.InstalledFile, Serialize());

    public InstalledRecord WithModel(string modelId, string? pin)
    {
        var models = new Dictionary<string, string>(Models, StringComparer.OrdinalIgnoreCase);
        if (pin == null)
            models.Remove(modelId);
        else
            models[modelId] = pin;
        return this with { Models = models };
    }
}

public static class Hashes
{
    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static string Sha256(Stream stream) => Convert.ToHexStringLower(SHA256.HashData(stream));

    // Hugging Face lists files outside Git LFS by their git blob id.
    public static string GitBlobSha1(Stream stream, long length)
    {
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha1.AppendData(System.Text.Encoding.ASCII.GetBytes($"blob {length}\0"));
        var buffer = new byte[1 << 16];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            sha1.AppendData(buffer, 0, read);
        return Convert.ToHexStringLower(sha1.GetHashAndReset());
    }
}
