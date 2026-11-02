using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.Claude;
using Veronica.Agent.Mcp;
using Veronica.Agent.OpenAi;

namespace Veronica;

// Development switch: VERONICA_PROVIDER=openai talks to an OpenAI-compatible server, =claude runs
// Claude Code, instead of starting WebQwen.exe.
internal static class ModelProvider
{
    private static string Provider => Environment.GetEnvironmentVariable("VERONICA_PROVIDER")?.Trim() ?? "";

    private static bool IsOpenAi => Provider.Equals("openai", StringComparison.OrdinalIgnoreCase);

    private static bool IsClaude => Provider.Equals("claude", StringComparison.OrdinalIgnoreCase);

    public static string Describe() =>
        IsOpenAi ? $"openai {Environment.GetEnvironmentVariable("VERONICA_OPENAI_URL")} model={Environment.GetEnvironmentVariable("VERONICA_OPENAI_MODEL") ?? "(first listed)"} effort={Environment.GetEnvironmentVariable("VERONICA_OPENAI_EFFORT") ?? "(template default)"}"
        : IsClaude ? $"claude model={ReadClaudeSettings().Model}"
        : "webqwen";

    public static bool NeedsEndpoint => IsClaude;

    public static IProvider Create(Action<string> showStatus, McpEndpoint endpoint)
    {
        if (IsOpenAi)
        {
            var settings = new OpenAiSettings(
                Environment.GetEnvironmentVariable("VERONICA_OPENAI_URL") ?? "",
                Environment.GetEnvironmentVariable("VERONICA_OPENAI_MODEL"),
                Environment.GetEnvironmentVariable("VERONICA_OPENAI_API_KEY"),
                Environment.GetEnvironmentVariable("VERONICA_OPENAI_EFFORT"));
            Log.Default.WriteLine($"[Veronica] OpenAI-compatible provider at '{LocalModel.AsciiOnly(settings.BaseUrl)}'.");
            var openAi = new OpenAiProvider(settings);
            openAi.StatusChanged += showStatus;
            return openAi;
        }
        if (IsClaude)
        {
            var settings = ReadClaudeSettings();
            Log.Default.WriteLine($"[Veronica] Claude Code provider, model '{LocalModel.AsciiOnly(settings.Model)}'.");
            var claude = new ClaudeProvider(settings, endpoint);
            claude.StatusChanged += showStatus;
            return claude;
        }
        if (Provider.Length > 0 && !Provider.Equals("webqwen", StringComparison.OrdinalIgnoreCase))
            Log.Default.WriteLine($"[Veronica] Unknown VERONICA_PROVIDER '{LocalModel.AsciiOnly(Provider)}', using WebQwen.");
        var webQwen = LocalModel.CreateProvider();
        webQwen.StatusChanged += showStatus;
        return webQwen;
    }

    private static ClaudeSettings ReadClaudeSettings()
    {
        string? model = Environment.GetEnvironmentVariable("VERONICA_CLAUDE_MODEL")?.Trim();
        return new ClaudeSettings(
            string.IsNullOrEmpty(model) ? ClaudeSettings.DefaultModel : model,
            Environment.GetEnvironmentVariable("VERONICA_CLAUDE_EFFORT"),
            ClaudeSettings.DefaultWorkingDirectory);
    }
}
