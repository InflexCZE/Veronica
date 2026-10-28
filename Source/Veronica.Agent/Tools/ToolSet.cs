using System.Text.Json;

namespace Veronica.Agent.Tools;

public interface ITool
{
    string Name { get; }

    string Description { get; }

    // JSON Schema object, as JSON text.
    string Parameters { get; }

    Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}

// The tools of a conversation. Every failure becomes a result the model can read and act on.
public sealed class ToolSet
{
    private readonly Dictionary<string, ITool> _tools;

    // One tool at a time, whoever calls: a chat turn, an MCP client, or both at once.
    private readonly SemaphoreSlim _running = new(1, 1);

    public ToolSet(IEnumerable<ITool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        Definitions = _tools.Values.Select(t => new ToolDefinition(t.Name, t.Description, t.Parameters)).ToList();
    }

    public IReadOnlyList<ToolDefinition> Definitions { get; }

    public async Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken)
    {
        if (call.Name.Length == 0)
            return new ToolResult($"Your tool call could not be parsed: {call.Arguments}\nWrite the call again in the tool call format.");

        if (!_tools.TryGetValue(call.Name, out var tool))
            return new ToolResult($"There is no tool named {call.Name}. The tools are: {string.Join(", ", _tools.Keys)}.");

        JsonElement arguments;
        try
        {
            using var document = JsonDocument.Parse(call.Arguments.Length == 0 ? "{}" : call.Arguments);
            arguments = document.RootElement.Clone();
        }
        catch (JsonException e)
        {
            return new ToolResult($"The arguments of {call.Name} are not valid JSON ({e.Message}). Write the call again.");
        }
        if (arguments.ValueKind != JsonValueKind.Object)
            return new ToolResult($"The arguments of {call.Name} must be a JSON object. Write the call again.");

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
            return new ToolResult($"{call.Name} failed: {e.Message}");
        }
        finally
        {
            _running.Release();
        }
    }
}
