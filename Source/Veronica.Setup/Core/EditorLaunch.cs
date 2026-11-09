namespace Veronica.Setup.Core;

// Starting the Editor on a mod (Docs/Tech/Distribution.md): the project, the GameData folders that hold the game's
// and the engine's projects a mod depends on, and the game when one is known.
public static class EditorLaunch
{
    public const string EditorExeName = "VRage.Editor.exe";

    public const string GameExeName = "SpaceEngineers2.exe";

    // FromSdk: the Editor is the Mod SDK's, so its game is the Steam one. DevGameExe: the game a development
    // checkout built in the Editor's configuration, when it is there.
    public sealed record Layout(IReadOnlyList<string> GlobalProjectDirs, bool FromSdk, string? DevGameExe);

    // Null when the Editor sits in neither a Mod SDK nor a development checkout.
    public static Layout? Find(string editorExe, Func<string, bool> directoryExists, Func<string, bool> fileExists)
    {
        string editorDir = Path.GetDirectoryName(editorExe)!;
        if (Path.GetDirectoryName(editorDir) is { } sdk
            && string.Equals(Path.GetFileName(editorDir), "Editor", StringComparison.OrdinalIgnoreCase)
            && directoryExists(Path.Combine(sdk, "GameData", "Vanilla")))
        {
            // The same pair the retail HUB passes, though the SDK keeps Engine inside GameData and has no VRage\GameData.
            return new Layout([Path.Combine(sdk, "GameData"), Path.Combine(sdk, "VRage", "GameData")], FromSdk: true, DevGameExe: null);
        }
        string configuration = Path.GetFileName(editorDir);
        for (string? folder = Path.GetDirectoryName(editorDir); folder != null; folder = Path.GetDirectoryName(folder))
        {
            string[] dirs = [Path.Combine(folder, "Game2", "GameData"), Path.Combine(folder, "VRage", "GameData")];
            if (!dirs.All(directoryExists))
                continue;
            string game = Path.Combine(folder, "Game2", "Sources", "Game2", "bin", "x64", configuration, GameExeName);
            return new Layout(dirs, FromSdk: false, fileExists(game) ? game : null);
        }
        return null;
    }

    // --globalAssemblyDirs: the SDK ships the game's assemblies Vanilla references (Game2.Client.dll...) beside the
    // Editor, where none of Vanilla's own search paths look; without it the Editor fails to start.
    public static IReadOnlyList<string> Arguments(string project, string editorExe, Layout layout, string? gameExe) =>
    [
        "--project", project,
        "--globalProjectDirs", .. layout.GlobalProjectDirs,
        "--globalAssemblyDirs", Path.GetDirectoryName(editorExe)!,
        .. gameExe == null ? (string[])[] : ["--projectExecutableSearchPaths", Path.GetDirectoryName(gameExe)!],
    ];

    // Like the retail HUB, the Editor runs in the game's folder when there is a game.
    public static string WorkingDirectory(string editorExe, string? gameExe) => Path.GetDirectoryName(gameExe ?? editorExe)!;
}
