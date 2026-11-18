using Veronica.Setup.Core;
using Xunit;

namespace Veronica.Setup.Tests;

public class ModsTests
{
    private const string Runtime = @"C:\Tools\Veronica\Runtime";

    // A mod project as the Editor writes it, trimmed.
    private const string Project = "{\r\n  \"$Bundles\": {\r\n    \"System.Runtime\": \"1.0.0.0\",\r\n    \"VRage\": \"0.12.9.606\"\r\n  },\r\n  \"$Type\": \"VRage:Keen.VRage.Core.Project.VRageProjectData\",\r\n  \"$Value\": {\r\n    \"Name\": \"NOTaROTOR\",\r\n    \"Type\": \"Mod\",\r\n    \"EditorExtensionAssemblies\": [\r\n      \"Mine.dll\"\r\n    ],\r\n    \"EditorExtensionAssembliesSearchPaths\": [\r\n      \"..\\\\Plugins\"\r\n    ],\r\n    \"SkipContentMounting\": false\r\n  }\r\n}\r\n";

    [Fact]
    public void Hook_AddsTheAssemblyAndTheRuntimePath_KeepingTheModsOwnEntries()
    {
        string hooked = VrgProj.Hook(Project, Runtime);

        Assert.Equal(HookState.Hooked, VrgProj.State(hooked, Runtime));
        Assert.Contains("\"Mine.dll\",\r\n      \"Veronica.dll\"", hooked);
        Assert.Contains("\"..\\\\Plugins\",\r\n      \"C:\\\\Tools\\\\Veronica\\\\Runtime\"", hooked);
        Assert.Contains("\"$Bundles\"", hooked);
        Assert.EndsWith("}\r\n", hooked);
    }

    [Fact]
    public void Hook_Twice_ChangesNothing()
    {
        string once = VrgProj.Hook(Project, Runtime);

        Assert.Equal(once, VrgProj.Hook(once, Runtime));
    }

    [Fact]
    public void Unhook_RestoresTheProject()
    {
        Assert.Equal(Project, VrgProj.Unhook(VrgProj.Hook(Project, Runtime), Runtime));
        Assert.Equal(HookState.NotHooked, VrgProj.State(Project, Runtime));
    }

    [Fact]
    public void MovedInstall_IsAnotherBuild()
    {
        string old = VrgProj.Hook(Project, @"D:\Old\Runtime");

        Assert.Equal(HookState.Foreign, VrgProj.State(old, Runtime));
    }

    [Fact]
    public void ModsOwnAbsoluteRuntimePath_SurvivesHookAndUnhook()
    {
        string own = Project.Replace(@"..\\Plugins", @"D:\\Tools\\Runtime");

        string hooked = VrgProj.Hook(own, Runtime);
        string unhooked = VrgProj.Unhook(hooked, Runtime);

        Assert.Contains(@"D:\\Tools\\Runtime", hooked);
        Assert.Equal(own, unhooked);
    }

    [Fact]
    public void VeronicaFromARelativeFolder_IsForeign()
    {
        string dev = Project.Replace("\"Mine.dll\"", "\"Veronica.dll\"").Replace(@"..\\Plugins", @"..\\..\\Source\\Veronica\\bin\\Debug");

        Assert.Equal(HookState.Foreign, VrgProj.State(dev, Runtime));
    }

    [Fact]
    public void UnhookAny_RemovesAnotherBuildsEntries_KeepingTheModsOwnPaths()
    {
        const string devBuild = @"..\\..\\Source\\Veronica\\bin\\Debug";
        string dev = Project.Replace("\"Mine.dll\"", "\"Mine.dll\",\r\n      \"Veronica.dll\"")
            .Replace("\"..\\\\Plugins\"", "\"..\\\\Plugins\",\r\n      \"" + devBuild + "\"");

        string unhooked = VrgProj.UnhookAny(dev, Runtime, path => path == @"..\..\Source\Veronica\bin\Debug");

        Assert.Equal(Project, unhooked);
    }

    [Fact]
    public void UnhookAny_RemovesThisInstallsRuntime_WhenNoFolderHoldsVeronica()
    {
        Assert.Equal(Project, VrgProj.UnhookAny(VrgProj.Hook(Project, Runtime), Runtime, _ => false));
    }

    private const string DevEditor = @"X:\Repo\VRage\Sources\Editor\VRage.Editor\bin\x64\Debug\VRage.Editor.exe";

    private const string DevGame = @"X:\Repo\Game2\Sources\Game2\bin\x64\Debug\SpaceEngineers2.exe";

