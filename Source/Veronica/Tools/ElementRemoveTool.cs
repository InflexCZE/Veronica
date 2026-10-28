using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ElementRemoveTool : ITool
{
    public string Name => "element_remove";

    public string Description => "Removes one list element or dictionary entry and saves. List elements after it move up one index.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the definition, or its file name if unique"},"path":{"type":"string","description":"Path of the element as read prints it, e.g. Dummies[1] or ThrustClasses[Ion]"}},"required":["target","path"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        string path = Args.Text(arguments, "path");
        return new ToolResult(await EditorProject.RunAsync(project =>
        {
            var (asset, fields) = Content.Definition(project, target);
            Prefabs.RequireNotBinding(asset);
            Content.RequireWritable(asset);
            string reply = Elements.Remove(fields.Root, path);
            Content.Save(asset);
            return reply;
        }, cancellationToken));
    }
}
