using System.Text.Json;
using System.Text.Json.Nodes;

namespace Veronica.Agent.Mcp;

public sealed record McpSessionStart(string Id, string? Instructions);

// Sessions by Mcp-Session-Id: each initialize opens one, a DELETE closes one.
public interface IMcpSessions
{
    McpSessionStart Open();

    bool IsOpen(string id);

    void Close(string id);
}

public sealed record McpHttpRequest(string Method, string Path, string? SessionId, string Body);

// Body null: no content. SessionId: the Mcp-Session-Id header to send back.
public sealed record McpHttpResponse(int Status, string? Body, string? SessionId = null);

// call runs one tool by name with its arguments as JSON text, for the request's session id or null without
// one; a null result means it was stopped.
public sealed class McpProtocol(
    string serverName,
    IReadOnlyList<ToolDefinition> tools,
    Func<string?, string, string, CancellationToken, Task<ToolResult?>> call,
    IMcpSessions? sessions = null)
{
    public const string EndpointPath = "/mcp";

    private const string DefaultProtocolVersion = "2025-06-18";

    public async Task<McpHttpResponse> HandleAsync(McpHttpRequest request, CancellationToken stop)
    {
        if (request.Path != EndpointPath)
            return new McpHttpResponse(404, null);
        // An id the server does not know, as after an Editor restart, makes a conforming client start a new
        // session; some clients go by the error code in the body rather than the status. An initialize
        // still carrying an old id is that new session starting.
        if (sessions != null && request.SessionId != null && !sessions.IsOpen(request.SessionId) && !IsInitialize(request))
            return new McpHttpResponse(404, Error(null, -32001, "Session not found."));
        if (request.Method == "DELETE" && sessions != null && request.SessionId != null)
        {
            sessions.Close(request.SessionId);
            return new McpHttpResponse(200, null);
        }
        if (request.Method != "POST")
            return new McpHttpResponse(405, null);
        return await HandlePostAsync(request.Body, request.SessionId, stop).ConfigureAwait(false);
    }

    private static bool IsInitialize(McpHttpRequest request)
    {
        if (request.Method != "POST")
            return false;
        try
        {
            return JsonNode.Parse(request.Body) is JsonObject message && message["method"] is JsonValue method
                && method.TryGetValue(out string? name) && name == "initialize";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<McpHttpResponse> HandlePostAsync(string body, string? sessionId, CancellationToken stop)
    {
        JsonNode? message;
        try
        {
            message = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return new McpHttpResponse(200, Error(null, -32700, "The body is not JSON."));
        }
        if (message is not JsonObject request)
            return new McpHttpResponse(200, Error(null, -32600, "Expected one JSON-RPC message object."));

        // Notifications and responses to us get no reply.
        if (!request.TryGetPropertyValue("id", out var id) || request["method"] is not JsonValue methodValue)
            return new McpHttpResponse(202, null);

        string method = methodValue.TryGetValue(out string? m) ? m : "";
        var parameters = request["params"] as JsonObject;
        try
        {
            string? opened = null;
            JsonNode result;
            switch (method)
            {
                case "initialize":
                    var start = sessions?.Open();
                    opened = start?.Id;
                    result = Initialize(parameters, start?.Instructions);
                    break;
                case "ping":
                    result = new JsonObject();
                    break;
                case "tools/list":
                    result = ListTools();
                    break;
                case "tools/call":
                    result = await CallToolAsync(parameters, sessionId, stop).ConfigureAwait(false);
                    break;
                default:
                    throw new RpcException(-32601, $"Unknown method '{method}'.");
            }
            return new McpHttpResponse(200, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = result }.ToJsonString(), opened);
        }
        catch (RpcException e)
        {
            return new McpHttpResponse(200, Error(id, e.Code, e.Message));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new McpHttpResponse(200, Error(id, -32603, e.Message));
        }
    }

    private sealed class RpcException(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }

    private static string Error(JsonNode? id, int code, string message) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        }.ToJsonString();

    private JsonObject Initialize(JsonObject? parameters, string? instructions)
    {
        string version = parameters?["protocolVersion"] is JsonValue v && v.TryGetValue(out string? requested) && requested.Length > 0
            ? requested
            : DefaultProtocolVersion;
        var result = new JsonObject
        {
            ["protocolVersion"] = version,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject { ["name"] = serverName, ["version"] = "1.0.0" },
        };
        if (instructions != null)
            result["instructions"] = instructions;
        return result;
    }

    private JsonObject ListTools() => new()
    {
        ["tools"] = new JsonArray(tools.Select(t => (JsonNode?)new JsonObject
        {
            ["name"] = t.Name,
            ["description"] = t.Description,
            ["inputSchema"] = JsonNode.Parse(t.Parameters),
        }).ToArray()),
    };

    private async Task<JsonObject> CallToolAsync(JsonObject? parameters, string? sessionId, CancellationToken stop)
    {
        if (parameters?["name"] is not JsonValue nameValue || !nameValue.TryGetValue(out string? name))
            throw new RpcException(-32602, "tools/call needs a tool name.");
        string arguments = parameters["arguments"]?.ToJsonString() ?? "{}";
        var result = await call(sessionId, name, arguments, stop).ConfigureAwait(false);
        return new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = result?.Output ?? ClientToolLoop.CancelledToolOutput }),
            ["isError"] = result == null,
        };
    }
}
