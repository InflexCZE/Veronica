using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ComponentRemoveTool : ITool
{
    public string Name => "component_remove";

    public string Description => "Removes a component from a block, on every side it is on, and saves. Refused while another component depends on it.";

    public string Parameters => """
        {"type":"object","properties":{"prefab":{"type":"string","description":"The block: its prefab binding, or any of its prefabs"},"component":{"type":"string","description":"The component's name as read lists it, e.g. ThrusterComponent"}},"required":["prefab","component"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string prefab = Args.Text(arguments, "prefab");
        string component = Args.Text(arguments, "component");
        return new ToolResult(await EditorProject.RunAsync(project => Block.Open(project, prefab).Remove(component), cancellationToken));
    }
}
