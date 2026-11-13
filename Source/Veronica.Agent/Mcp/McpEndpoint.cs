using System.Net.Sockets;
using Veronica.Agent.Tools;

namespace Veronica.Agent.Mcp;

public sealed class McpEndpointException(string message) : Exception(message);

// Veronica's tools for any local MCP client. A call runs directly, unless a provider routes calls
// into the turn it is running. With client sessions, each session's calls work on its own state.
public sealed class McpEndpoint(int port, ToolSet tools, McpClientSessions? sessions = null) : IDisposable
{
    public const int DefaultPort = 47812;

    private readonly object _gate = new();
    private McpServer? _server;
    private Func<string, string, CancellationToken, Task<ToolResult?>>? _route;
    private int _calls;

    // A failed start is tried again by the next call.
    public string Start()
    {
        lock (_gate)
        {
            try
            {
                _server ??= new McpServer(port, tools.Definitions, CallAsync, sessions);
            }
            catch (SocketException e)
            {
                throw new McpEndpointException($"Veronica's tool endpoint cannot listen on 127.0.0.1:{port} ({e.Message}). Set VERONICA_MCP_PORT to a free port.");
            }
            return _server.Url;
        }
    }

    // route runs one tool by name with its arguments as JSON text; null means it did not run.
    public IDisposable Route(Func<string, string, CancellationToken, Task<ToolResult?>> route)
    {
        lock (_gate)
        {
            if (_route != null)
                throw new InvalidOperationException("The endpoint's calls are already routed.");
            _route = route;
        }
        return new Routing(this, route);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _server?.Dispose();
            _server = null;
        }
    }

    public static int PortFrom(string? setting, out string? problem)
    {
        problem = null;
        string port = setting?.Trim() ?? "";
        if (port.Length == 0)
            return DefaultPort;
        if (int.TryParse(port, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int value) && value is >= 1 and <= 65535)
            return value;
        problem = $"'{port}' is not a port, using {DefaultPort}.";
        return DefaultPort;
    }

    private async Task<ToolResult?> CallAsync(string? session, string name, string arguments, CancellationToken stop)
    {
        Func<string, string, CancellationToken, Task<ToolResult?>>? route;
        lock (_gate)
            route = _route;
        if (route != null)
            return await route(name, arguments, stop).ConfigureAwait(false);
        var call = new ToolCall($"mcp-{Interlocked.Increment(ref _calls)}", name, arguments);
        if (sessions != null)
            return await sessions.RunAsync<ToolResult?>(session, async () => await tools.ExecuteAsync(call, stop).ConfigureAwait(false)).ConfigureAwait(false);
        return await tools.ExecuteAsync(call, stop).ConfigureAwait(false);
    }

    private sealed class Routing(McpEndpoint endpoint, Func<string, string, CancellationToken, Task<ToolResult?>> route) : IDisposable
    {
        public void Dispose()
        {
            lock (endpoint._gate)
            {
                if (endpoint._route == route)
                    endpoint._route = null;
            }
        }
    }
}
