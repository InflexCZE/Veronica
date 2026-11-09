using System.Text;

namespace Veronica.Setup.Core;

// Valve's KeyValues text format, as in libraryfolders.vdf and appmanifest_*.acf: quoted keys, each followed by a
// quoted value or a braced block. Keys compare case-insensitively; a repeated key keeps its last value.
public sealed class KeyValues
{
    private readonly Dictionary<string, object> _entries = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> Keys => _entries.Keys;

    public string? String(string key) => _entries.GetValueOrDefault(key) as string;

    public KeyValues? Block(string key) => _entries.GetValueOrDefault(key) as KeyValues;

    public static KeyValues Parse(string text)
    {
        int position = 0;
        return ParseBlock(text, ref position, nested: false);
    }

    private static KeyValues ParseBlock(string text, ref int position, bool nested)
    {
        var block = new KeyValues();
        while (true)
        {
            if (NextToken(text, ref position) is not (string key, bool keyQuoted))
            {
                if (nested)
                    throw new FormatException("A block is not closed.");
                return block;
            }
            if (!keyQuoted && key == "}")
            {
                if (!nested)
                    throw new FormatException("A '}' closes no block.");
                return block;
            }
            if (!keyQuoted)
                throw new FormatException("A block has no key.");
            var (value, valueQuoted) = NextToken(text, ref position) ?? throw new FormatException($"'{key}' has no value.");
            if (valueQuoted)
                block._entries[key] = value;
            else if (value == "{")
                block._entries[key] = ParseBlock(text, ref position, nested: true);
            else
                throw new FormatException($"'{key}' has no value.");
        }
    }

    private static (string Text, bool Quoted)? NextToken(string text, ref int position)
    {
        while (position < text.Length)
        {
            char c = text[position];
            if (char.IsWhiteSpace(c))
            {
                position++;
            }
            else if (c == '/' && position + 1 < text.Length && text[position + 1] == '/')
            {
                while (position < text.Length && text[position] != '\n')
                    position++;
            }
            else if (c is '{' or '}')
            {
                position++;
                return (c.ToString(), false);
            }
            else if (c == '"')
            {
                return (QuotedString(text, ref position), true);
            }
            else if (c == '[')
            {
                // A platform condition such as [$WIN32] after a value; this reader takes every entry as is.
                int end = text.IndexOf(']', position);
                position = end < 0 ? text.Length : end + 1;
            }
            else
            {
                throw new FormatException($"Unexpected '{c}' at {position}.");
            }
        }
        return null;
    }

    private static string QuotedString(string text, ref int position)
    {
        var builder = new StringBuilder();
        position++;
        while (position < text.Length)
        {
            char c = text[position++];
            if (c == '"')
                return builder.ToString();
            if (c == '\\' && position < text.Length)
            {
                char escaped = text[position++];
                builder.Append(escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    _ => escaped,
                });
            }
            else
            {
                builder.Append(c);
            }
        }
        throw new FormatException("A string is not closed.");
    }
}

// Where Steam installed an app, read from Steam's own files: no Steam client or API needed.
public static class Steam
{
    public const string ModSdkAppId = "3578990";

    public const string GameAppId = "1133870";

    // The Steam folder first, then every library libraryfolders.vdf lists.
    public static IReadOnlyList<string> Libraries(IFiles files, string steamRoot)
    {
        var libraries = new List<string> { Normalize(steamRoot) };
        string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        try
        {
            if (files.FileExists(vdf) && KeyValues.Parse(files.ReadAllText(vdf)).Block("libraryfolders") is { } folders)
                foreach (var key in folders.Keys)
                    if (folders.Block(key)?.String("path") is { Length: > 0 } path)
                        libraries.Add(Normalize(path));
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }
        return libraries.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // The app's install folder, from the first library whose appmanifest names a folder that exists. The
    // libraryfolders.vdf "apps" lists are not trusted: Steam updates them lazily.
    public static string? AppFolder(IFiles files, string steamRoot, string appId)
    {
        foreach (var library in Libraries(files, steamRoot))
        {
            try
            {
                string manifest = Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf");
                if (!files.FileExists(manifest)
                    || KeyValues.Parse(files.ReadAllText(manifest)).Block("AppState")?.String("installdir") is not { Length: > 0 } installDir)
                    continue;
                string folder = SafePath.Under(Path.Combine(library, "steamapps", "common"), SafePath.Segment(installDir));
                if (files.DirectoryExists(folder))
                    return folder;
            }
            catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException or ArgumentException)
            {
            }
        }
        return null;
    }

    public static string? RootFromRegistry()
    {
        try
        {
            string? path = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                ?? Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
            return string.IsNullOrWhiteSpace(path) ? null : Normalize(path);
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or ArgumentException)
        {
            return null;
        }
    }

    public static string? EditorExe(IFiles files, string? steamRoot) =>
        ExistingFile(files, steamRoot == null ? null : AppFolder(files, steamRoot, ModSdkAppId), "Editor", EditorLaunch.EditorExeName);

    public static string? GameExe(IFiles files, string? steamRoot) =>
        ExistingFile(files, steamRoot == null ? null : AppFolder(files, steamRoot, GameAppId), "Game2", EditorLaunch.GameExeName);

    private static string? ExistingFile(IFiles files, string? appFolder, string subfolder, string name)
    {
        if (appFolder == null)
            return null;
        string path = Path.Combine(appFolder, subfolder, name);
        return files.FileExists(path) ? path : null;
    }

    // The registry keeps forward slashes and lower case ("c:/program files (x86)/steam").
    private static string Normalize(string path) => Path.GetFullPath(path.Replace('/', '\\')).TrimEnd('\\');
}
