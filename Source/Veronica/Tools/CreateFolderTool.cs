using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Tools;

namespace Veronica.Tools;

public sealed class CreateFolderTool : ITool
{
    public string Name => "create_folder";

    public string Description => "Creates a folder in the mod, with any missing parent folders. An existing folder is fine.";

    public string Parameters => """
        {"type":"object","properties":{"folder_path":{"type":"string","description":"Folder path inside the mod, e.g. Assets/MyThruster"}},"required":["folder_path"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string path = Args.Text(arguments, "folder_path");
        return new ToolResult(await EditorProject.RunAsync(project =>
        {
            var (folder, created) = Folders.Ensure(project, path);
            return created ? $"Created {Content.Shown(folder)}." : $"{Content.Shown(folder)} already exists.";
        }, cancellationToken));
    }
}
