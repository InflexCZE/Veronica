using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ElementAddTool : ITool
{
    public string Name => "element_add";

    public string Description => "Adds a default element to a list (at the end) or to a dictionary (under key), saves, and returns the new element's path and fields. Set them with write.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the definition, or its file name if unique"},"path":{"type":"string","description":"Path of the list or dictionary field, e.g. Dummies"},"key":{"type":"string","description":"Only for dictionaries and keyed lists: the new element's key"},"type":{"type":"string","description":"Only for collections of an abstract type: the element type to create"}},"required":["target","path"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        string path = Args.Text(arguments, "path");
        string? key = Args.OptionalText(arguments, "key");
        string? type = Args.OptionalText(arguments, "type");
        return new ToolResult(await EditorProject.RunAsync(project =>
        {
            var (asset, fields) = Content.Definition(project, target);
            Content.RequireWritable(asset);
            string reply = Elements.Add(fields.Root, path, key, type, new ProjectLinks(project, asset));
            Content.Save(asset);
            return reply;
        }, cancellationToken));
    }
}
