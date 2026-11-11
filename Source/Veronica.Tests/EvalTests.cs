using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Eval;
using Xunit;

namespace Veronica.Tests;

public sealed class EvalTests
{
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
            Run?.TurnStarted();
            return Task.FromResult<string?>(null);
        }

        public void Stop() => Stops++;

        public QuestionBoard? Board { get; init; }

        public Task<string?> AnswerAsync(IReadOnlyList<string?> answers) =>
            Task.FromResult(Board == null ? "No board." : Board.Answer(answers).Refused);
    }

    [Fact]
    public void StatusReportsTheRecordingsWriteError() =>
        Assert.Contains("\"transcript_error\":\"disk full\"", new RunControl(new FakeChat(), () => "disk full").StatusJson());

    [Fact]
    public void StatusReportsTurnStateAndContext()
    {
        var run = new RunControl(new FakeChat(), null);
        Assert.Equal("{\"busy\":false,\"turns\":0}", run.StatusJson());

        run.TurnStarted();
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

        run.TurnStarted();
        var wait = run.WaitIdleAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.False(wait.IsCompleted);
        run.TurnEnded(TurnOutcome.Answered, null);
        await wait.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Turn_with_a_queued_next_one_never_reports_idle()
    {
        var run = new RunControl(new FakeChat(), null);
        run.TurnStarted();
        var wait = run.WaitIdleAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        run.TurnEnded(TurnOutcome.Answered, null, nextQueued: true);
        run.TurnStarted();
        await Task.Delay(100);
        Assert.False(wait.IsCompleted);
        Assert.Contains("\"busy\":true,\"turns\":1", run.StatusJson());

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
    public async Task ServerShowsOpenQuestionsAndTakesTheirAnswers()
    {
        var board = new QuestionBoard();
        var chat = new FakeChat { Board = board };
        var run = chat.Run = new RunControl(chat, null, board);
        using var server = new ControlServer(0, run);

        Assert.Equal((200, "{\"busy\":false,\"turns\":0,\"questions\":[]}"), await Http(server.Port, "GET", "/status"));
        Assert.Equal(409, (await Http(server.Port, "POST", "/answer", "[\"x\"]")).Status);

        run.TurnStarted();
        var waiting = Http(server.Port, "GET", "/status?wait=60");
        await Task.Delay(200);
        Assert.False(waiting.IsCompleted);
        var answer = board.WaitAsync([new Question("Name?", ["Bob", "Ann"]), new Question("Folder?", [])], CancellationToken.None);
        var (_, body) = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("{\"busy\":true,\"turns\":0,\"questions\":[{\"question\":\"Name?\",\"options\":[\"Bob\",\"Ann\"]},{\"question\":\"Folder?\",\"options\":[]}]}", body);

        Assert.Equal(400, (await Http(server.Port, "POST", "/answer", "{\"a\":1}")).Status);
        Assert.Equal(400, (await Http(server.Port, "POST", "/answer", "[1]")).Status);
        Assert.Equal(409, (await Http(server.Port, "POST", "/answer", "[\"Bob\"]")).Status);
        Assert.Equal((200, "{\"ok\":true}"), await Http(server.Port, "POST", "/answer", "[\"Bob\",null]"));
        Assert.Equal("Q: Name?\nA: Bob\n\nQ: Folder?\nA: (skipped)", await answer.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ServerReportsValidationWithItsWait()
    {
        TimeSpan? asked = null;
        using var server = new ControlServer(0, new RunControl(new FakeChat(), null), (wait, _) =>
        {
            asked = wait;
            return Task.FromResult("{\"errors\":0}");
        });
        Assert.Equal((200, "{\"errors\":0}"), await Http(server.Port, "GET", "/validation?wait=5"));
        Assert.Equal(TimeSpan.FromSeconds(5), asked);
        Assert.Equal(400, (await Http(server.Port, "GET", "/validation?wait=x")).Status);
        Assert.Equal(405, (await Http(server.Port, "POST", "/validation")).Status);

        using var bare = new ControlServer(0, new RunControl(new FakeChat(), null));
        Assert.Equal(404, (await Http(bare.Port, "GET", "/validation")).Status);
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
