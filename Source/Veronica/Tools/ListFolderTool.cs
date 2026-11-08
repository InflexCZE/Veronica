using System.Text.Json;
using Keen.VRage.ContentPipeline.Project.Structure;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ListFolderTool : ITool
{
    private const int MaxLines = 100;

    public string Name => "list_folder";

    public string Description => "Lists a folder's direct children. A folder that exists in several projects, such as the mod and Vanilla, is listed once per project.";

    public string Parameters => """
        {"type":"object","properties":{"folder_path":{"type":"string","description":"Project-relative folder; empty for the project roots"}}}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string folder = Args.OptionalText(arguments, "folder_path") ?? "";
        return await EditorProject.RunAsync(project => List(project, folder), cancellationToken);
    }

    public string Attempt(JsonElement arguments) =>
        Args.OptionalText(arguments, "folder_path") is { Length: > 0 } folder ? $"list {Gist.Clip(folder, 60)}" : "list the project roots";

    private static ToolResult List(LoadedProject project, string folderPath)
    {
        var found = Folders.Existing(project, folderPath);
        if (found.Count == 0)
            throw new EditError(Folders.NotFound(project, folderPath, "list a parent folder to see what exists"));

        var lines = new List<string>();
        int items = 0;
        foreach (var (owner, folder) in found)
        {
            items += folder.ChildItems.Count;
            lines.Add($"{Content.Shown(folder)} in {owner.Name}{(owner.IsReadOnly ? " (read-only)" : "")}:");
            var children = folder.ChildItems
                .OrderByDescending(c => c is ExplorerFolder)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (children.Count == 0)
                lines.Add("  (empty)");
            foreach (var child in children)
            {
                if (child is ExplorerFolder sub)
                    lines.Add($"  {child.Name}\\ ({sub.ChildItems.Count} item{(sub.ChildItems.Count == 1 ? "" : "s")})");
                else
                    lines.Add(Content.DefinitionTypeName(child) is { } type ? $"  {child.Name} ({type})" : $"  {child.Name}");
            }
        }

        string listed = ContentNames.Normalize(folderPath).Length == 0 ? "the project roots" : Gist.Clip(folderPath, 60);
        string summary = $"Listed {listed} ({Gist.Count(items, "item", "items")})";
        if (lines.Count <= MaxLines)
            return new ToolResult(string.Join("\n", lines)) { Summary = summary };
        return new ToolResult(string.Join("\n", lines.Take(MaxLines)) + $"\n... {lines.Count - MaxLines} more lines; list a subfolder or use find_content with folder.") { Summary = summary };
    }
}
