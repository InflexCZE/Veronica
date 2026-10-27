using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Veronica.Agent.Claude;

// Veronica's tools as an MCP Streamable HTTP endpoint on loopback: plain JSON responses, one request
// per connection, no authentication.
// A raw socket instead of HttpListener: http.sys needs a URL reservation for a non-admin user.
public sealed class McpServer : IDisposable
{
    public const string ServerName = "veronica";
    public const string EndpointPath = "/mcp";

    private const string DefaultProtocolVersion = "2025-06-18";

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private const int MaxHeaderBytes = 64 * 1024;
    private const int MaxBodyBytes = 64 * 1024 * 1024;

    private readonly TcpListener _listener;
    private readonly IReadOnlyList<ToolDefinition> _tools;
    private readonly Func<string, string, CancellationToken, Task<ToolResult?>> _call;
    private readonly CancellationTokenSource _stop = new();

    // call runs one tool by name with its arguments as JSON text; null means it was stopped.
    public McpServer(int port, IReadOnlyList<ToolDefinition> tools, Func<string, string, CancellationToken, Task<ToolResult?>> call)
    {
        _tools = tools;
        _call = call;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public string Url => $"http://127.0.0.1:{Port}{EndpointPath}";

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }

    public static string ToolNamePrefix => $"mcp__{ServerName}__";

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(100).ConfigureAwait(false);
                continue;
            }
            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using var _ = client;
        try
        {
            var stream = client.GetStream();
            Request? request;
            using (var reading = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
            {
                reading.CancelAfter(ReadTimeout);
                request = await ReadRequestAsync(stream, reading.Token).ConfigureAwait(false);
            }
            var response = request == null
                ? new Response(400, null)
                : request.Path != EndpointPath
                    ? new Response(404, null)
                    : request.Method != "POST"
                        ? new Response(405, null)
                        : await HandleAsync(request.Body).ConfigureAwait(false);
            await WriteResponseAsync(stream, response).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    private sealed record Request(string Method, string Path, string Body);

    // Body null: no content.
    private sealed record Response(int Status, string? Body);

    private async Task<Response> HandleAsync(string body)
    {
        JsonNode? message;
        try
        {
            message = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return new Response(200, Error(null, -32700, "The body is not JSON."));
        }
        if (message is not JsonObject request)
            return new Response(200, Error(null, -32600, "Expected one JSON-RPC message object."));

        // Notifications and responses to us get no reply.
        if (!request.TryGetPropertyValue("id", out var id) || request["method"] is not JsonValue methodValue)
            return new Response(202, null);

        string method = methodValue.TryGetValue(out string? m) ? m : "";
        var parameters = request["params"] as JsonObject;
        try
        {
            JsonNode result = method switch
            {
                "initialize" => Initialize(parameters),
                "ping" => new JsonObject(),
                "tools/list" => ListTools(),
                "tools/call" => await CallToolAsync(parameters).ConfigureAwait(false),
                _ => throw new RpcException(-32601, $"Unknown method '{method}'."),
            };
            return new Response(200, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = result }.ToJsonString());
        }
        catch (RpcException e)
        {
            return new Response(200, Error(id, e.Code, e.Message));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Response(200, Error(id, -32603, e.Message));
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

    private static JsonObject Initialize(JsonObject? parameters)
    {
        string version = parameters?["protocolVersion"] is JsonValue v && v.TryGetValue(out string? requested) && requested.Length > 0
            ? requested
            : DefaultProtocolVersion;
        return new JsonObject
        {
            ["protocolVersion"] = version,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject { ["name"] = ServerName, ["version"] = "1.0.0" },
        };
    }

    private JsonObject ListTools() => new()
    {
        ["tools"] = new JsonArray(_tools.Select(t => (JsonNode?)new JsonObject
        {
            ["name"] = t.Name,
            ["description"] = t.Description,
            ["inputSchema"] = JsonNode.Parse(t.Parameters),
        }).ToArray()),
    };

    private async Task<JsonObject> CallToolAsync(JsonObject? parameters)
    {
        if (parameters?["name"] is not JsonValue nameValue || !nameValue.TryGetValue(out string? name))
            throw new RpcException(-32602, "tools/call needs a tool name.");
        string arguments = parameters["arguments"]?.ToJsonString() ?? "{}";
        var result = await _call(name, arguments, _stop.Token).ConfigureAwait(false);
        return new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = result?.Output ?? ClientToolLoop.CancelledToolOutput }),
            ["isError"] = result == null,
        };
    }

    private static async Task<Request?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxHeaderBytes];
        int length = 0;
        int headerEnd;
        while ((headerEnd = buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8)) < 0)
        {
            if (length == buffer.Length)
                return null;
            int read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return null;
            length += read;
        }

        var lines = Encoding.ASCII.GetString(buffer, 0, headerEnd).Split("\r\n");
        var start = lines[0].Split(' ');
        if (start.Length != 3)
            return null;

        int contentLength = 0;
        foreach (var line in lines.Skip(1))
        {
            int colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                && (!int.TryParse(line[(colon + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out contentLength) || contentLength > MaxBodyBytes))
                return null;
        }

        var body = new byte[contentLength];
        int have = Math.Min(length - (headerEnd + 4), contentLength);
        Array.Copy(buffer, headerEnd + 4, body, 0, have);
        while (have < contentLength)
        {
            int read = await stream.ReadAsync(body.AsMemory(have), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return null;
            have += read;
        }

        string target = start[1];
        int q = target.IndexOf('?');
        return new Request(start[0], q >= 0 ? target[..q] : target, Encoding.UTF8.GetString(body));
    }

    private static async Task WriteResponseAsync(NetworkStream stream, Response response)
    {
        string reason = response.Status switch
        {
            200 => "OK",
            202 => "Accepted",
            400 => "Bad Request",
            404 => "Not Found",
            405 => "Method Not Allowed",
            _ => "Error",
        };
        var payload = response.Body == null ? [] : Encoding.UTF8.GetBytes(response.Body);
        var head = new StringBuilder($"HTTP/1.1 {response.Status} {reason}\r\n");
        if (response.Body != null)
            head.Append("Content-Type: application/json; charset=utf-8\r\n");
        if (response.Status == 405)
            head.Append("Allow: POST\r\n");
        head.Append(CultureInfo.InvariantCulture, $"Content-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString())).ConfigureAwait(false);
        await stream.WriteAsync(payload).ConfigureAwait(false);
    }
}
