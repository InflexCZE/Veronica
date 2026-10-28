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
        return new ToolResult(await EditorProject.RunAsync(project => List(project, folder), cancellationToken));
    }

    private static string List(LoadedProject project, string folderPath)
    {
        var found = Folders.Existing(project, folderPath);
        if (found.Count == 0)
            throw new EditError(Folders.NotFound(project, folderPath, "list a parent folder to see what exists"));

        var lines = new List<string>();
        foreach (var (owner, folder) in found)
        {
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

        if (lines.Count <= MaxLines)
            return string.Join("\n", lines);
        return string.Join("\n", lines.Take(MaxLines)) + $"\n... {lines.Count - MaxLines} more lines; list a subfolder or use find_content with folder.";
    }
}
