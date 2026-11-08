using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Veronica.Setup.Core;

public sealed record ModIndexEntry(string ProjectId, string Folder);

// The HUB's list of local mods: project GUID to folder.
public static class ModIndex
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceEngineers2", "Temp", "LocalMods", "ModIndex");

    public static IReadOnlyList<ModIndexEntry> Parse(string text)
    {
        var entries = new List<ModIndexEntry>();
        if (JsonNode.Parse(text)?["$Value"]?["Projects"] is JsonObject projects)
        {
            foreach (var (id, folder) in projects)
            {
                if (folder is JsonValue value && value.TryGetValue<string>(out var path) && path.Length > 0)
                    entries.Add(new ModIndexEntry(id, path));
            }
        }
        return entries;
    }
}

public enum HookState
{
    NotHooked,
    Hooked,
    // Veronica.dll is loaded from a folder that is not this install's Runtime (a development build, another
    // or a moved install); Veronica.exe only removes it.
    Foreign,
}

// A mod's .vrgproj, as far as Veronica touches it: Veronica.dll in EditorExtensionAssemblies and this install's
// absolute Runtime path in EditorExtensionAssembliesSearchPaths. Any other entry is the mod's own.
public static class VrgProj
{
    public const string Assembly = "Veronica.dll";

    private const string Assemblies = "EditorExtensionAssemblies";
    private const string SearchPaths = "EditorExtensionAssembliesSearchPaths";

    public static string? Name(string text) => Value(text)["Name"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public static HookState State(string text, string runtime)
    {
        var data = Value(text);
        if (!List(data, Assemblies).Any(IsVeronica))
            return HookState.NotHooked;
        return List(data, SearchPaths).Any(p => SamePath(p, runtime)) ? HookState.Hooked : HookState.Foreign;
    }

    public static string Hook(string text, string runtime) => Edit(text, data =>
    {
        var assemblies = Array(data, Assemblies);
        if (!List(data, Assemblies).Any(IsVeronica))
            assemblies.Add(Assembly);
        if (!List(data, SearchPaths).Any(p => SamePath(p, runtime)))
            Array(data, SearchPaths).Add(runtime);
    });

    public static string Unhook(string text, string runtime) => Edit(text, data =>
    {
        RemoveWhere(Array(data, Assemblies), IsVeronica);
        RemoveWhere(Array(data, SearchPaths), p => SamePath(p, runtime));
    });

    // Another build's or install's entries: Veronica.dll, and the search paths whose folder holds a Veronica.dll
    // (holdsVeronica gets the path as written, relative paths included). The mod's other paths stay.
    public static string UnhookAny(string text, string runtime, Func<string, bool> holdsVeronica) => Edit(text, data =>
    {
        RemoveWhere(Array(data, Assemblies), IsVeronica);
        RemoveWhere(Array(data, SearchPaths), p => SamePath(p, runtime) || holdsVeronica(p));
    });

    private static bool IsVeronica(string name) => string.Equals(name.Trim(), Assembly, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b) =>
        string.Equals(a.Trim().TrimEnd('\\', '/'), b.Trim().TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private static JsonObject Value(string text) =>
        JsonNode.Parse(text)?["$Value"] as JsonObject ?? throw new FormatException("The .vrgproj has no $Value object.");

    private static IEnumerable<string> List(JsonObject data, string name) =>
        data[name] is JsonArray array
            ? array.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>()
            : [];

    private static JsonArray Array(JsonObject data, string name)
    {
        if (data[name] is JsonArray array)
            return array;
        var created = new JsonArray();
        data[name] = created;
        return created;
    }

    private static void RemoveWhere(JsonArray array, Func<string, bool> match)
    {
        for (int i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is JsonValue v && v.TryGetValue<string>(out var s) && match(s))
                array.RemoveAt(i);
        }
    }

    // Written back the way the Editor writes it: two-space indent, the file's own line ends.
    private static string Edit(string text, Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(text) ?? throw new FormatException("The .vrgproj is empty.");
        edit(root["$Value"] as JsonObject ?? throw new FormatException("The .vrgproj has no $Value object."));
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NewLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n",
        };
        string json = root.ToJsonString(options);
        return text.EndsWith('\n') ? json + options.NewLine : json;
    }
}