    private static readonly HashSet<string> DevFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        @"X:\Repo\Game2\GameData", @"X:\Repo\VRage\GameData", @"X:\Repo\VRage\Sources\Editor\GameData",
    };

    [Fact]
    public void EditorLaunch_FindsTheCheckoutsGameDataAndGameAboveADevEditor()
    {
        var layout = EditorLaunch.Find(DevEditor, DevFolders.Contains, f => f == DevGame)!;

        Assert.False(layout.FromSdk);
        Assert.Equal([@"X:\Repo\Game2\GameData", @"X:\Repo\VRage\GameData"], layout.GlobalProjectDirs);
        Assert.Equal(DevGame, layout.DevGameExe);
        Assert.Equal(["--project", @"Y:\Mods\A\A.vrgproj", "--globalProjectDirs", @"X:\Repo\Game2\GameData", @"X:\Repo\VRage\GameData",
                "--globalAssemblyDirs", @"X:\Repo\VRage\Sources\Editor\VRage.Editor\bin\x64\Debug",
                "--projectExecutableSearchPaths", @"X:\Repo\Game2\Sources\Game2\bin\x64\Debug"],
            EditorLaunch.Arguments(@"Y:\Mods\A\A.vrgproj", DevEditor, layout, layout.DevGameExe));
        Assert.Equal(@"X:\Repo\Game2\Sources\Game2\bin\x64\Debug", EditorLaunch.WorkingDirectory(DevEditor, layout.DevGameExe));
    }

    [Fact]
    public void EditorLaunch_DevEditorHasNoGame_WhenItsConfigurationIsNotBuilt()
    {
        var layout = EditorLaunch.Find(DevEditor, DevFolders.Contains, f => f == DevGame.Replace(@"\Debug\", @"\Release\"))!;

        Assert.Null(layout.DevGameExe);
        Assert.Equal(["--project", @"Y:\Mods\A\A.vrgproj", "--globalProjectDirs", @"X:\Repo\Game2\GameData", @"X:\Repo\VRage\GameData",
                "--globalAssemblyDirs", @"X:\Repo\VRage\Sources\Editor\VRage.Editor\bin\x64\Debug"],
            EditorLaunch.Arguments(@"Y:\Mods\A\A.vrgproj", DevEditor, layout, null));
        Assert.Equal(Path.GetDirectoryName(DevEditor), EditorLaunch.WorkingDirectory(DevEditor, null));
    }

    [Fact]
    public void EditorLaunch_PassesTheModSdksGameDataLikeTheHub()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"X:\Sdk\GameData\Vanilla", @"X:\Sdk\GameData\Engine" };

        var layout = EditorLaunch.Find(@"X:\Sdk\Editor\VRage.Editor.exe", existing.Contains, _ => false)!;

        Assert.True(layout.FromSdk);
        Assert.Null(layout.DevGameExe);
        Assert.Equal(["--project", @"Y:\Mods\A\A.vrgproj", "--globalProjectDirs", @"X:\Sdk\GameData", @"X:\Sdk\VRage\GameData",
                "--globalAssemblyDirs", @"X:\Sdk\Editor",
                "--projectExecutableSearchPaths", @"X:\Game\Game2"],
            EditorLaunch.Arguments(@"Y:\Mods\A\A.vrgproj", @"X:\Sdk\Editor\VRage.Editor.exe", layout, @"X:\Game\Game2\SpaceEngineers2.exe"));
    }

    [Fact]
    public void EditorLaunch_FindsNothing_ForAnEditorInNeitherLayout()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"X:\Somewhere\Game2\GameData", @"X:\Somewhere\GameData" };

        Assert.Null(EditorLaunch.Find(@"X:\Somewhere\Editor\VRage.Editor.exe", existing.Contains, _ => true));
    }

    [Fact]
    public void LfProject_StaysLf()
    {
        string lf = Project.Replace("\r\n", "\n");

        string hooked = VrgProj.Hook(lf, Runtime);

        Assert.DoesNotContain("\r", hooked);
        Assert.EndsWith("}\n", hooked);
    }

    [Fact]
    public void ModIndex_ListsGuidAndFolder()
    {
        const string index = """
            {
              "$Bundles": { "System.Runtime": "1.0.0.0", "VRage": "1.5.0.3086" },
              "$Type": "VRage:Keen.VRage.Core.Project.ModIndex",
              "$Value": {
                "Projects": {
                  "bc5b7b66-f3d5-43d8-8b95-598dee48c55e": "C:\\Users\\m\\Documents\\SpaceEngineers2\\Mods\\New SE2 Mod",
                  "0dca74b9-f717-44fb-825c-0da201822a79": "C:\\Users\\m\\Documents\\SpaceEngineers2\\Mods\\NOTaROTOR"
                }
              }
            }
            """;

        var entries = ModIndex.Parse(index);

        Assert.Equal(
            [new ModIndexEntry("bc5b7b66-f3d5-43d8-8b95-598dee48c55e", @"C:\Users\m\Documents\SpaceEngineers2\Mods\New SE2 Mod"),
             new ModIndexEntry("0dca74b9-f717-44fb-825c-0da201822a79", @"C:\Users\m\Documents\SpaceEngineers2\Mods\NOTaROTOR")],
            entries);
    }

    [Fact]
    public void LauncherSettings_ProjectsRoundTrip()
    {
        var settings = LauncherSettings.Empty.WithProjects([@"D:\Mods\A\A.vrgproj", @"D:\Mods\B\B.vrgproj"]);

        var parsed = LauncherSettings.Parse(settings.Serialize());

        Assert.Equal([@"D:\Mods\A\A.vrgproj", @"D:\Mods\B\B.vrgproj"], parsed.Projects);
    }

    [Fact]
    public void LauncherSettings_KeepsUnknownKeys()
    {
        const string text = """{ "Future": { "x": 1 }, "Projects": ["D:\\Mods\\A\\A.vrgproj"] }""";

        string written = LauncherSettings.Parse(text).WithProjects([]).Serialize();

        Assert.Empty(LauncherSettings.Parse(written).Projects);
        Assert.Contains("\"Future\"", written);
    }

    [Fact]
    public void LauncherSettings_SkipsNonStringAndBlankProjects()
    {
        var settings = LauncherSettings.Parse("""{ "Projects": ["", 5, null, "D:\\M\\M.vrgproj"] }""");

        Assert.Equal([@"D:\M\M.vrgproj"], settings.Projects);
    }

    [Fact]
    public void LauncherSettings_NotAnObject_Throws()
    {
        Assert.Throws<FormatException>(() => LauncherSettings.Parse("[]"));
    }
}
