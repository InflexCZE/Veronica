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
            Run?.TurnStarted();
            return Task.FromResult<string?>(null);
        }

        public void Stop() => Stops++;

        public QuestionBoard? Board { get; init; }

        public Task<string?> AnswerAsync(IReadOnlyList<string?> answers) =>
            Task.FromResult(Board == null ? "No board." : Board.Answer(answers).Refused);
    }

    // A provider whose sessions run a scripted turn on the host they are given.
    private sealed class ScriptedProvider(Func<TurnRequest, ITurnHost, Task> turn) : IProvider
    {
        public int ContextWindow => IProvider.AssumedContextWindow;
        public bool RunsSubagents => true;
        public IProviderSession OpenSession(string name) => new Session(turn);

        private sealed class Session(Func<TurnRequest, ITurnHost, Task> turn) : IProviderSession
        {
            public Task RunTurnAsync(TurnRequest request, ITurnHost host, CancellationToken cancellationToken) => turn(request, host);
            public Task ResetAsync() => Task.CompletedTask;
            public void Dispose() { }
        }
    }

    private RecordingProvider Recording(Func<TurnRequest, ITurnHost, Task> turn) => new(new ScriptedProvider(turn), _dir, "openai x");

    private static TurnRequest Request(string user, IReadOnlyList<ToolDefinition>? tools = null) =>
        new("SYS", [new ChatMessage(ChatRole.User, user)], tools ?? [], new TurnLimits());

    private List<JsonElement> Lines(string file = "transcript.jsonl") =>
        File.ReadAllLines(Path.Combine(_dir, file)).Select(l => JsonDocument.Parse(l).RootElement).ToList();

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
        var inner = new NullHost();
        var provider = Recording(async (_, host) =>
        {
            await RunScriptedTurn(host);
            throw new InvalidOperationException("context full");
        });
        using (var session = provider.OpenSession(IProvider.ChatSession))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                session.RunTurnAsync(Request("hi\nthere", [new ToolDefinition("read", "Reads.", "{\"type\":\"object\"}")]), inner, CancellationToken.None));
        }
        Assert.Equal(3, inner.Appended.Count);

        var lines = Lines();
        Assert.Equal(["setup", "user", "assistant", "tool", "notice", "nudge", "turn_end"], lines.Select(l => l.GetProperty("type").GetString()));
        Assert.Equal(Enumerable.Range(0, 7), lines.Select(l => l.GetProperty("i").GetInt32()));
        Assert.Equal("SYS", lines[0].GetProperty("system").GetString());
        Assert.Equal("openai x", lines[0].GetProperty("provider").GetString());
        Assert.False(lines[0].TryGetProperty("parent", out _));
        Assert.False(lines[3].TryGetProperty("sessions", out _));
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
    public async Task ACompactedTurnRecordsItsSummaryAndTheSessionThatWroteIt()
    {
        var provider = Recording((_, _) => Task.CompletedTask);
        using (var compaction = provider.OpenSession(Compaction.SessionName))
            await compaction.RunTurnAsync(Request("BRIEF"), new NullHost(), CancellationToken.None);
        using (var session = provider.OpenSession(IProvider.ChatSession))
            await session.RunTurnAsync(Request("SUMMARY") with { Compacted = true }, new NullHost(), CancellationToken.None);

        var lines = Lines();
        Assert.Equal(["setup", "compacted", "turn_end"], lines.Select(l => l.GetProperty("type").GetString()));
        Assert.Equal("SUMMARY", lines[1].GetProperty("text").GetString());
        Assert.Equal("compaction-1.jsonl", lines[1].GetProperty("session").GetString());
        Assert.Equal("BRIEF", Lines("compaction-1.jsonl")[1].GetProperty("text").GetString());
    }

    [Fact]
    public async Task SetupIsWrittenAgainOnlyWhenTheSystemPromptChanges()
    {
        var provider = Recording((_, _) => Task.CompletedTask);
        using (var session = provider.OpenSession(IProvider.ChatSession))
        {
            var tools = new List<ToolDefinition>();
            await session.RunTurnAsync(Request("a", tools), new NullHost(), CancellationToken.None);
            await session.RunTurnAsync(Request("b", tools), new NullHost(), CancellationToken.None);
            await session.RunTurnAsync(Request("c", tools) with { SystemPrompt = "SYS2" }, new NullHost(), CancellationToken.None);
        }

        var lines = Lines();
        Assert.Equal(["setup", "user", "turn_end", "user", "turn_end", "setup", "user", "turn_end"], lines.Select(l => l.GetProperty("type").GetString()));
        Assert.Equal("answered", lines[2].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task ClearIsRecordedAndTheNextTurnWritesItsSetupAgain()
    {
        var provider = Recording((_, _) => Task.CompletedTask);
        using (var session = provider.OpenSession(IProvider.ChatSession))
        {
            var tools = new List<ToolDefinition>();
            await session.RunTurnAsync(Request("a", tools), new NullHost(), CancellationToken.None);
            await session.ResetAsync();
            await session.RunTurnAsync(Request("b", tools), new NullHost(), CancellationToken.None);
        }

        Assert.Equal(["setup", "user", "turn_end", "clear", "setup", "user", "turn_end"], Lines().Select(l => l.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task SessionOpenedDuringAToolCallGetsItsOwnFileLinkedBothWays()
    {
        var provider = Recording(async (request, host) =>
        {
            if (request.SystemPrompt == "SUB")
            {
                host.AppendTranscript(new ChatMessage(ChatRole.Assistant, "the answer"));
                return;
            }
            var call = new ToolCall("c7", "wiki", "{}");
            host.AppendTranscript(new ChatMessage(ChatRole.Assistant, "") { ToolCalls = [call] });
            await host.OnToolCallAsync(call, new ToolCallContext(false), CancellationToken.None);
            host.AppendTranscript(new ChatMessage(ChatRole.Tool, "the answer") { ToolCallId = "c7", Name = "wiki" });
        });
        using (var session = provider.OpenSession(IProvider.ChatSession))
            await session.RunTurnAsync(Request("go"), new SubSessionHost(provider), CancellationToken.None);

        var tool = Lines().Single(l => l.GetProperty("type").GetString() == "tool");
        Assert.Equal(["wiki-1.jsonl"], tool.GetProperty("sessions").EnumerateArray().Select(e => e.GetString()));
        var sub = Lines("wiki-1.jsonl");
        Assert.Equal(["setup", "user", "assistant", "turn_end"], sub.Select(l => l.GetProperty("type").GetString()));
        Assert.Equal("transcript.jsonl", sub[0].GetProperty("parent").GetProperty("file").GetString());
        Assert.Equal("c7", sub[0].GetProperty("parent").GetProperty("call").GetString());
        Assert.Equal("brief", sub[1].GetProperty("text").GetString());
    }

    // Runs a subagent session from inside the chat's tool call, as the wiki tool does.
    private sealed class SubSessionHost(IProvider provider) : ITurnHost
    {
        public void OnRoundStart() { }
        public void OnText(string delta) { }
        public void OnReasoning(string delta) { }
        public void OnRoundStats(RoundStats stats) { }
        public void OnNotice(string text) { }
        public void AppendTranscript(ChatMessage message) { }

        public async Task<ToolResult?> OnToolCallAsync(ToolCall call, ToolCallContext context, CancellationToken cancellationToken)
        {
            using var session = provider.OpenSession("wiki");
            await session.RunTurnAsync(new TurnRequest("SUB", [new ChatMessage(ChatRole.User, "brief")], [], new TurnLimits()), new NullHost(), cancellationToken);
            return new ToolResult("the answer");
        }
    }

    [Fact]
    public async Task TranscriptKeepsUnparsableArgumentsAsText()
    {
        var provider = Recording((_, host) =>
        {
            host.AppendTranscript(new ChatMessage(ChatRole.Assistant, "") { ToolCalls = [new ToolCall("c", "read", "{broken")] });
            return Task.CompletedTask;
        });
        using (var session = provider.OpenSession(IProvider.ChatSession))
            await session.RunTurnAsync(Request("x"), new NullHost(), CancellationToken.None);

        Assert.Equal("{broken", Lines()[2].GetProperty("calls")[0].GetProperty("args").GetString());
    }

    [Fact]
    public async Task TranscriptWritesLoneSurrogates()
    {
        var provider = Recording((_, _) => Task.CompletedTask);
        using (var session = provider.OpenSession(IProvider.ChatSession))
        {
            await session.RunTurnAsync(Request("bad \ud800 half"), new NullHost(), CancellationToken.None);
            await session.RunTurnAsync(Request("ok"), new NullHost(), CancellationToken.None);
        }
        Assert.Null(provider.WriteError);

        var lines = Lines();
        Assert.StartsWith("bad ", lines[1].GetProperty("text").GetString());
        Assert.Equal("ok", lines[3].GetProperty("text").GetString());
        Assert.Equal([0, 1, 2, 3, 4], lines.Select(l => l.GetProperty("i").GetInt32()));
    }

    [Fact]
    public async Task WriteFailureShowsInStatusAndDoesNotFailTheTurn()
    {
        var provider = Recording((_, _) => Task.CompletedTask);
        var session = provider.OpenSession(IProvider.ChatSession);
        session.Dispose();

        await session.RunTurnAsync(Request("x"), new NullHost(), CancellationToken.None);

        Assert.NotNull(provider.WriteError);
        Assert.Contains("transcript_error", new RunControl(new FakeChat(), () => provider.WriteError).StatusJson());
    }

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
