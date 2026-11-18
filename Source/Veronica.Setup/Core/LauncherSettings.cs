using System.Text.Json;
using System.Text.Json.Nodes;

namespace Veronica.Setup.Core;

// Veronica.launcher.json: what only the Launcher reads, so unlike Veronica.settings.json no environment variable
// overrides it. Keys this version does not know are kept, so an older Launcher does not drop a newer one's.
public sealed class LauncherSettings
{
    public const string FileName = "Veronica.launcher.json";

    private const string ProjectsKey = "Projects";

    private readonly JsonObject _root;

    private LauncherSettings(JsonObject root) => _root = root;

    public static LauncherSettings Empty => new(new JsonObject());

    public static LauncherSettings Parse(string text) =>
        new(JsonNode.Parse(text) as JsonObject ?? throw new FormatException($"{FileName} is not a JSON object."));

    // The .vrgproj files the modder added by hand, beside the mod HUB's index.
    public IReadOnlyList<string> Projects =>
        _root[ProjectsKey] is JsonArray array
            ? array.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>().Where(s => s.Length > 0).ToList()
            : [];

    public LauncherSettings WithProjects(IEnumerable<string> projects)
    {
        var root = (JsonObject)_root.DeepClone();
        root[ProjectsKey] = new JsonArray(projects.Select(p => (JsonNode?)JsonValue.Create(p)).ToArray());
        return new LauncherSettings(root);
    }

    public string Serialize() => _root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
