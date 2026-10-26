using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.OpenAi;

namespace Veronica;

// Development switch: VERONICA_PROVIDER=openai talks to an OpenAI-compatible server instead of
// starting WebQwen.exe.
internal static class ModelProvider
{
    public static IProvider Create(Action<string> showStatus)
    {
        string provider = Environment.GetEnvironmentVariable("VERONICA_PROVIDER")?.Trim() ?? "";
        if (provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            var settings = new OpenAiSettings(
                Environment.GetEnvironmentVariable("VERONICA_OPENAI_URL") ?? "",
                Environment.GetEnvironmentVariable("VERONICA_OPENAI_MODEL"),
                Environment.GetEnvironmentVariable("VERONICA_OPENAI_API_KEY"));
            Log.Default.WriteLine($"[Veronica] OpenAI-compatible provider at '{LocalModel.AsciiOnly(settings.BaseUrl)}'.");
            var openAi = new OpenAiProvider(settings);
            openAi.StatusChanged += showStatus;
            return openAi;
        }
        if (provider.Length > 0 && !provider.Equals("webqwen", StringComparison.OrdinalIgnoreCase))
            Log.Default.WriteLine($"[Veronica] Unknown VERONICA_PROVIDER '{provider}', using WebQwen.");
        var webQwen = LocalModel.CreateProvider();
        webQwen.StatusChanged += showStatus;
        return webQwen;
    }
}
