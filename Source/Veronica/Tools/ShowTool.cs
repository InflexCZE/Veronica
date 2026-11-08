using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;

namespace Veronica.Tools;

// What a click and double-click on the item do: select it in the Project Explorer and the Property Inspector,
// then open it in the Render View.
public sealed class ShowTool : ITool
{
    public string Name => "show";

    public string Description => "Shows a content item to the user: selects it in the Project Explorer and Property Inspector, and opens it in the Render View when it can be previewed.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the item, or its file name if unique; for a block, any of its prefab files"}},"required":["target"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        return new ToolResult(await EditorProject.RunAsync(project => Explorer.Show(Content.Resolve(project, target)), cancellationToken))
        {
            Summary = $"Showed {Gist.Item(target)} in the Editor",
        };
    }

    public string Attempt(JsonElement arguments) => $"show {Gist.Item(Args.Text(arguments, "target"))}";
}
