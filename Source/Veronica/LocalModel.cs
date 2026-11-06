using System.Text;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.WebQwen;
using Veronica.Residency;

namespace Veronica;

// WebQwen.exe and the active model: from the install folder, or in development from this repo's
// build output and .deps folder.
internal static class LocalModel
{
    private const string DevelopmentModelId = "Ternary-Bonsai-2-27B-MLC";

    // Null when an installed Veronica has no model chosen; the message says what to do.
    public static WebQwenProvider? CreateProvider(out string? unavailable)
    {
        string? chosen = Install.Settings.GetTrimmed(Settings.VERONICA_MODEL);
        unavailable = null;
        if (chosen == null && Install.IsInstalled)
        {
            unavailable = "No local model is chosen. Choose one in Veronica.exe, then restart the Editor.";
            return null;
        }
        Install.CopyShaderCompiler();
        string modelId = chosen ?? DevelopmentModelId;
        var launch = new WebQwenLaunch(Install.WebQwenExe, modelId, Install.ModelsDir);
        var gpu = new GpuShare(EditorResidency.FreeAsync, EditorResidency.RestoreAsync, line => Log.Default.WriteLine($"[Veronica] {line}"));
        var provider = new WebQwenProvider(launch, line => Log.Default.WriteLine($"[WebQwen] {AsciiOnly(line)}"), gpu);
        EditorResidency.DropUnusedTextures();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => provider.Dispose();
        return provider;
    }

    // The Editor's Console shows the log in a font whose glyph fallback crashes the Editor
    // on characters it lacks (Docs/TODOs/EditorTextOutside.md).
    public static string AsciiOnly(string text)
    {
        if (text.All(char.IsAscii))
            return text;
        var ascii = new StringBuilder(text.Length + 16);
        foreach (char c in text)
        {
            if (char.IsAscii(c))
                ascii.Append(c);
            else
                ascii.Append("\\u").Append(((int)c).ToString("X4"));
        }
        return ascii.ToString();
    }
}
