using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class FindTemplateTool : ITool
{
    public string Name => "find_template";

    public string Description => "Lists the prefab templates create_prefab can build from, as the Editor's Create Prefab menu shows them.";

    public string Parameters => """
        {"type":"object","properties":{"filter":{"type":"string","description":"Words that must all appear in the template path, e.g. ion thrusters; empty lists all"}}}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string? filter = Args.OptionalText(arguments, "filter");
        string matching = filter is { Length: > 0 } ? $" matching \"{Gist.Clip(filter, 40)}\"" : "";
        return await EditorProject.RunAsync(project =>
        {
            var found = Templates.Filter(PrefabTemplates.List(project).Keys, filter);
            if (found.Count == 0)
                return new ToolResult($"No template matches '{filter}'.") { Summary = $"Found no prefab template{matching}" };
            var lines = found.Take(Templates.MaxListed).ToList();
            if (found.Count > Templates.MaxListed)
                lines.Add($"... {found.Count - Templates.MaxListed} more; add words to the filter.");
            return new ToolResult(string.Join("\n", lines)) { Summary = $"Found {Gist.Count(found.Count, "prefab template", "prefab templates")}{matching}" };
        }, cancellationToken);
    }

    public string Attempt(JsonElement arguments) => "list prefab templates";
}
