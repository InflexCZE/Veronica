using System.Text.Json;
using Veronica.Agent.Tools;

namespace Veronica.Agent.Mcp;

// What the user hands their own agent to connect it to Veronica's tools, in the Editor and the Launcher.
public static class McpConnection
{
    public static string Prompt(int port) =>
        $"Please help me configure new MCP server \"{McpServer.ServerName}\" (Streamable HTTP, {McpServer.UrlFor(port)}).\n" +
        "Tell me what configuration options we have so it gets installed as I need it.";

    // Clients cut long server instructions short, so the system prompt comes from a tool instead.
    public const string Hint =
        "Veronica edits the Space Engineers 2 mod open in the Editor. Before you use any other Veronica tool, call " + ReadInstructionsTool.ToolName + " once and follow what it returns.";

    public const string CheckInstructions =
        "This is Veronica's setup check, and the connection works. Tell the user so. Veronica's tools appear when the Space Engineers 2 Editor runs with Veronica.";
}

// Bare MCP: the system prompt every other provider gets, for a client whose own prompt Veronica does not write.
public sealed class ReadInstructionsTool(Func<string> prompt) : ITool
{
    public const string ToolName = "read_instructions";

    public string Name => ToolName;

    public string Description => "Returns how Veronica works and its skills. Call it once before using any other Veronica tool.";

    public string Parameters => """{"type":"object","properties":{}}""";

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(new ToolResult(prompt()) { Summary = "Read Veronica's instructions" });

    public string Attempt(JsonElement arguments) => "read the instructions";
}

// The Launcher's check: a server with no tools that passes once a client starts a session.
public sealed class McpConnectionCheck : IMcpSessions, IDisposable
{
    private readonly TaskCompletionSource _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly McpServer _server;

    // Throws SocketException when the port is taken.
    public McpConnectionCheck(int port) =>
        _server = new McpServer(port, [], (_, _, _, _) => Task.FromResult<ToolResult?>(null), this);

    public Task Connected => _connected.Task;

    public McpSessionStart Open()
    {
        _connected.TrySetResult();
        return new McpSessionStart(Guid.NewGuid().ToString("N"), McpConnection.CheckInstructions);
    }

    // Any id is taken: a 404 would only send the client round to initialize again.
    public bool IsOpen(string id) => true;

    public void Close(string id)
    {
    }

    public void Dispose() => _server.Dispose();
}
