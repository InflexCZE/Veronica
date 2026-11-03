using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ElementAddTool : ITool
{
    public string Name => "element_add";

    public string Description => "Adds an element and saves. A list gets a default element at the end, a dictionary one under key; the reply shows its path and fields, set them with write, or pass value to write a single-value element at once. A set gets the element named by key (or value), e.g. an item's name.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the definition, or its file name if unique"},"path":{"type":"string","description":"Path of the list, dictionary or set field, e.g. Dummies"},"key":{"type":"string","description":"For dictionaries and keyed lists: the new element's key. For sets: the element to add"},"value":{"type":"string","description":"Optional: the value of a single-value list or dictionary element, written right away. For sets it may name the element instead of key"},"type":{"type":"string","description":"Only for collections of an abstract type: the element type to create"}},"required":["target","path"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        string path = Args.Text(arguments, "path");
        string? key = Args.OptionalText(arguments, "key");
        string? type = Args.OptionalText(arguments, "type");
        string? value = Args.OptionalText(arguments, "value");
        return new ToolResult(await EditorProject.RunAsync(project =>
        {
            var resolved = Content.Resolve(project, target);
            if (Block.TryOpen(project, resolved) is { } block)
                return block.ElementAdd(path, key, type, value);
            var (asset, fields) = Content.Definition(resolved);
            Content.RequireWritable(asset);
            string reply = Elements.Add(fields.Root, path, key, type, new ProjectLinks(project, asset), value: value);
            Content.Save(asset);
            return reply;
        }, cancellationToken));
    }
}
