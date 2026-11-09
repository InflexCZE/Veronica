using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Veronica.Agent;

// Every setting Veronica reads. A key is also the name of the environment variable that overrides it
// and of its entry in Veronica.settings.json (Docs/Tech/Distribution.md).
public static class Settings
{
    public const string FileName = "Veronica.settings.json";

    /// <summary>The chat's provider: <c>webqwen</c> (default), <c>openai</c> or <c>claude</c>.</summary>
    public const string VERONICA_PROVIDER = nameof(VERONICA_PROVIDER);

    /// <summary>The local model WebQwen runs: a model id of the WebQwen manifest.</summary>
    public const string VERONICA_MODEL = nameof(VERONICA_MODEL);

    /// <summary>OpenAI-compatible server: base URL including the API prefix, e.g. <c>http://localhost:8080/v1</c>.</summary>
    public const string VERONICA_OPENAI_URL = nameof(VERONICA_OPENAI_URL);

    /// <summary>OpenAI-compatible server: the model; unset takes the first model the server lists.</summary>
    public const string VERONICA_OPENAI_MODEL = nameof(VERONICA_OPENAI_MODEL);

    /// <summary>OpenAI-compatible server: optional bearer token. Stored DPAPI-encrypted in the settings file.</summary>
    public const string VERONICA_OPENAI_API_KEY = nameof(VERONICA_OPENAI_API_KEY);

    /// <summary>OpenAI-compatible server: optional reasoning effort for the chat template; unset leaves the template's default.</summary>
    public const string VERONICA_OPENAI_EFFORT = nameof(VERONICA_OPENAI_EFFORT);

    /// <summary>Claude provider: the model; unset is <c>sonnet</c>.</summary>
    public const string VERONICA_CLAUDE_MODEL = nameof(VERONICA_CLAUDE_MODEL);

    /// <summary>Claude provider: optional effort; unset passes none and the CLI's default applies.</summary>
    public const string VERONICA_CLAUDE_EFFORT = nameof(VERONICA_CLAUDE_EFFORT);

    /// <summary>Tool endpoint: <c>1</c> serves it with any provider, <c>0</c> or unset only when the provider needs it.</summary>
    public const string VERONICA_MCP = nameof(VERONICA_MCP);

    /// <summary>Tool endpoint: its port; unset is 47812.</summary>
    public const string VERONICA_MCP_PORT = nameof(VERONICA_MCP_PORT);

    /// <summary>Eval runs: a folder that records every provider session.</summary>
    public const string VERONICA_RUN_DIR = nameof(VERONICA_RUN_DIR);

    /// <summary>Eval runs: the port of the control channel; unset serves none.</summary>
    public const string VERONICA_CONTROL_PORT = nameof(VERONICA_CONTROL_PORT);

    /// <summary>The releases/latest API URL updates come from; unset is this repo's GitHub releases.</summary>
    public const string VERONICA_RELEASES_URL = nameof(VERONICA_RELEASES_URL);

    /// <summary>The Editor <c>Veronica.exe</c> starts on a mod: the path of <c>VRage.Editor.exe</c>, found in Steam's Mod SDK or picked by the user.</summary>
    public const string VERONICA_EDITOR_EXE = nameof(VERONICA_EDITOR_EXE);

    /// <summary>The game the Editor runs a mod with: the path of <c>SpaceEngineers2.exe</c>, found beside the Editor or picked by the user; unset starts the Editor without it.</summary>
    public const string VERONICA_GAME_EXE = nameof(VERONICA_GAME_EXE);

    public const string DefaultReleasesUrl ="https://api.github.com/repos/InflexCZE/Veronica/releases/latest";

    public static readonly IReadOnlyList<string> All =
    [
        VERONICA_PROVIDER, VERONICA_MODEL,
        VERONICA_OPENAI_URL, VERONICA_OPENAI_MODEL, VERONICA_OPENAI_API_KEY, VERONICA_OPENAI_EFFORT,
        VERONICA_CLAUDE_MODEL, VERONICA_CLAUDE_EFFORT,
        VERONICA_MCP, VERONICA_MCP_PORT,
        VERONICA_RUN_DIR, VERONICA_CONTROL_PORT,
        VERONICA_RELEASES_URL, VERONICA_EDITOR_EXE, VERONICA_GAME_EXE,
    ];

