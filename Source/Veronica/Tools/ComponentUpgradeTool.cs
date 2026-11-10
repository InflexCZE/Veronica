using System.Text.Json;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Utils;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ComponentUpgradeTool : ITool
{
    public string Name => "component_upgrade";

    public string Description =>
        "Changes a block's component to a more specific kind of it, such as a FunctionalBlockComponent to a PowerableBlockComponent so the block draws power, and saves. " +
        "The block keeps every value it had; the new kind's own values come from the base, so pick the base closest to the block's new function. " +
        "The component gets a new definition in the block's folder, a copy of the base's with the block's values. The reply names the values the base brought.";

    public string Parameters => """
        {"type":"object","properties":{"prefab":{"type":"string","description":"The block: its prefab binding, or any of its prefabs"},"component":{"type":"string","description":"The component's name as read lists it, e.g. FunctionalBlockComponent"},"base":{"type":"string","description":"Where the new kind comes from: a template path from find_template (e.g. Thrusters), a block, or a definition, by name or path"}},"required":["prefab","component","base"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string prefab = Args.Text(arguments, "prefab");
        string component = Args.Text(arguments, "component");
        string baseQuery = Args.Text(arguments, "base");

        var plan = await EditorProject.RunAsync(project => Block.Open(project, prefab).PlanUpgrade(component, baseQuery), cancellationToken);
        string? file = plan.BaseDefinition is { } id
            ? await EditorProject.RunAsync(project => project.GetAssetFileByGuid(id) is ExplorerAssetFile source
                ? CreatePartialTool.CreateCopyAsync(project, source, plan.Folder, plan.DefinitionName)
                : throw new EditError($"{plan.Base}'s definition is missing from the project."), cancellationToken)
            : null;

        string reply;
        try
        {
            reply = await EditorProject.RunAsync(project => Block.Open(project, prefab).Upgrade(plan, file), cancellationToken);
        }
        catch (EditError error) when (file != null)
        {
            throw new EditError($"{error.Message} The new definition {file} in {Content.Shown(plan.Folder)} was created and is not used.");
        }
        return new ToolResult(reply)
        {
            Summary = $"Upgraded {Gist.Clip(component, 40)} on {Gist.Item(prefab)} to {plan.NewType.Name}",
        };
    }

    public string Attempt(JsonElement arguments) => $"upgrade {Gist.Clip(Args.Text(arguments, "component"), 40)} on {Gist.Item(Args.Text(arguments, "prefab"))}";
}
