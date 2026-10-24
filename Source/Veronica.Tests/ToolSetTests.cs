using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Xunit;

namespace Veronica.Tests;

public sealed class ToolSetTests
{
    private static readonly ToolSet Tools = new([new Echo(), new Throwing()]);

    private static Task<ToolResult> Run(string name, string arguments) =>
        Tools.ExecuteAsync(new ToolCall("c1", name, arguments), CancellationToken.None);

    [Fact]
    public async Task Runs_the_named_tool_with_its_arguments() =>
        Assert.Equal("{\"text\":\"hi\"}", (await Run("echo", "{\"text\":\"hi\"}")).Output);

    [Fact]
    public async Task Empty_arguments_are_an_empty_object() =>
        Assert.Equal("{}", (await Run("echo", "")).Output);

    [Fact]
    public async Task Unparsed_call_asks_for_it_again()
    {
        var result = await Run("", "<tool_call>echo hi");
        Assert.Contains("<tool_call>echo hi", result.Output);
        Assert.Contains("again", result.Output);
    }

    [Fact]
    public async Task Unknown_tool_lists_the_tools() =>
        Assert.Contains("echo, boom", (await Run("shout", "{}")).Output);

    [Fact]
    public async Task Bad_json_is_a_result_not_an_exception() =>
        Assert.Contains("not valid JSON", (await Run("echo", "{\"a\":")).Output);

    [Fact]
    public async Task Non_object_arguments_are_rejected() =>
        Assert.Contains("JSON object", (await Run("echo", "[1,2]")).Output);

    [Fact]
    public async Task Tool_failure_becomes_a_result() =>
        Assert.Equal("boom failed: nope", (await Run("boom", "{}")).Output);

    [Fact]
    public void Definitions_carry_the_schema()
    {
        var echo = Assert.Single(Tools.Definitions, d => d.Name == "echo");
        using var schema = JsonDocument.Parse(echo.Parameters);
        Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
    }

    private sealed class Echo : ITool
    {
        public string Name => "echo";
        public string Description => "Returns its arguments.";
        public string Parameters => "{\"type\":\"object\",\"properties\":{\"text\":{\"type\":\"string\"}}}";
        public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
            Task.FromResult(new ToolResult(arguments.GetRawText()));
    }

    private sealed class Throwing : ITool
    {
        public string Name => "boom";
        public string Description => "Fails.";
        public string Parameters => "{\"type\":\"object\"}";
        public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("nope");
    }
}