    public static bool IsSecret(string key) => key == VERONICA_OPENAI_API_KEY;
}

// The settings in effect: the file's values, then environment variables of the same names, which win.
public sealed class SettingsStore
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public SettingsStore(IReadOnlyDictionary<string, string> values) => _values = values;

    public static SettingsStore Empty { get; } = new(new Dictionary<string, string>());

    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

    // Unset and blank are the same: a blank variable cannot switch a setting back to its default otherwise.
    public string? GetTrimmed(string key) => Get(key)?.Trim() is { Length: > 0 } value ? value : null;

    public static SettingsStore Layer(SettingsFile file, Func<string, string?> environment, Func<string, string> unprotect)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in file.Values)
            values[key] = Settings.IsSecret(key) ? unprotect(value) : value;
        foreach (var key in Settings.All)
        {
            if (environment(key) is { } value)
                values[key] = value;
        }
        return new SettingsStore(values);
    }

    // The store a running Veronica reads: the settings file, if any, under the real environment. A file that
    // cannot be read is reported and skipped, so a broken file cannot keep the Editor's panel from opening.
    public static SettingsStore Load(string? settingsPath, Action<string> warn) =>
        From(() => settingsPath != null && File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null,
            settingsPath ?? "", Environment.GetEnvironmentVariable, Secrets.Unprotect, warn);

    public static SettingsStore From(Func<string?> readFile, string fileName, Func<string, string?> environment, Func<string, string> unprotect, Action<string> warn)
    {
        var file = SettingsFile.Empty;
        try
        {
            if (readFile() is { } text)
                file = SettingsFile.Parse(text);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException or InvalidOperationException)
        {
            warn($"{fileName} could not be read ({e.Message}); using environment variables only.");
        }
        return Layer(file, environment, unprotect);
    }
}

// Veronica.settings.json: one flat JSON object of string values; secrets are stored protected.
public sealed class SettingsFile
{
    public static SettingsFile Empty { get; } = new(new Dictionary<string, string>());

    public SettingsFile(IReadOnlyDictionary<string, string> values) => Values = values;

    public IReadOnlyDictionary<string, string> Values { get; }

    public static SettingsFile Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (JsonNode.Parse(text) is JsonObject root)
        {
            foreach (var (key, node) in root)
            {
                if (node is JsonValue value && value.TryGetValue<string>(out var s))
                    values[key] = s;
            }
        }
        return new SettingsFile(values);
    }

    public string Serialize()
    {
        var root = new JsonObject();
        foreach (var (key, value) in Values.OrderBy(p => p.Key, StringComparer.Ordinal))
            root[key] = value;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    // Plain values in, the file's form out: secrets protected, a blank value removes its key, other keys stay.
    public SettingsFile With(IReadOnlyDictionary<string, string?> plain, Func<string, string> protect)
    {
        var values = new Dictionary<string, string>(Values, StringComparer.Ordinal);
        foreach (var (key, value) in plain)
        {
            if (string.IsNullOrWhiteSpace(value))
                values.Remove(key);
            else
                values[key] = Settings.IsSecret(key) ? protect(value.Trim()) : value.Trim();
        }
        return new SettingsFile(values);
    }
}

// Windows DPAPI for the current user: a copied settings file does not carry the secret in the clear.
public static class Secrets
{
    private static readonly byte[] Entropy = Encoding.ASCII.GetBytes("Veronica.settings");

    public static string Protect(string plain)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Secrets are protected with Windows DPAPI.");
        return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser));
    }

    // A value this user cannot decrypt (another user's or machine's file) reads as unset.
    public static string Unprotect(string stored)
    {
        if (!OperatingSystem.IsWindows())
            return "";
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            return "";
        }
    }
}
