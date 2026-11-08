using System.Text.Json;
using Keen.VRage.ContentPipeline.Project.Structure;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class FindContentTool : ITool
{
    private const int MaxResults = 50;

    public string Name => "find_content";

    public string Description => "Finds content items whose file name or definition type contains every word of query. A base type finds every definition derived from it, e.g. CubeBlockDefinition finds every block's definition. Searches the mod and the projects it depends on, such as Vanilla.";

    public string Parameters => """
        {"type":"object","properties":{"query":{"type":"string","description":"Words of a file name or definition type, e.g. ion thruster or ThrusterDefinition"},"folder":{"type":"string","description":"Only search inside this folder"}},"required":["query"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string query = Args.Text(arguments, "query");
        string? folder = Args.OptionalText(arguments, "folder");
        return await EditorProject.RunAsync(project => Find(project, query, folder), cancellationToken);
    }

    public string Attempt(JsonElement arguments) => $"search for \"{Gist.Clip(Args.Text(arguments, "query"), 40)}\"";

    private static ToolResult Find(LoadedProject project, string query, string? folder)
    {
        string scope = "";
        HashSet<LoadedProject>? inProjects = null;
        if (ContentNames.Normalize(folder ?? "").Length > 0)
        {
            var found = Folders.Existing(project, folder!);
            if (found.Count == 0)
                throw new EditError(Folders.NotFound(project, folder!, "see list_folder"));
            string root = ContentNames.Normalize(found[0].Folder.ProjectRelativePath);
            scope = root.Length == 0 ? "" : root + "\\";
            inProjects = found.Select(f => f.Owner).ToHashSet();
        }
        var order = Content.ProjectOrder(project);
        var matches = project.GetAvailableFiles().OfType<ExplorerAssetFile>()
            .Where(f => inProjects == null || inProjects.Contains(f.Project))
            .Where(f => scope.Length == 0 || ContentNames.Normalize(f.ProjectRelativePath).StartsWith(scope, StringComparison.OrdinalIgnoreCase))
            .Where(f => ContentNames.QueryMatches(query, f.Name, Content.DefinitionTypeNames(f)))
            .OrderBy(f => order.GetValueOrDefault(f.Project, int.MaxValue))
            .ThenBy(f => f.ProjectRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string where = scope.Length == 0 ? "" : $" in {Gist.Item(scope)}";
        string quoted = $"\"{Gist.Clip(query, 40)}\"";
        if (matches.Count == 0)
            return new ToolResult(scope.Length == 0 ? $"Nothing matches '{query}'." : $"Nothing under '{scope.TrimEnd('\\')}' matches '{query}'.")
            {
                Summary = $"Found nothing matching {quoted}{where}",
            };

        var lines = Content.ByProject(project, matches.Take(MaxResults));
        if (matches.Count > MaxResults)
            lines.Add($"... {matches.Count - MaxResults} more; narrow the query or pass folder.");
        return new ToolResult(string.Join("\n", lines)) { Summary = $"Found {Gist.Count(matches.Count, "item", "items")} matching {quoted}{where}" };
    }
}
