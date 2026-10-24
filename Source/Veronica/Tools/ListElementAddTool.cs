using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ListElementAddTool : ITool
{
    public string Name => "list_element_add";

    public string Description => "Appends a default element to a list field, saves, and returns the new element's path and fields. Set them with write.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the definition, or its file name if unique"},"list_path":{"type":"string","description":"Path of the list field, e.g. Dummies"},"type":{"type":"string","description":"Only for lists of an abstract type: the element type to create"}},"required":["target","list_path"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        string listPath = Args.Text(arguments, "list_path");
        string? type = Args.OptionalText(arguments, "type");
        return new ToolResult(await EditorProject.RunAsync(project =>
        {
            var (asset, fields) = Content.Definition(project, target);
            Content.RequireWritable(asset);
            string reply = ListAppender.Append(fields.Root, listPath, type, new ProjectLinks(project, asset));
            Content.Save(asset);
            return reply;
        }, cancellationToken));
    }
}
