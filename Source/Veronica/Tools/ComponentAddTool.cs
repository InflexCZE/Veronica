using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ComponentAddTool : ITool
{
    public string Name => "component_add";

    public string Description =>
        "Adds a component to a block, on the client, the server or both as its type requires, together with the components it depends on, and saves. " +
        "The reply names what was added and any component still needing a definition.";

    public string Parameters => """
        {"type":"object","properties":{"prefab":{"type":"string","description":"The block: its prefab binding, or any of its prefabs"},"type":{"type":"string","description":"Component type, e.g. ThrusterComponent"},"definition":{"type":"string","description":"Optional: an existing definition for the component to use, by name or path"}},"required":["prefab","type"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string prefab = Args.Text(arguments, "prefab");
        string type = Args.Text(arguments, "type");
        string? definition = Args.OptionalText(arguments, "definition");
        return new ToolResult(await EditorProject.RunAsync(project => Block.Open(project, prefab).Add(type, definition), cancellationToken))
        {
            Summary = $"Added {Gist.Clip(type, 40)} to {Gist.Item(prefab)}",
        };
    }

    public string Attempt(JsonElement arguments) => $"add {Gist.Clip(Args.Text(arguments, "type"), 40)} to {Gist.Item(Args.Text(arguments, "prefab"))}";
}
