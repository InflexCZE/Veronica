using System.Reflection;
using System.Text;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.WebQwen;
using Veronica.Residency;

namespace Veronica;

// Development wiring: WebQwen.exe from this repo's build output, the model from its .deps folder.
internal static class LocalModel
{
    private const string ModelId = "Ternary-Bonsai-2-27B-MLC";

    public static WebQwenProvider CreateProvider()
    {
        var exe = typeof(LocalModel).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "WebQwenExe").Value!;
        var gpu = new GpuShare(EditorResidency.FreeAsync, EditorResidency.RestoreAsync, line => Log.Default.WriteLine($"[Veronica] {line}"));
        var provider = new WebQwenProvider(new WebQwenLaunch(exe, ModelId), line => Log.Default.WriteLine($"[WebQwen] {AsciiOnly(line)}"), gpu);
        EditorResidency.DropUnusedTextures();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => provider.Dispose();
        return provider;
    }

    // The Editor's Console shows the log in a font whose glyph fallback crashes the Editor
    // on characters it lacks (Docs/TODOs/EditorTextOutside.md).
    private static string AsciiOnly(string text)
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
