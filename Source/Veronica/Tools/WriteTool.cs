using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class WriteTool : ITool
{
    public string Name => "write";

    public string Description => "Sets one field of a definition and saves it. Vanilla content is read-only.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the definition, or its file name if unique"},"path":{"type":"string","description":"Field path as read prints it, e.g. Dummies[0].Name"},"value":{"description":"Number, true/false, text or enum name. A reference field takes the name or path of the item to point at. A struct read prints on one line takes the same form, e.g. {X: 0.1, Y: 0.1, Z: 0.1}. null resets the field."}},"required":["target","path","value"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        string path = Args.Text(arguments, "path");
        var value = Args.Value(arguments, "value");
        return new ToolResult(await EditorProject.RunAsync(project =>
        {
            var (asset, fields) = Content.Definition(project, target);
            Content.RequireWritable(asset);
            string reply = FieldWriter.Write(fields.Root, path, value, new ProjectLinks(project, asset));
            Content.Save(asset);
            return reply;
        }, cancellationToken));
    }
}
