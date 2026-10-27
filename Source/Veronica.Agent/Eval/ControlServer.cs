using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Veronica.Agent.Eval;

// The eval control channel: a minimal HTTP/1.1 JSON endpoint on loopback, one request per connection.
// A raw socket instead of HttpListener: http.sys needs a URL reservation for a non-admin user.
public sealed class ControlServer : IDisposable
{
    public static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxBodyBytes = 1024 * 1024;

    private readonly TcpListener _listener;
    private readonly RunControl _run;
    private readonly CancellationTokenSource _stop = new();

    public ControlServer(int port, RunControl run)
    {
        _run = run;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }

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
            (int status, string body) = (400, Error("Malformed request."));
            if (request != null)
            {
                try
                {
                    (status, body) = await RouteAsync(request).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    (status, body) = (500, Error(e.Message));
                }
            }
            await WriteResponseAsync(stream, status, body).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    private sealed record Request(string Method, string Path, IReadOnlyDictionary<string, string> Query, string Body);

    private async Task<(int Status, string Body)> RouteAsync(Request request)
    {
        switch (request.Path)
        {
            case "/status":
                if (request.Method != "GET")
                    return (405, Error("Use GET."));
                if (request.Query.TryGetValue("wait", out var wait))
                {
                    if (!double.TryParse(wait, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) || !double.IsFinite(seconds) || seconds < 0)
                        return (400, Error("wait must be a number of seconds."));
                    var timeout = TimeSpan.FromSeconds(Math.Min(seconds, MaxWait.TotalSeconds));
                    await _run.WaitIdleAsync(timeout, _stop.Token).ConfigureAwait(false);
                }
                return (200, _run.StatusJson());
            case "/send":
                if (request.Method != "POST")
                    return (405, Error("Use POST with the message as the body."));
                if (request.Body.Trim().Length == 0)
                    return (400, Error("The message is empty."));
                return await _run.Chat.SendAsync(request.Body).ConfigureAwait(false) is { } refused
                    ? (409, Error(refused))
                    : (200, "{\"ok\":true}");
            case "/stop":
                if (request.Method != "POST")
                    return (405, Error("Use POST."));
                _run.Chat.Stop();
                return (200, "{\"ok\":true}");
            default:
                return (404, Error("Unknown path. Paths: GET /status[?wait=<s>], POST /send, POST /stop."));
        }
    }

    private static string Error(string message) =>
        "{\"error\":" + System.Text.Json.JsonSerializer.Serialize(message) + "}";

    private static async Task<Request?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxHeaderBytes];
        int length = 0;
        int headerEnd;
        while ((headerEnd = IndexOf(buffer, length, "\r\n\r\n"u8)) < 0)
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
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (q >= 0)
        {
            foreach (var pair in target[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                query[Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq])] = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
            target = target[..q];
        }
        return new Request(start[0], target, query, Encoding.UTF8.GetString(body));
    }

    private static int IndexOf(byte[] buffer, int length, ReadOnlySpan<byte> value) =>
        buffer.AsSpan(0, length).IndexOf(value);

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string body)
    {
        string reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            405 => "Method Not Allowed",
            409 => "Conflict",
            _ => "Error",
        };
        var payload = Encoding.UTF8.GetBytes(body + "\n");
        var head = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(head).ConfigureAwait(false);
        await stream.WriteAsync(payload).ConfigureAwait(false);
    }
}
