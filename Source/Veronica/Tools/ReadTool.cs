using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ReadTool : ITool
{
    public string Name => "read";

    public string Description => "Shows a definition's fields as 'Path = value (type)' lines; references show the path of the item they point at. Long output is cut; read a narrower path for the rest.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the definition, or its file name if unique"},"path":{"type":"string","description":"Field path as read prints it, e.g. Dummies[0].Scale; empty for the whole definition"}},"required":["target"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        string path = Args.OptionalText(arguments, "path") ?? "";
        return new ToolResult(await EditorProject.RunAsync(project =>
        {
            var (asset, fields) = Content.Definition(project, target);
            if (path.Length == 0 && Prefabs.IsBinding(asset))
                return $"{Content.Describe(asset)}:\nA prefab binding, with no fields of its own; {Prefabs.Definitions(asset)}";
            Prefabs.RequireNotBinding(asset);
            return $"{Content.Describe(asset)}:\n{FieldReader.Read(fields.Root, path, new ProjectLinks(project, asset))}";
        }, cancellationToken));
    }
}
