using System.Globalization;
using System.Text.Json;

namespace Veronica.Agent;

public readonly record struct SemVer(int Major, int Minor, int Patch) : IComparable<SemVer>
{
    public int CompareTo(SemVer other) =>
        Major != other.Major ? Major.CompareTo(other.Major)
        : Minor != other.Minor ? Minor.CompareTo(other.Minor)
        : Patch.CompareTo(other.Patch);

    public static bool operator >(SemVer a, SemVer b) => a.CompareTo(b) > 0;

    public static bool operator <(SemVer a, SemVer b) => a.CompareTo(b) < 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    // "v1.2.3", "1.2.3" and an assembly's "1.2.3.0" or "1.2.3+commit" all parse; anything else does not.
    public static bool TryParse(string? text, out SemVer version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
            s = s[1..];
        int cut = s.IndexOfAny(['+', '-']);
        if (cut >= 0)
            s = s[..cut];
        var parts = s.Split('.');
        if (parts.Length is < 3 or > 4)
            return false;
        var numbers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return false;
        }
        if (parts.Length == 4 && numbers[3] != 0)
            return false;
        version = new SemVer(numbers[0], numbers[1], numbers[2]);
        return true;
    }
}

public sealed record ReleaseAsset(string Name, string Url, long Size);

public sealed record Release(SemVer Version, string Tag, IReadOnlyList<ReleaseAsset> Assets)
{
    public ReleaseAsset? Asset(string name) => Assets.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    public const string ZipAsset = "Veronica.zip";

    public const string ExeAsset = "Veronica.exe";

    // The releases/latest answer of GitHub and of Gitea, which share these fields.
    public static Release Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string tag = root.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "";
        if (!SemVer.TryParse(tag, out var version))
            throw new FormatException($"The latest release's tag '{tag}' is not a version vX.Y.Z.");
        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in list.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    && asset.TryGetProperty("browser_download_url", out var url) && url.ValueKind == JsonValueKind.String)
                {
                    long size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out long value) ? value : -1;
                    assets.Add(new ReleaseAsset(name.GetString()!, url.GetString()!, size));
                }
            }
        }
        return new Release(version, tag, assets);
    }
}
