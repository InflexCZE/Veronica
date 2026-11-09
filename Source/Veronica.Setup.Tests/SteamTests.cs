using System.Text;
using Veronica.Setup.Core;
using Xunit;

namespace Veronica.Setup.Tests;

public class SteamTests
{
    private const string Root = @"X:\Steam";

    private const string LibraryFolders = """
        "libraryfolders"
        {
        	"0"
        	{
        		"path"		"X:\\Steam"
        		"label"		""
        		"apps"
        		{
        			"100"		"12345"
        		}
        	}
        	"1"
        	{
        		"path"		"Y:\\Library"
        		"apps"
        		{
        		}
        	}
        }
        """;

    private static string Manifest(string appId, string installDir) => $$"""
        "AppState"
        {
        	"appid"		"{{appId}}"
        	"name"		"Some App"
        	"installdir"		"{{installDir}}"
        	"InstalledDepots"
        	{
        		"101"
        		{
        			"manifest"		"42"
        		}
        	}
        }
        """;

    private static MemoryFiles Files(params (string Path, string Text)[] entries)
    {
        var files = new MemoryFiles();
        foreach (var (path, text) in entries)
            files.WriteAllBytes(path, Encoding.UTF8.GetBytes(text));
        return files;
    }

    [Fact]
    public void KeyValues_ReadsNestedBlocksEscapesAndComments()
    {
        var kv = KeyValues.Parse("""
            // a comment
            "Root"
            {
            	"Path"	"A:\\B \"C\""
            	"Inner" { "x" "1" "X" "2" }
            	"Cond"	"y"	[$WIN32]
            }
            """);

        var root = kv.Block("root")!;
        Assert.Equal("A:\\B \"C\"", root.String("path"));
        Assert.Equal("2", root.Block("inner")!.String("x"));
        Assert.Equal("y", root.String("Cond"));
        Assert.Null(root.String("Inner"));
        Assert.Null(root.Block("Path"));
    }

    [Theory]
    [InlineData("\"a\" {")]
    [InlineData("\"a\" \"b")]
    [InlineData("}")]
    [InlineData("\"a\"")]
    [InlineData("a b")]
    public void KeyValues_RejectsBrokenText(string text)
    {
        Assert.Throws<FormatException>(() => KeyValues.Parse(text));
    }

    [Fact]
    public void Libraries_AreTheSteamFolderThenEveryListedPath()
    {
        var files = Files((@"X:\Steam\steamapps\libraryfolders.vdf", LibraryFolders));

        Assert.Equal([@"X:\Steam", @"Y:\Library"], Steam.Libraries(files, "X:/Steam/"));
    }

    [Fact]
    public void AppFolder_IsFoundInAnyLibrary_EvenWhenItsAppsListIsStale()
    {
        var files = Files(
            (@"X:\Steam\steamapps\libraryfolders.vdf", LibraryFolders),
            (@"Y:\Library\steamapps\appmanifest_200.acf", Manifest("200", "Some App")),
            (@"Y:\Library\steamapps\common\Some App\app.exe", ""));

        Assert.Equal(@"Y:\Library\steamapps\common\Some App", Steam.AppFolder(files, Root, "200"));
    }

    [Fact]
    public void AppFolder_SkipsAManifestWhoseFolderIsGone()
    {
        var files = Files(
            (@"X:\Steam\steamapps\libraryfolders.vdf", LibraryFolders),
            (@"X:\Steam\steamapps\appmanifest_200.acf", Manifest("200", "Old Copy")),
            (@"Y:\Library\steamapps\appmanifest_200.acf", Manifest("200", "Some App")),
            (@"Y:\Library\steamapps\common\Some App\app.exe", ""));

        Assert.Equal(@"Y:\Library\steamapps\common\Some App", Steam.AppFolder(files, Root, "200"));
    }

    [Theory]
    [InlineData(@"..\\Elsewhere")]
    [InlineData(@"Z:\\Elsewhere")]
    [InlineData("")]
    public void AppFolder_IgnoresAnInstallDirThatLeavesTheLibrary(string installDir)
    {
        var files = Files(
            (@"X:\Steam\steamapps\appmanifest_200.acf", Manifest("200", installDir)),
            (@"X:\Steam\Elsewhere\app.exe", ""),
            (@"Z:\Elsewhere\app.exe", ""));

        Assert.Null(Steam.AppFolder(files, Root, "200"));
    }

    [Fact]
    public void AppFolder_SurvivesBrokenFiles()
    {
        var files = Files(
            (@"X:\Steam\steamapps\libraryfolders.vdf", "\"libraryfolders\" {"),
            (@"X:\Steam\steamapps\appmanifest_200.acf", "garbage"));

        Assert.Null(Steam.AppFolder(files, Root, "200"));
    }

    [Fact]
    public void EditorAndGame_AreTheExesInsideTheirApps()
    {
        var files = Files(
            (@"X:\Steam\steamapps\libraryfolders.vdf", LibraryFolders),
            ($@"Y:\Library\steamapps\appmanifest_{Steam.ModSdkAppId}.acf", Manifest(Steam.ModSdkAppId, "Sdk Folder")),
            (@"Y:\Library\steamapps\common\Sdk Folder\Editor\VRage.Editor.exe", ""),
            ($@"X:\Steam\steamapps\appmanifest_{Steam.GameAppId}.acf", Manifest(Steam.GameAppId, "Game Folder")),
            (@"X:\Steam\steamapps\common\Game Folder\Game2\SpaceEngineers2.exe", ""));

        Assert.Equal(@"Y:\Library\steamapps\common\Sdk Folder\Editor\VRage.Editor.exe", Steam.EditorExe(files, Root));
        Assert.Equal(@"X:\Steam\steamapps\common\Game Folder\Game2\SpaceEngineers2.exe", Steam.GameExe(files, Root));
        Assert.Null(Steam.GameExe(files, null));
    }

    [Fact]
    public void Game_IsNull_WhenItsAppHasNoExe()
    {
        var files = Files(
            ($@"X:\Steam\steamapps\appmanifest_{Steam.GameAppId}.acf", Manifest(Steam.GameAppId, "Game Folder")),
            (@"X:\Steam\steamapps\common\Game Folder\readme.txt", ""));

        Assert.Null(Steam.GameExe(files, Root));
    }
}
