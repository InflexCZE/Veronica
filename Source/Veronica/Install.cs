using System.Diagnostics;
using System.Reflection;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;

namespace Veronica;

// Where this Veronica.dll finds its files: an install folder around it (Docs/Tech/Distribution.md),
// or the paths a development build writes into it.
internal static class Install
{
    public const string RuntimeFolder = "Runtime";

    private static readonly string AssemblyDir = Path.GetDirectoryName(typeof(Install).Assembly.Location)!;

    public static bool IsInstalled { get; } =
        string.Equals(Path.GetFileName(AssemblyDir), RuntimeFolder, StringComparison.OrdinalIgnoreCase)
        && File.Exists(Path.Combine(AssemblyDir, "WebQwen.exe"));

    public static string? Root => IsInstalled ? Path.GetDirectoryName(AssemblyDir) : null;

    public static string WebQwenExe => IsInstalled ? Path.Combine(AssemblyDir, "WebQwen.exe") : Metadata("WebQwenExe")!;

    public static string? SkillsDir => IsInstalled ? Path.Combine(Root!, "Skills") : Metadata("SkillsDir");

    public static string? WikiDir => IsInstalled ? Path.Combine(Root!, "Wiki") : Metadata("WikiDir");

    public static string? ModelsDir => IsInstalled ? Path.Combine(Root!, "Models") : null;

    public static string? SetupExe => IsInstalled ? Path.Combine(Root!, Release.ExeAsset) : null;

    // Read once, when the panel is created; a change takes effect at the next Editor start.
    public static SettingsStore Settings => _settings ??= SettingsStore.Load(
        IsInstalled ? Path.Combine(Root!, Agent.Settings.FileName) : null,
        warning => Log.Default.WriteLine($"[Veronica] {LocalModel.AsciiOnly(warning)}"));

    private static SettingsStore? _settings;

    public static SemVer? Version =>
        SemVer.TryParse(typeof(Install).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, out var v) ? v : null;

    private static string? Metadata(string key) =>
        typeof(Install).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(a => a.Key == key)?.Value;

    // The release leaves out the DirectX Shader Compiler that Dawn needs; the Editor ships one.
    public static void CopyShaderCompiler()
    {
        if (!IsInstalled)
            return;
        foreach (var name in new[] { "dxcompiler.dll", "dxil.dll" })
        {
            try
            {
                string? source = new[] { Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", name), Path.Combine(AppContext.BaseDirectory, name) }
                    .FirstOrDefault(File.Exists);
                if (source == null)
                {
                    Log.Default.WriteLine($"[Veronica] {name} not found in the Editor folder, WebQwen may not start.");
                    continue;
                }
                string target = Path.Combine(AssemblyDir, name);
                if (File.Exists(target) && !IsOlder(target, source))
                    continue;
                File.Copy(source, target, overwrite: true);
                Log.Default.WriteLine($"[Veronica] Copied {name} from the Editor.");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Default.WriteLine($"[Veronica] Copying {name} failed: {LocalModel.AsciiOnly(e.Message)}");
            }
        }
    }

    private static bool IsOlder(string target, string source)
    {
        var t = FileVersionInfo.GetVersionInfo(target);
        var s = FileVersionInfo.GetVersionInfo(source);
        return new Version(t.FileMajorPart, t.FileMinorPart, t.FileBuildPart, t.FilePrivatePart)
            < new Version(s.FileMajorPart, s.FileMinorPart, s.FileBuildPart, s.FilePrivatePart);
    }
}
