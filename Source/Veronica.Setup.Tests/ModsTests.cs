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

    [Fact]
    public void EditorLaunch_FindsTheCheckoutsGameDataAboveADevEditor()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"R:\SE2\Game2\GameData", @"R:\SE2\VRage\GameData", @"R:\SE2\VRage\Sources\Editor\GameData" };

        var dirs = EditorLaunch.GlobalProjectDirs(@"R:\SE2\VRage\Sources\Editor\VRage.Editor\bin\x64\Debug\VRage.Editor.exe", existing.Contains);

        Assert.Equal([@"R:\SE2\Game2\GameData", @"R:\SE2\VRage\GameData"], dirs);
        Assert.Equal(["--project", @"C:\Mods\A\A.vrgproj", "--globalProjectDirs", @"R:\SE2\Game2\GameData", @"R:\SE2\VRage\GameData"],
            EditorLaunch.Arguments(@"C:\Mods\A\A.vrgproj", dirs));
    }

    [Fact]
    public void EditorLaunch_FindsNothingWithoutBothGameDataFolders()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\Games\SE2\Game2\GameData" };

        Assert.Empty(EditorLaunch.GlobalProjectDirs(@"C:\Games\SE2\Editor\VRage.Editor.exe", existing.Contains));
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
}
