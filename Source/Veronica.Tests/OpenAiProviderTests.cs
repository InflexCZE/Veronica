using System.Net;
using System.Text;
using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.OpenAi;
using Xunit;

namespace Veronica.Tests;

public sealed class OpenAiProviderTests
{
    private sealed class Server(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return respond(request, body);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Sse(params string[] chunks) =>
        new(HttpStatusCode.OK) { Content = new StringContent(string.Concat(chunks.Select(c => $"data: {c}\n\n")) + "data: [DONE]\n\n", Encoding.UTF8, "text/event-stream") };

    private static readonly RoundRequest Request = new(
        [
            new ChatMessage(ChatRole.System, "SYS"),
            new ChatMessage(ChatRole.User, "Hi"),
            new ChatMessage(ChatRole.Assistant, "") { Reasoning = "think", ToolCalls = [new ToolCall("c1", "read", "{\"path\":\"a\"}")] },
            new ChatMessage(ChatRole.Tool, "OUT") { ToolCallId = "c1", Name = "read" },
        ],
        [new ToolDefinition("read", "Reads.", "{\"type\":\"object\"}")],
        true,
        4000);

    private static async Task<List<StreamDelta>> Stream(OpenAiProvider provider, CancellationToken cancellationToken = default)
    {
        var deltas = new List<StreamDelta>();
        await provider.StreamRoundAsync(Request, deltas.Add, cancellationToken);
        return deltas;
    }

    [Fact]
    public async Task SendsTranscriptToolsAndKey()
    {
        var server = new Server((_, _) => Sse("{\"choices\":[{\"delta\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}"));
        using var provider = new OpenAiProvider(new OpenAiSettings("http://host:8080/v1/", "m1", "KEY"), server);

        await Stream(provider);

        var (request, body) = Assert.Single(server.Requests);
        Assert.Equal("http://host:8080/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer KEY", request.Headers.Authorization!.ToString());
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("m1", json.GetProperty("model").GetString());
        Assert.True(json.GetProperty("stream").GetBoolean());
        Assert.Equal(4000, json.GetProperty("max_tokens").GetInt32());
        var messages = json.GetProperty("messages");
        Assert.Equal(["system", "user", "assistant", "tool"], messages.EnumerateArray().Select(m => m.GetProperty("role").GetString()));
        Assert.Equal("think", messages[2].GetProperty("reasoning_content").GetString());
        Assert.Equal("{\"path\":\"a\"}", messages[2].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("arguments").GetString());
        Assert.Equal("c1", messages[3].GetProperty("tool_call_id").GetString());
        Assert.False(messages[3].TryGetProperty("name", out _));
        Assert.Equal("object", json.GetProperty("tools")[0].GetProperty("function").GetProperty("parameters").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(true, 1000, 5000, 1000)]
    [InlineData(null, 1000, 5000, 1000)]
    [InlineData(false, 1000, 4000, null)]
    [InlineData(true, null, 4000, null)]
    public async Task ThinkingBudgetComesOnTopOfMaxTokens(bool? thinking, int? budget, int maxTokens, int? sentBudget)
    {
        var server = new Server((_, _) => Sse("{\"choices\":[{\"delta\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}"));
        using var provider = new OpenAiProvider(new OpenAiSettings("http://host/v1", "m", null), server);

        await provider.StreamRoundAsync(Request with { Thinking = thinking, ThinkingBudget = budget }, _ => { }, CancellationToken.None);

        var json = JsonDocument.Parse(Assert.Single(server.Requests).Body).RootElement;
        Assert.Equal(maxTokens, json.GetProperty("max_tokens").GetInt32());
        Assert.Equal(sentBudget, json.TryGetProperty("thinking_budget_tokens", out var b) ? b.GetInt32() : null);
    }

    [Theory]
    [InlineData("medium", true, "medium")]
    [InlineData(" low ", null, "low")]
    [InlineData("medium", false, null)]
    [InlineData(null, true, null)]
    [InlineData("", true, null)]
    public async Task SendsReasoningEffortToTheChatTemplate(string? effort, bool? thinking, string? sent)
    {
        var server = new Server((_, _) => Sse("{\"choices\":[{\"delta\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}"));
        using var provider = new OpenAiProvider(new OpenAiSettings("http://host/v1", "m", null, effort), server);

        await provider.StreamRoundAsync(Request with { Thinking = thinking }, _ => { }, CancellationToken.None);

        var json = JsonDocument.Parse(Assert.Single(server.Requests).Body).RootElement;
        Assert.Equal(sent, json.TryGetProperty("chat_template_kwargs", out var kwargs) ? kwargs.GetProperty("reasoning_effort").GetString() : null);
    }

    [Fact]
    public async Task TakesTheFirstListedModelOnce()
    {
        var server = new Server((request, _) => request.Method == HttpMethod.Get
            ? Json("{\"data\":[{\"id\":\"first\"},{\"id\":\"second\"}]}")
            : Sse("{\"choices\":[{\"delta\":{\"content\":\"ok\"}}]}"));
        using var provider = new OpenAiProvider(new OpenAiSettings("http://host/v1", null, null), server);

        await Stream(provider);
        await Stream(provider);

        Assert.Equal(["http://host/v1/models", "http://host/v1/chat/completions", "http://host/v1/chat/completions"],
            server.Requests.Select(r => r.Request.RequestUri!.ToString()));
        Assert.Null(server.Requests[0].Request.Headers.Authorization);
        Assert.Equal("first", JsonDocument.Parse(server.Requests[2].Body).RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task StreamsTextReasoningToolCallsAndStats()
    {
        var server = new Server((_, _) => Sse(
            "{\"choices\":[{\"delta\":{\"reasoning_content\":\"hm\"}}]}",
            "{\"choices\":[{\"delta\":{\"reasoning\":\"m\"}}]}",
            "{\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}",
            "{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"c1\",\"function\":{\"name\":\"read\",\"arguments\":\"{\\\"pa\"}}]}}]}",
            "{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"th\\\":1}\"}}]}}]}",
            "{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}],\"timings\":{\"cache_n\":880,\"prompt_n\":100,\"prompt_ms\":500,\"predicted_n\":20,\"predicted_ms\":1000}}"));
        using var provider = new OpenAiProvider(new OpenAiSettings("http://host/v1", "m", null), server);

        var deltas = await Stream(provider);

        Assert.Equal(
        [
            new ReasoningDelta("hm"),
            new ReasoningDelta("m"),
            new TextDelta("Hello"),
            new ToolCallDelta(0, "c1", "read", "{\"pa"),
            new ToolCallDelta(0, null, null, "th\":1}"),
            new StatsDelta(new RoundStats(100, 20, 200, 20) { CachedTokens = 880 }),
            new DoneDelta("tool_calls"),
        ], deltas);
    }

    [Fact]
    public async Task ReportsServerErrors()
    {
        var failing = new Server((_, _) => Json("{\"error\":{\"message\":\"model not loaded\"}}", HttpStatusCode.ServiceUnavailable));
        using var provider = new OpenAiProvider(new OpenAiSettings("http://host/v1", "m", null), failing);
        var e = await Assert.ThrowsAsync<OpenAiException>(() => Stream(provider));
        Assert.Contains("model not loaded", e.Message);

        var midStream = new Server((_, _) => Sse("{\"choices\":[{\"delta\":{\"content\":\"a\"}}]}", "{\"error\":{\"message\":\"overloaded\"}}"));
        using var streaming = new OpenAiProvider(new OpenAiSettings("http://host/v1", "m", null), midStream);
        e = await Assert.ThrowsAsync<OpenAiException>(() => Stream(streaming));
        Assert.Equal("overloaded", e.Message);
    }

    [Fact]
    public async Task ToleratesOddButValidChunks()
    {
        var server = new Server((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                ": keep-alive\r\n\r\n" +
                "data:\r\n\r\n" +
                "data: {\"error\":null,\"choices\":[{\"delta\":{\"content\":\"a\",\"tool_calls\":[null]}}]}\r\n\r\n" +
                "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":1}}\r\n\r\n" +
                "data: [DONE]\r\n\r\n"),
        });
        using var provider = new OpenAiProvider(new OpenAiSettings("http://host/v1", "m", null), server);

        Assert.Equal([new TextDelta("a"), new DoneDelta(null)], await Stream(provider));
    }

    [Fact]
    public async Task RejectsWhatIsNotAnEventStream()
    {
        using var html = new OpenAiProvider(new OpenAiSettings("http://host", "m", null),
            new Server((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>login</html>") }));
        var e = await Assert.ThrowsAsync<OpenAiException>(() => Stream(html));
        Assert.Contains("<html>login</html>", e.Message);

        using var garbled = new OpenAiProvider(new OpenAiSettings("http://host/v1", "m", null),
            new Server((_, _) => Sse("{\"choices\":[{\"delta\":{\"content\":\"a\"}}]}", "not json")));
        e = await Assert.ThrowsAsync<OpenAiException>(() => Stream(garbled));
        Assert.Contains("not json", e.Message);

        using var oddList = new OpenAiProvider(new OpenAiSettings("http://host/v1", null, null),
            new Server((_, _) => Json("{\"data\":[\"m1\"]}")));
        e = await Assert.ThrowsAsync<OpenAiException>(() => Stream(oddList));
        Assert.Equal("The server lists no models.", e.Message);
    }

    [Fact]
    public async Task RejectsAMissingUrl()
    {
        using var provider = new OpenAiProvider(new OpenAiSettings("", "m", null), new Server((_, _) => throw new InvalidOperationException()));
        var e = await Assert.ThrowsAsync<OpenAiException>(() => Stream(provider));
        Assert.Equal("No server URL is configured.", e.Message);
    }

    [Fact]
    public async Task StopKeepsWhatArrived()
    {
        using var cancel = new CancellationTokenSource();
        var pipe = new System.IO.Pipelines.Pipe();
        var server = new Server((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(pipe.Reader.AsStream()) });
        using var provider = new OpenAiProvider(new OpenAiSettings("http://host/v1", "m", null), server);
        var deltas = new List<StreamDelta>();

        var round = provider.StreamRoundAsync(Request, d =>
        {
            deltas.Add(d);
            cancel.Cancel();
        }, cancel.Token);
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("data: {\"choices\":[{\"delta\":{\"content\":\"part\"}}]}\n\n"));
        await round;

        Assert.Equal([new TextDelta("part")], deltas);
    }
}
