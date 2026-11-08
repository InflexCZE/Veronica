namespace Veronica.Setup.Core;

// Starting the Editor on a mod with as few arguments as it needs (Docs/Tech/Distribution.md): the project, and
// the GameData folders that hold the game's and the engine's projects, which a mod depends on.
public static class EditorLaunch
{
    private static readonly string[] GameDataFolders = [@"Game2\GameData", @"VRage\GameData"];

    // Shaped to a development Editor, which sits deep inside a checkout holding Game2\GameData and VRage\GameData
    // (Docs/TODOs/RetailEditorLaunch.md). Empty when no folder above the exe has both.
    public static IReadOnlyList<string> GlobalProjectDirs(string editorExe, Func<string, bool> directoryExists)
    {
        for (string? folder = Path.GetDirectoryName(editorExe); folder != null; folder = Path.GetDirectoryName(folder))
        {
            var dirs = GameDataFolders.Select(f => Path.Combine(folder, f)).ToList();
            if (dirs.All(directoryExists))
                return dirs;
        }
        return [];
    }

    public static IReadOnlyList<string> Arguments(string project, IReadOnlyList<string> globalProjectDirs) =>
        ["--project", project, "--globalProjectDirs", .. globalProjectDirs];
}
