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

    public string Description => "Finds content items whose file name or definition type contains every word of query. Searches the mod and the projects it depends on, such as Vanilla.";

    public string Parameters => """
        {"type":"object","properties":{"query":{"type":"string","description":"Words of a file name or definition type, e.g. ion thruster or ThrusterDefinition"},"folder":{"type":"string","description":"Only search inside this folder"}},"required":["query"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string query = Args.Text(arguments, "query");
        string? folder = Args.OptionalText(arguments, "folder");
        return new ToolResult(await EditorProject.RunAsync(project => Find(project, query, folder), cancellationToken));
    }

    private static string Find(LoadedProject project, string query, string? folder)
    {
        string root = ContentNames.Normalize(folder ?? "");
        string scope = root.Length == 0 ? "" : root + "\\";
        var matches = project.GetAvailableFiles().OfType<ExplorerAssetFile>()
            .Where(f => scope.Length == 0 || ContentNames.Normalize(f.ProjectRelativePath).StartsWith(scope, StringComparison.OrdinalIgnoreCase))
            .Where(f => ContentNames.QueryMatches(query, f.Name, Content.DefinitionTypeName(f)))
            .OrderBy(f => f.ProjectRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matches.Count == 0 && scope.Length > 0 && !project.EnumerateProjectDAG().Any(p => HasFolder(p, root)))
            throw new EditError($"No folder '{folder}' in the mod or its dependencies; see list_folder.");
        if (matches.Count == 0)
            return scope.Length == 0 ? $"Nothing matches '{query}'." : $"Nothing under '{folder}' matches '{query}'.";

        var lines = matches.Take(MaxResults).Select(Content.Describe).ToList();
        if (matches.Count > MaxResults)
            lines.Add($"... {matches.Count - MaxResults} more; narrow the query or pass folder.");
        return string.Join("\n", lines);
    }

    private static bool HasFolder(LoadedProject project, string path)
    {
        ExplorerFolder? folder = project.GetRootFolder();
        foreach (string segment in ContentNames.Segments(path))
            folder = folder?.GetItemByName(segment) as ExplorerFolder;
        return folder != null;
    }
}
