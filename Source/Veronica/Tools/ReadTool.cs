using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ReadTool : ITool
{
    public string Name => "read";

    public string Description =>
        "Shows a definition's fields as 'Path = value (type)' lines; references show the path of the item they point at. Long output is cut; read a narrower path for the rest. " +
        "A block (its prefab binding, or any of its prefabs) reads as its component list; a component's fields are at Components[<name>].";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the definition, or its file name if unique"},"path":{"type":"string","description":"Field path as read prints it, e.g. Dummies[0].Scale; empty for the whole definition"}},"required":["target"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        string path = Args.OptionalText(arguments, "path") ?? "";
        return new ToolResult(await EditorProject.RunAsync(project =>
        {
            var resolved = Content.Resolve(project, target);
            if (Block.TryOpen(project, resolved) is { } block)
                return block.Read(path);
            var (asset, fields) = Content.Definition(resolved);
            return $"{Content.Describe(asset)}:\n{FieldReader.Read(fields.Root, path, new ProjectLinks(project, asset))}";
        }, cancellationToken));
    }
}
