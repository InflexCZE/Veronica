using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Recording;
using Xunit;

namespace Veronica.Tests;

public sealed class RecordingTests
{
    private readonly MemoryRecordStore _store = new();

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

    private RecordingProvider Recording(Func<TurnRequest, ITurnHost, Task> turn) => new(new ScriptedProvider(turn), _store, "openai x");

    private static TurnRequest Request(string user, IReadOnlyList<ToolDefinition>? tools = null) =>
        new("SYS", [new ChatMessage(ChatRole.User, user)], tools ?? [], new TurnLimits());

    private static List<JsonElement> Parse(string copy) =>
        copy.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement).ToList();

    // Each session's lines, by the file its header names.
    private static Dictionary<string, List<JsonElement>> Sessions(MemoryRecordStore store)
    {
        var sessions = new Dictionary<string, List<JsonElement>>();
        List<JsonElement>? current = null;
        foreach (var line in Parse(store.Copy()))
        {
            if (line.GetProperty("type").GetString() == "session")
                sessions.Add(line.GetProperty("file").GetString()!, current = []);
            else
                current!.Add(line);
        }
        return sessions;
    }

    private List<JsonElement> Lines(string file = "transcript.jsonl") => Sessions(_store)[file];

    private static List<string?> Types(IEnumerable<JsonElement> lines) => lines.Select(l => l.GetProperty("type").GetString()).ToList();

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
        Assert.Equal(["setup", "user", "assistant", "tool", "notice", "nudge", "turn_end"], Types(lines));
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
        using (var session = provider.OpenSession(IProvider.ChatSession))
        {
            using (var compaction = provider.OpenSession(Compaction.SessionName))
                await compaction.RunTurnAsync(Request("BRIEF"), new NullHost(), CancellationToken.None);
            await session.RunTurnAsync(Request("SUMMARY") with { Compacted = true }, new NullHost(), CancellationToken.None);
        }

        var lines = Lines();
        Assert.Equal(["setup", "compacted", "turn_end"], Types(lines));
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
        Assert.Equal(["setup", "user", "turn_end", "user", "turn_end", "setup", "user", "turn_end"], Types(lines));
        Assert.Equal("answered", lines[2].GetProperty("outcome").GetString());
    }

    // A clear keeps the eval file whole and marks it.
    [Fact]
    public async Task ClearIsRecordedAndTheNextTurnWritesItsSetupAgain()
    {
        var lines = new List<string>();
        var provider = new RecordingProvider(new ScriptedProvider((_, _) => Task.CompletedTask), new ListStore(lines), "openai x");
        using (var session = provider.OpenSession(IProvider.ChatSession))
        {
            var tools = new List<ToolDefinition>();
            await session.RunTurnAsync(Request("a", tools), new NullHost(), CancellationToken.None);
            await session.ResetAsync();
            await session.RunTurnAsync(Request("b", tools), new NullHost(), CancellationToken.None);
        }

        Assert.Equal(["setup", "user", "turn_end", "clear", "setup", "user", "turn_end"], Types(Parse(string.Concat(lines))));
    }

    [Fact]
    public async Task CopyHoldsOnlyWhatFollowsTheLastClear()
    {
        var provider = Recording(WikiTurn);
        using (var session = provider.OpenSession(IProvider.ChatSession))
        {
            await session.RunTurnAsync(Request("old"), new SubSessionHost(provider), CancellationToken.None);
            await session.ResetAsync();
            await session.RunTurnAsync(Request("new"), new NullHost(), CancellationToken.None);
        }

        var sessions = Sessions(_store);
        Assert.Equal(["transcript.jsonl"], sessions.Keys);
        Assert.Equal(["setup", "user", "assistant", "tool", "turn_end"], Types(sessions["transcript.jsonl"]));
        Assert.Equal("new", sessions["transcript.jsonl"][1].GetProperty("text").GetString());
    }

    [Fact]
    public void CopyOfAnUnusedChatIsEmpty()
    {
        using var session = Recording((_, _) => Task.CompletedTask).OpenSession(IProvider.ChatSession);
        Assert.Equal("", _store.Copy());
    }

    [Fact]
    public async Task CopyDuringATurnHoldsWhatIsRecordedSoFar()
    {
        string? during = null;
        var provider = Recording(async (_, host) =>
        {
            var call = new ToolCall("c1", "read", "{}");
            host.AppendTranscript(new ChatMessage(ChatRole.Assistant, "") { ToolCalls = [call] });
            await host.OnToolCallAsync(call, new ToolCallContext(false), CancellationToken.None);
            during = _store.Copy();
            host.AppendTranscript(new ChatMessage(ChatRole.Tool, "out") { ToolCallId = "c1", Name = "read" });
        });
        using (var session = provider.OpenSession(IProvider.ChatSession))
            await session.RunTurnAsync(Request("go"), new NullHost(), CancellationToken.None);

        Assert.Equal(["session", "setup", "user", "assistant"], Types(Parse(during!)));
        Assert.Equal(["setup", "user", "assistant", "tool", "turn_end"], Types(Lines()));
    }

    private static async Task WikiTurn(TurnRequest request, ITurnHost host)
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
    }

    [Fact]
    public async Task SessionOpenedDuringAToolCallGetsItsOwnSectionLinkedBothWays()
    {
        var provider = Recording(WikiTurn);
        using (var session = provider.OpenSession(IProvider.ChatSession))
            await session.RunTurnAsync(Request("go"), new SubSessionHost(provider), CancellationToken.None);

        var copy = Parse(_store.Copy());
        Assert.Equal(["session", "setup", "user", "assistant", "tool", "turn_end", "session", "setup", "user", "assistant", "turn_end"], Types(copy));
        Assert.Equal("transcript.jsonl", copy[0].GetProperty("file").GetString());
        Assert.Equal("wiki-1.jsonl", copy[6].GetProperty("file").GetString());

        Assert.Equal(["wiki-1.jsonl"], copy[4].GetProperty("sessions").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("transcript.jsonl", copy[7].GetProperty("parent").GetProperty("file").GetString());
        Assert.Equal("c7", copy[7].GetProperty("parent").GetProperty("call").GetString());
        Assert.Equal("brief", copy[8].GetProperty("text").GetString());
    }

    [Fact]
    public async Task StackedRecordingsEachLinkTheirOwnSessions()
    {
        var innerStore = new MemoryRecordStore();
        var inner = new RecordingProvider(new ScriptedProvider(WikiTurn), innerStore, "openai x");
        var outer = new RecordingProvider(inner, _store, "openai x");
        using (var session = outer.OpenSession(IProvider.ChatSession))
            await session.RunTurnAsync(Request("go"), new SubSessionHost(outer), CancellationToken.None);

        foreach (var store in new[] { _store, innerStore })
        {
            var sessions = Sessions(store);
            var tool = sessions["transcript.jsonl"].Single(l => l.GetProperty("type").GetString() == "tool");
            Assert.Equal(["wiki-1.jsonl"], tool.GetProperty("sessions").EnumerateArray().Select(e => e.GetString()));
            Assert.Equal("c7", sessions["wiki-1.jsonl"][0].GetProperty("parent").GetProperty("call").GetString());
        }
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
    public async Task WriteFailureIsReportedAndDoesNotFailTheTurn()
    {
        var provider = new RecordingProvider(new ScriptedProvider((_, _) => Task.CompletedTask), new FailingStore(), "openai x");
        using var session = provider.OpenSession(IProvider.ChatSession);

        await session.RunTurnAsync(Request("x"), new NullHost(), CancellationToken.None);

        Assert.Equal("disk full", provider.WriteError);
    }

    private sealed class ListStore(List<string> lines) : IRecordStore
    {
        public void Append(string fileName, byte[] line) => lines.Add(System.Text.Encoding.UTF8.GetString(line));
        public void ChatCleared() { }
    }

    private sealed class FailingStore : IRecordStore
    {
        public void Append(string fileName, byte[] line) => throw new IOException("disk full");
        public void ChatCleared() { }
    }
}
