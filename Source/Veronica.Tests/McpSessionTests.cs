using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Mcp;
using Veronica.Agent.Tools;
using Xunit;

namespace Veronica.Tests;

public sealed class McpSessionTests
{
    private static readonly Skill Build = new("build", "d", "the steps", ["first", "second"]);

    private sealed class Server
    {
        public Server(bool withSessions = true)
        {
            Scope = new SessionScope(() => new SessionState([Build], []));
            Sessions = new McpClientSessions(Scope, state => $"RULES {state.Skills.Count}");
            var tools = new ToolSet([
                new ActivateSkillTool(() => Scope.Current.Skills, () => Scope.Current.Plan),
                new MarkCompletedTool(() => Scope.Current.Plan),
            ]);
            Protocol = new McpProtocol("veronica", tools.Definitions,
                (id, name, arguments, stop) => Sessions.RunAsync<ToolResult?>(id, async () => await tools.ExecuteAsync(new ToolCall("c", name, arguments), stop)),
                withSessions ? Sessions : null);
        }

        public SessionScope Scope { get; }

        public McpClientSessions Sessions { get; }

        public McpProtocol Protocol { get; }

        public Task<McpHttpResponse> Post(string? session, string method, string parameters = "{}") =>
            Protocol.HandleAsync(new McpHttpRequest("POST", "/mcp", session, $"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"{method}\",\"params\":{parameters}}}"), CancellationToken.None);

        public async Task<string> Open() => (await Post(null, "initialize")).SessionId!;

        public async Task<string> Call(string? session, string tool, string arguments = "{}")
        {
            var response = await Post(session, "tools/call", $"{{\"name\":\"{tool}\",\"arguments\":{arguments}}}");
            return JsonDocument.Parse(response.Body!).RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
        }
    }

    [Fact]
    public async Task Initialize_opens_a_session_with_its_instructions()
    {
        var server = new Server();

        var response = await server.Post(null, "initialize");

        Assert.NotNull(response.SessionId);
        Assert.Equal("RULES 1", JsonDocument.Parse(response.Body!).RootElement.GetProperty("result").GetProperty("instructions").GetString());
        Assert.Equal(1, server.Sessions.OpenCount);
    }

    [Fact]
    public async Task Without_sessions_initialize_gives_no_id_and_no_instructions()
    {
        var response = await new Server(withSessions: false).Post(null, "initialize");

        Assert.Null(response.SessionId);
        Assert.False(JsonDocument.Parse(response.Body!).RootElement.GetProperty("result").TryGetProperty("instructions", out _));
    }

    [Fact]
    public async Task Each_session_works_on_its_own_plan()
    {
        var server = new Server();
        string a = await server.Open(), b = await server.Open();

        await server.Call(a, "activate_skill", """{"name":"build"}""");

        Assert.Contains("No skill is active", await server.Call(b, "mark_completed", """{"proof":"p"}"""));
        Assert.Contains("second", await server.Call(a, "mark_completed", """{"proof":"p"}"""));
        Assert.Null(server.Scope.Chat.Plan.ActiveSkill);
    }

    [Fact]
    public async Task A_call_without_a_session_id_works_on_the_chat_state()
    {
        var server = new Server();

        await server.Call(null, "activate_skill", """{"name":"build"}""");

        Assert.Equal("build", server.Scope.Chat.Plan.ActiveSkill);
    }

    [Fact]
    public async Task Delete_closes_the_session_and_its_id_is_then_unknown()
    {
        var server = new Server();
        string id = await server.Open();

        var deleted = await server.Protocol.HandleAsync(new McpHttpRequest("DELETE", "/mcp", id, ""), CancellationToken.None);

        Assert.Equal(200, deleted.Status);
        Assert.Equal(0, server.Sessions.OpenCount);
        Assert.Equal(404, (await server.Post(id, "tools/list")).Status);
    }

    [Fact]
    public async Task An_unknown_session_id_is_not_found()
    {
        var response = await new Server().Post("gone", "tools/list");

        Assert.Equal(404, response.Status);
        Assert.Equal(-32001, JsonDocument.Parse(response.Body!).RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Initialize_with_an_old_session_id_opens_a_new_session()
    {
        var response = await new Server().Post("gone", "initialize");

        Assert.Equal(200, response.Status);
        Assert.NotNull(response.SessionId);
    }

    [Fact]
    public void Renew_gives_the_chat_a_new_state()
    {
        int loads = 0;
        var scope = new SessionScope(() => { loads++; return new SessionState([Build], []); });
        var first = scope.Chat;
        first.Plan.Activate(Build);

        var renewed = scope.Renew();

        Assert.NotSame(first, renewed);
        Assert.Same(renewed, scope.Current);
        Assert.Null(renewed.Plan.ActiveSkill);
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task An_entered_session_does_not_leak_to_the_caller()
    {
        var scope = new SessionScope(() => new SessionState([], []));
        var entered = scope.Create();

        var seen = await scope.RunInAsync(entered, () => Task.FromResult(scope.Current));

        Assert.Same(entered, seen);
        Assert.Same(scope.Chat, scope.Current);
    }

    [Fact]
    public async Task Read_instructions_returns_the_prompt_and_the_hint_names_it()
    {
        var result = await new ReadInstructionsTool(() => "PROMPT").ExecuteAsync(JsonDocument.Parse("{}").RootElement, CancellationToken.None);

        Assert.Equal("PROMPT", result.Output);
        Assert.Contains(ReadInstructionsTool.ToolName, McpConnection.Hint);
    }

    [Theory]
    [InlineData(null, McpEndpoint.DefaultPort, false)]
    [InlineData(" 5000 ", 5000, false)]
    [InlineData("abc", McpEndpoint.DefaultPort, true)]
    [InlineData("70000", McpEndpoint.DefaultPort, true)]
    public void Port_comes_from_the_setting(string? setting, int port, bool problem)
    {
        Assert.Equal(port, McpEndpoint.PortFrom(setting, out var said));
        Assert.Equal(problem, said != null);
    }
}
