using System.Globalization;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.Claude;
using Veronica.Agent.OpenAi;
using Veronica.Agent.Tools;

namespace Veronica;

// Development switch: VERONICA_PROVIDER=openai talks to an OpenAI-compatible server, =claude runs
// Claude Code, instead of starting WebQwen.exe.
internal static class ModelProvider
{
    private static string Provider => Environment.GetEnvironmentVariable("VERONICA_PROVIDER")?.Trim() ?? "";

    private static bool IsOpenAi => Provider.Equals("openai", StringComparison.OrdinalIgnoreCase);

    private static bool IsClaude => Provider.Equals("claude", StringComparison.OrdinalIgnoreCase);

    public static IProvider Create(Action<string> showStatus, ToolSet tools)
    {
        if (IsOpenAi)
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
        if (IsClaude)
        {
            var settings = ReadClaudeSettings();
            Log.Default.WriteLine($"[Veronica] Claude Code provider, model '{LocalModel.AsciiOnly(settings.Model)}', tools on port {settings.McpPort}.");
            var claude = new ClaudeProvider(settings, tools);
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
        string port = Environment.GetEnvironmentVariable("VERONICA_MCP_PORT")?.Trim() ?? "";
        int mcpPort = ClaudeSettings.DefaultMcpPort;
        if (port.Length > 0 && !(int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out mcpPort) && mcpPort is >= 1 and <= 65535))
        {
            Log.Default.WriteLine($"[Veronica] VERONICA_MCP_PORT '{LocalModel.AsciiOnly(port)}' is not a port, using {ClaudeSettings.DefaultMcpPort}.");
            mcpPort = ClaudeSettings.DefaultMcpPort;
        }
        return new ClaudeSettings(
            string.IsNullOrEmpty(model) ? ClaudeSettings.DefaultModel : model,
            Environment.GetEnvironmentVariable("VERONICA_CLAUDE_EFFORT"),
            mcpPort,
            ClaudeSettings.DefaultWorkingDirectory);
    }
}
