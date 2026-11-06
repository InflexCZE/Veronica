using Veronica.Agent;

namespace Veronica.Setup.Core;

// The install folder (Docs/Tech/Distribution.md): everything sits beside Veronica.exe.
public sealed record InstallLayout(string Root)
{
    public string Runtime => Path.Combine(Root, "Runtime");

    public string RuntimeNew => Path.Combine(Root, "Runtime.new");

    public string RuntimeOld => Path.Combine(Root, "Runtime.old");

    public string Skills => Path.Combine(Root, "Skills");

    public string Wiki => Path.Combine(Root, "Wiki");

    public string Models => Path.Combine(Root, "Models");

    public string Downloads => Path.Combine(Root, "Downloads");

    public string SettingsFile => Path.Combine(Root, Settings.FileName);

    public string InstalledFile => Path.Combine(Root, "Veronica.installed.json");

    public string LogFile => Path.Combine(Root, "Veronica.log");

    public string Exe => Path.Combine(Root, Release.ExeAsset);

    public string Manifest => Path.Combine(Runtime, "manifest.json");

    public string PluginDll => Path.Combine(Runtime, "Veronica.dll");

    public string ModelFolder(string modelId) => Path.Combine(Models, modelId);

    // The content folders an update merges, as the prefixes of their entries in Veronica.zip.
    public static readonly IReadOnlyList<string> ContentFolders = ["Skills", "Wiki"];

    private static readonly HashSet<string> OwnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Runtime", "Runtime.new", "Runtime.old", "Skills", "Wiki", "Models", "Downloads", Settings.FileName, "Veronica.installed.json", "Veronica.log",
    };

    // Names in the root that are not Veronica's: the user dropped Veronica.exe into a busy folder.
    public static IReadOnlyList<string> ForeignEntries(IEnumerable<string> names, string runningExeName) =>
        names.Where(n => !OwnNames.Contains(n)
                && !string.Equals(n, runningExeName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(n, runningExeName + SelfUpdate.OldSuffix, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(n, Release.ExeAsset, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
