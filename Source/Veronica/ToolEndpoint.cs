using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.Mcp;
using Veronica.Agent.Tools;

namespace Veronica;

// VERONICA_MCP=1 serves the tools from panel creation with any provider; a provider that needs the
// endpoint starts it then too. VERONICA_MCP_PORT picks the port.
internal static class ToolEndpoint
{
    public static McpEndpoint Create(ToolSet tools, McpClientSessions? sessions = null) => new(Port, tools, sessions);

    private static readonly Lazy<int> ReadOnce = new(ReadPort);

    public static int Port => ReadOnce.Value;

    public static void Start(McpEndpoint endpoint, bool providerNeedsIt, Action<ProviderStatus> showStatus)
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
            showStatus(new ProviderStatus("Veronica's tools could not start", e.Message));
        }
    }

    private static int ReadPort()
    {
        int port = McpEndpoint.PortFrom(Install.Settings.Get(Settings.VERONICA_MCP_PORT), out string? problem);
        if (problem != null)
            Log.Default.WriteLine($"[Veronica] VERONICA_MCP_PORT {LocalModel.AsciiOnly(problem)}");
        return port;
    }
}
