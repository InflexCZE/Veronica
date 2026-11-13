using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Veronica.Agent.Mcp;

// Veronica's tools as an MCP Streamable HTTP endpoint on loopback: plain JSON responses, one request
// per connection, no authentication.
// A raw socket instead of HttpListener: http.sys needs a URL reservation for a non-admin user.
public sealed class McpServer : IDisposable
{
    public const string ServerName = "veronica";
    public const string EndpointPath = McpProtocol.EndpointPath;

    private const string SessionHeader = "Mcp-Session-Id";

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private const int MaxHeaderBytes = 64 * 1024;
    private const int MaxBodyBytes = 64 * 1024 * 1024;

    private readonly TcpListener _listener;
    private readonly McpProtocol _protocol;
    private readonly bool _sessions;
    private readonly CancellationTokenSource _stop = new();

    // call runs one tool by name with its arguments as JSON text, for the request's session id or null
    // without one; null means it was stopped. Without sessions no ids are issued.
    public McpServer(int port, IReadOnlyList<ToolDefinition> tools, Func<string?, string, string, CancellationToken, Task<ToolResult?>> call, IMcpSessions? sessions = null)
    {
        _protocol = new McpProtocol(ServerName, tools, call, sessions);
        _sessions = sessions != null;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public string Url => UrlFor(Port);

    public static string UrlFor(int port) => $"http://127.0.0.1:{port}{EndpointPath}";

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
            McpHttpRequest? request;
            using (var reading = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
            {
                reading.CancelAfter(ReadTimeout);
                request = await ReadRequestAsync(stream, reading.Token).ConfigureAwait(false);
            }
            var response = request == null
                ? new McpHttpResponse(400, null)
                : await _protocol.HandleAsync(request, _stop.Token).ConfigureAwait(false);
            await WriteResponseAsync(stream, response).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    private static async Task<McpHttpRequest?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
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
        string? session = null;
        foreach (var line in lines.Skip(1))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0)
                continue;
            string name = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength) || contentLength > MaxBodyBytes))
                return null;
            if (name.Equals(SessionHeader, StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                session = value;
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
        return new McpHttpRequest(start[0], q >= 0 ? target[..q] : target, session, Encoding.UTF8.GetString(body));
    }

    private async Task WriteResponseAsync(NetworkStream stream, McpHttpResponse response)
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
        if (response.SessionId != null)
            head.Append(CultureInfo.InvariantCulture, $"{SessionHeader}: {response.SessionId}\r\n");
        if (response.Status == 405)
            head.Append(_sessions ? "Allow: POST, DELETE\r\n" : "Allow: POST\r\n");
        head.Append(CultureInfo.InvariantCulture, $"Content-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString())).ConfigureAwait(false);
        await stream.WriteAsync(payload).ConfigureAwait(false);
    }
}
