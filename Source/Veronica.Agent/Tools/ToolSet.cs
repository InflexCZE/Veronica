using System.Text.Json;

namespace Veronica.Agent.Tools;

public interface ITool
{
    string Name { get; }

    string Description { get; }

    // JSON Schema object, as JSON text.
    string Parameters { get; }

    Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);

    // False for a tool that may wait on the user: holding the queue would stall every other caller.
    bool Exclusive => true;
}

public static class ToolArgs
{
    // Empty when missing or not text.
    public static string Text(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : "";
}

// The tools of a conversation. Every failure becomes a result the model can read and act on.
public sealed class ToolSet
{
    private readonly Dictionary<string, ITool> _tools;

    // One tool at a time, whoever calls: a chat turn, an MCP client, or both at once.
    private readonly SemaphoreSlim _running;

    public ToolSet(IEnumerable<ITool> tools)
        : this(tools, new SemaphoreSlim(1, 1))
    {
    }

    private ToolSet(IEnumerable<ITool> tools, SemaphoreSlim running)
    {
        _running = running;
        _tools = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        Definitions = _tools.Values.Select(t => new ToolDefinition(t.Name, t.Description, t.Parameters)).ToList();
    }

    // The same tools but one, still run one at a time with this set's.
    public ToolSet Without(string name) => new(_tools.Values.Where(t => t.Name != name), _running);

    // The same tools with one added or replacing the one of its name, still run one at a time with this set's.
    public ToolSet With(ITool tool) => new(_tools.Values.Where(t => t.Name != tool.Name).Append(tool), _running);

    public IReadOnlyList<ToolDefinition> Definitions { get; }

    public async Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken)
    {
        if (call.Name.Length == 0)
            return new ToolResult($"Your tool call could not be parsed: {call.Arguments}\nWrite the call again in the tool call format.", Refused: true);

        if (!_tools.TryGetValue(call.Name, out var tool))
            return new ToolResult($"There is no tool named {call.Name}. The tools are: {string.Join(", ", _tools.Keys)}.", Refused: true);

        JsonElement arguments;
        try
        {
            using var document = JsonDocument.Parse(call.Arguments.Length == 0 ? "{}" : call.Arguments);
            arguments = document.RootElement.Clone();
        }
        catch (JsonException e)
        {
            return new ToolResult($"The arguments of {call.Name} are not valid JSON ({e.Message}). Write the call again.", Refused: true);
        }
        if (arguments.ValueKind != JsonValueKind.Object)
            return new ToolResult($"The arguments of {call.Name} must be a JSON object. Write the call again.", Refused: true);

        bool exclusive = tool.Exclusive;
        if (exclusive)
            await _running.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await tool.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new ToolResult($"{call.Name} failed: {e.Message}", Refused: true);
        }
        finally
        {
            if (exclusive)
                _running.Release();
        }
    }
}
