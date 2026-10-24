using System.Globalization;
using System.Text.Json;

namespace Veronica.Agent.Tools;

// Proves the tool loop end to end; removed when the first Editor tool lands (Docs/TODOs/AddTool.md).
public sealed class AddTool : ITool
{
    public string Name => "add";

    public string Description => "Adds two numbers and returns the sum.";

    public string Parameters => """
        {"type":"object","properties":{"a":{"type":"number","description":"First addend"},"b":{"type":"number","description":"Second addend"}},"required":["a","b"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!TryNumber(arguments, "a", out double a) || !TryNumber(arguments, "b", out double b))
            return Task.FromResult(new ToolResult("add needs two numbers, a and b."));
        return Task.FromResult(new ToolResult((a + b).ToString(CultureInfo.InvariantCulture)));
    }

    private static bool TryNumber(JsonElement arguments, string name, out double value)
    {
        value = 0;
        if (!arguments.TryGetProperty(name, out var p))
            return false;
        return p.ValueKind switch
        {
            JsonValueKind.Number => p.TryGetDouble(out value),
            // Models sometimes quote numbers.
            JsonValueKind.String => double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
    }
}
