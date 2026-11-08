using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Veronica.Agent;
using Veronica.Setup.Core;

namespace Veronica.Setup.Ui;

// Everything the window reaches outside itself: the install folder, the network, other processes.
public sealed class SetupHost
{
    public required InstallLayout Layout { get; init; }

    public required IFiles Files { get; init; }

    public required HttpClient Http { get; init; }

    // The provider check trusts what the plugin's OpenAI provider trusts, so a server it can use passes.
    public HttpClient OpenAiHttp { get; init; } = new(Agent.OpenAi.OpenAiProvider.AnyCertificateHandler());

    public required IEditorProbe Editor { get; init; }

    public required string RunningExe { get; init; }

    public SemVer? Version { get; init; }

    public static SetupHost ForThisProcess()
    {
        string exe = Environment.ProcessPath!;
        var layout = new InstallLayout(Path.GetDirectoryName(exe)!);
        var version = SemVer.TryParse(typeof(SetupHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, out var v) ? v : (SemVer?)null;
        return new SetupHost
        {
            Layout = layout,
            Files = new DiskFiles(),
            Http = ReleaseFeed.CreateClient(null, version),
            Editor = new EditorProbe(layout),
            RunningExe = exe,
            Version = version,
        };
    }

    // A broken settings file reads as empty; saving then replaces it.
    public SettingsFile SettingsFile
    {
        get
        {
            try
            {
                return Files.FileExists(Layout.SettingsFile) ? SettingsFile.Parse(Files.ReadAllText(Layout.SettingsFile)) : SettingsFile.Empty;
            }
            catch (Exception e) when (e is System.Text.Json.JsonException or FormatException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                Log($"{Layout.SettingsFile} could not be read ({e.Message}); treated as empty.");
                return SettingsFile.Empty;
            }
        }
    }

    public SettingsStore Settings => SettingsStore.Layer(SettingsFile, Environment.GetEnvironmentVariable, Secrets.Unprotect);

    public void SaveSettings(IReadOnlyDictionary<string, string?> changes) =>
        Files.WriteAllText(Layout.SettingsFile, SettingsFile.With(changes, Secrets.Protect).Serialize());

    public string ReleasesUrl => Settings.GetTrimmed(Agent.Settings.VERONICA_RELEASES_URL) ?? Agent.Settings.DefaultReleasesUrl;

    public InstalledRecord Record
    {
        get
        {
            var record = InstalledRecord.Load(Files, Layout, out var warning);
            if (warning != null)
            {
                RecordWarning = warning;
                Log(warning);
            }
            return record;
        }
    }

    public string? RecordWarning { get; private set; }

    public IReadOnlyList<ModelRecord> Manifest => Files.FileExists(Layout.Manifest) ? ModelCatalog.Parse(Files.ReadAllText(Layout.Manifest)) : [];

    public void Log(string line)
    {
        try
        {
            File.AppendAllText(Layout.LogFile, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} {Ascii(line)}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void Open(params string[] paths)
    {
        foreach (var path in paths)
            Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { path }, UseShellExecute = false });
    }

    // A single-file exe loads its assemblies lazily from its own file, so once it is renamed it must not
    // load anything new: the restart is prepared before the rename and the process exits right after.
    public Action PrepareRestart(string exe, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        return () =>
        {
            Process.Start(info)?.Dispose();
            Environment.Exit(0);
        };
    }

    private static string Ascii(string text) => new(text.Select(c => c < 128 ? c : '?').ToArray());
}
