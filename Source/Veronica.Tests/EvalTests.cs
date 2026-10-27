using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Eval;
using Xunit;

namespace Veronica.Tests;

public sealed class EvalTests : IDisposable
{
    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "eval-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private sealed class NullHost : ITurnHost
    {
        public List<ChatMessage> Appended { get; } = [];
        public void OnRoundStart() { }
        public void OnText(string delta) { }
        public void OnReasoning(string delta) { }
        public void OnRoundStats(RoundStats stats) { }
        public Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken) =>
            Task.FromResult<ToolResult?>(new ToolResult("out"));
        public void OnNotice(string text) { }
        public void AppendTranscript(ChatMessage message) => Appended.Add(message);
    }

    private sealed class FakeChat : IChatControl
    {
        public RunControl? Run { get; set; }
        public List<string> Sent { get; } = [];
        public int Stops { get; private set; }
        public string? Refuse { get; set; }

        public Task<string?> SendAsync(string text)
        {
            if (Refuse != null)
                return Task.FromResult<string?>(Refuse);
            Sent.Add(text);
            Run?.TurnStarted(text);
            return Task.FromResult<string?>(null);
        }

        public void Stop() => Stops++;
    }

    private List<JsonElement> Lines() =>
        File.ReadAllLines(Path.Combine(_dir, RunTranscript.FileName)).Select(l => JsonDocument.Parse(l).RootElement).ToList();

    private static async Task RunScriptedTurn(ITurnHost host)
    {
        host.OnRoundStart();
        host.OnRoundStats(new RoundStats(100, 20, 1, 1) { CachedTokens = 900 });
        var call = new ToolCall("c1", "read", "{\"path\":\"a\"}");
        host.AppendTranscript(new ChatMessage(ChatRole.Assistant, "") { Reasoning = "think", ToolCalls = [call] });
        await host.OnToolCallAsync(call, new ToolCallContext(false), CancellationToken.None);
        host.AppendTranscript(new ChatMessage(ChatRole.Tool, "out") { ToolCallId = "c1", Name = "read" });
        host.OnNotice("budget");
        host.AppendTranscript(new ChatMessage(ChatRole.User, "go on"));
    }

    [Fact]
    public async Task TranscriptWritesOneLinePerMessage()
    {
        using (var transcript = new RunTranscript(_dir, "openai x", "SYS", [new ToolDefinition("read", "Reads.", "{\"type\":\"object\"}")]))
        {
            var run = new RunControl(new FakeChat(), transcript);
            var inner = new NullHost();
            run.TurnStarted("hi\nthere");
            await RunScriptedTurn(run.Wrap(inner));
            run.TurnEnded(TurnOutcome.Failed, "context full");
            Assert.Equal(3, inner.Appended.Count);
        }

        var lines = Lines();
        Assert.Equal(["setup", "user", "assistant", "tool", "notice", "nudge", "turn_end"], lines.Select(l => l.GetProperty("type").GetString()));
        Assert.Equal(Enumerable.Range(0, 7), lines.Select(l => l.GetProperty("i").GetInt32()));
        Assert.Equal("SYS", lines[0].GetProperty("system").GetString());
        Assert.Equal("object", lines[0].GetProperty("tools")[0].GetProperty("parameters").GetProperty("type").GetString());
        Assert.Equal("hi\nthere", lines[1].GetProperty("text").GetString());
        var assistant = lines[2];
        Assert.Equal(1, assistant.GetProperty("round").GetInt32());
        Assert.Equal("think", assistant.GetProperty("reasoning").GetString());
        Assert.Equal("a", assistant.GetProperty("calls")[0].GetProperty("args").GetProperty("path").GetString());
        Assert.Equal(1020, assistant.GetProperty("stats").GetProperty("context").GetInt32());
        Assert.Equal("out", lines[3].GetProperty("result").GetString());
        Assert.True(lines[3].TryGetProperty("ms", out _));
        Assert.Equal("error", lines[6].GetProperty("outcome").GetString());
        Assert.Equal("context full", lines[6].GetProperty("error").GetString());
        Assert.Equal(1020, lines[6].GetProperty("context").GetInt32());
    }

    [Fact]
    public void TranscriptKeepsUnparsableArgumentsAsText()
    {
        using (var transcript = new RunTranscript(_dir, "p", "S", []))
            transcript.Wrap(new NullHost()).AppendTranscript(new ChatMessage(ChatRole.Assistant, "") { ToolCalls = [new ToolCall("c", "read", "{broken")] });

        Assert.Equal("{broken", Lines()[1].GetProperty("calls")[0].GetProperty("args").GetString());
    }

    [Fact]
    public void TranscriptWritesLoneSurrogates()
    {
        using (var transcript = new RunTranscript(_dir, "p", "S", []))
        {
            transcript.WriteUser("bad \ud800 half");
            transcript.WriteUser("ok");
            Assert.Null(transcript.WriteError);
        }

        var lines = Lines();
        Assert.StartsWith("bad ", lines[1].GetProperty("text").GetString());
        Assert.Equal("ok", lines[2].GetProperty("text").GetString());
        Assert.Equal([0, 1, 2], lines.Select(l => l.GetProperty("i").GetInt32()));
    }

    [Fact]
    public async Task TurnEndsEvenWhenTheTranscriptIsClosed()
    {
        var transcript = new RunTranscript(_dir, "p", "S", []);
        var run = new RunControl(new FakeChat(), transcript);
        run.TurnStarted("x");
        var wait = run.WaitIdleAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
        transcript.Dispose();

        run.TurnEnded(TurnOutcome.Answered, null);

        await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("transcript_error", run.StatusJson());
    }

    [Fact]
    public void StatusReportsTurnStateAndContext()
    {
        var run = new RunControl(new FakeChat(), null);
        Assert.Equal("{\"busy\":false,\"turns\":0}", run.StatusJson());

        run.TurnStarted("x");
        run.Wrap(new NullHost()).OnRoundStats(new RoundStats(10, 5, 1, 1) { CachedTokens = 85 });
        Assert.Equal("{\"busy\":true,\"turns\":0,\"context\":100}", run.StatusJson());

        run.TurnEnded(TurnOutcome.Stopped, null);
        Assert.Equal("{\"busy\":false,\"turns\":1,\"last\":{\"outcome\":\"stopped\"},\"context\":100}", run.StatusJson());
    }

    [Fact]
    public async Task WaitIdleReturnsWhenTheTurnEnds()
    {
        var run = new RunControl(new FakeChat(), null);
        await run.WaitIdleAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        run.TurnStarted("x");
        var wait = run.WaitIdleAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.False(wait.IsCompleted);
        run.TurnEnded(TurnOutcome.Answered, null);
        await wait.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task<(int Status, string Body)> Http(int port, string method, string path, string? body = null)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"http://127.0.0.1:{port}{path}");
        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "text/plain");
        using var response = await client.SendAsync(request);
        return ((int)response.StatusCode, (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task ServerSendsStopsAndWaits()
    {
        var chat = new FakeChat();
        var run = chat.Run = new RunControl(chat, null);
        using var server = new ControlServer(0, run);

        Assert.Equal((200, "{\"busy\":false,\"turns\":0}"), await Http(server.Port, "GET", "/status"));
        Assert.Equal((200, "{\"ok\":true}"), await Http(server.Port, "POST", "/send", "Make a block\nplease \u00e9"));
        Assert.Equal(["Make a block\nplease \u00e9"], chat.Sent);
        Assert.Equal((200, "{\"ok\":true}"), await Http(server.Port, "POST", "/stop"));
        Assert.Equal(1, chat.Stops);

        var waiting = Http(server.Port, "GET", "/status?wait=60");
        await Task.Delay(200);
        Assert.False(waiting.IsCompleted);
        run.TurnEnded(TurnOutcome.Answered, null);
        var (status, body) = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(200, status);
        Assert.Contains("\"answered\"", body);

        Assert.Equal(200, (await Http(server.Port, "GET", "/status?wait=0.1")).Status);
    }

    [Fact]
    public async Task ServerRefusesBadRequests()
    {
        var chat = new FakeChat { Refuse = "A turn is running." };
        using var server = new ControlServer(0, new RunControl(chat, null));

        Assert.Equal((409, "{\"error\":\"A turn is running.\"}"), await Http(server.Port, "POST", "/send", "hi"));
        Assert.Equal(400, (await Http(server.Port, "POST", "/send", "  ")).Status);
        Assert.Equal(405, (await Http(server.Port, "GET", "/send")).Status);
        Assert.Equal(400, (await Http(server.Port, "GET", "/status?wait=soon")).Status);
        Assert.Equal(400, (await Http(server.Port, "GET", "/status?wait=NaN")).Status);
        Assert.Equal(400, (await Http(server.Port, "GET", "/status?wait=Infinity")).Status);
        Assert.Equal(404, (await Http(server.Port, "GET", "/nope")).Status);
    }

    [Fact]
    public void ServerListensOnLoopbackOnly()
    {
        using var server = new ControlServer(0, new RunControl(new FakeChat(), null));
        using var probe = new TcpClient();
        var external = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        if (external == null)
            return;
        Assert.ThrowsAny<SocketException>(() => probe.Connect(external, server.Port));
    }
}
