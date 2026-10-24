using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Xunit;

namespace Veronica.Tests;

public sealed class ToolSetTests
{
    private static readonly ToolSet Tools = new([new AddTool(), new Throwing()]);

    private static Task<ToolResult> Run(string name, string arguments) =>
        Tools.ExecuteAsync(new ToolCall("c1", name, arguments), CancellationToken.None);

    [Theory]
    [InlineData("{\"a\":2,\"b\":3}", "5")]
    [InlineData("{\"a\":1.5,\"b\":-0.25}", "1.25")]
    [InlineData("{\"a\":\"1234\",\"b\":5678}", "6912")]
    public async Task Add_sums(string arguments, string sum) => Assert.Equal(sum, (await Run("add", arguments)).Output);

    [Fact]
    public async Task Add_without_numbers_says_what_it_needs() =>
        Assert.Contains("two numbers", (await Run("add", "{\"a\":1}")).Output);

    [Fact]
    public async Task Unparsed_call_asks_for_it_again()
    {
        var result = await Run("", "<tool_call>add 1 2");
        Assert.Contains("<tool_call>add 1 2", result.Output);
        Assert.Contains("again", result.Output);
    }

    [Fact]
    public async Task Unknown_tool_lists_the_tools() =>
        Assert.Contains("add", (await Run("subtract", "{}")).Output);

    [Fact]
    public async Task Bad_json_is_a_result_not_an_exception() =>
        Assert.Contains("not valid JSON", (await Run("add", "{\"a\":")).Output);

    [Fact]
    public async Task Non_object_arguments_are_rejected() =>
        Assert.Contains("JSON object", (await Run("add", "[1,2]")).Output);

    [Fact]
    public async Task Tool_failure_becomes_a_result() =>
        Assert.Equal("boom failed: nope", (await Run("boom", "{}")).Output);

    [Fact]
    public void Definitions_carry_the_schema()
    {
        var add = Assert.Single(Tools.Definitions, d => d.Name == "add");
        using var schema = JsonDocument.Parse(add.Parameters);
        Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
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
