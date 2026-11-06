using System.Globalization;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.Mcp;
using Veronica.Agent.Tools;

namespace Veronica;

// VERONICA_MCP=1 serves the tools from panel creation with any provider; a provider that needs the
// endpoint starts it then too. VERONICA_MCP_PORT picks the port.
internal static class ToolEndpoint
{
    public static McpEndpoint Create(ToolSet tools) => new(ReadPort(), tools);

    public static void Start(McpEndpoint endpoint, bool providerNeedsIt, Action<string> showStatus)
    {
        string flag = Install.Settings.Get(Settings.VERONICA_MCP)?.Trim() ?? "";
        if (flag.Length > 0 && flag != "0" && flag != "1")
            Log.Default.WriteLine($"[Veronica] VERONICA_MCP '{LocalModel.AsciiOnly(flag)}' is not 1 or 0, treated as 0.");
        if (flag != "1" && !providerNeedsIt)
            return;
        try
        {
            Log.Default.WriteLine($"[Veronica] Tool endpoint at {endpoint.Start()}.");
        }
        catch (McpEndpointException e)
        {
            Log.Default.WriteLine($"[Veronica] {LocalModel.AsciiOnly(e.Message)}");
            showStatus(e.Message);
        }
    }

    private static int ReadPort()
    {
        string port = Install.Settings.Get(Settings.VERONICA_MCP_PORT)?.Trim() ?? "";
        if (port.Length == 0)
            return McpEndpoint.DefaultPort;
        if (int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value is >= 1 and <= 65535)
            return value;
        Log.Default.WriteLine($"[Veronica] VERONICA_MCP_PORT '{LocalModel.AsciiOnly(port)}' is not a port, using {McpEndpoint.DefaultPort}.");
        return McpEndpoint.DefaultPort;
    }
}
