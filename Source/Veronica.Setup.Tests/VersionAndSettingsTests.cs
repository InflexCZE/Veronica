using Veronica.Agent;
using Xunit;

namespace Veronica.Setup.Tests;

public class VersionTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("1.2.3.0", 1, 2, 3)]
    [InlineData("0.4.10+6f0e2c1", 0, 4, 10)]
    public void Parses(string text, int major, int minor, int patch)
    {
        Assert.True(SemVer.TryParse(text, out var version));
        Assert.Equal(new SemVer(major, minor, patch), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Test")]
    [InlineData("v1.2")]
    [InlineData("1.2.3.4")]
    public void Rejects(string text) => Assert.False(SemVer.TryParse(text, out _));

    [Fact]
    public void ComparesNumerically()
    {
        Assert.True(new SemVer(0, 10, 0) > new SemVer(0, 9, 9));
        Assert.True(new SemVer(1, 0, 0) > new SemVer(0, 99, 99));
        Assert.False(new SemVer(1, 2, 3) > new SemVer(1, 2, 3));
    }

    [Fact]
    public void Release_ReadsTagAndAssets()
    {
        const string json = """
            {
              "tag_name": "v0.3.1",
              "name": "Veronica 0.3.1",
              "assets": [
                { "name": "Veronica.exe", "size": 52428800, "browser_download_url": "http://localhost:3000/Inflex/Veronica/releases/download/v0.3.1/Veronica.exe" },
                { "name": "Veronica.zip", "size": 73400320, "browser_download_url": "http://localhost:3000/Inflex/Veronica/releases/download/v0.3.1/Veronica.zip" }
              ]
            }
            """;

        var release = Release.Parse(json);

        Assert.Equal(new SemVer(0, 3, 1), release.Version);
        Assert.Equal(73400320, release.Asset("veronica.zip")!.Size);
        Assert.EndsWith("/Veronica.exe", release.Asset(Release.ExeAsset)!.Url);
    }

    [Fact]
    public void Release_WithAnotherTag_IsRefused() =>
        Assert.Throws<FormatException>(() => Release.Parse("""{ "tag_name": "Test", "assets": [] }"""));
}

public class SettingsTests
{
    private static string Unprotect(string stored) => "plain:" + stored;

    [Fact]
    public void EnvironmentWinsOverTheFile()
    {
        var file = SettingsFile.Parse("""{ "VERONICA_PROVIDER": "openai", "VERONICA_OPENAI_URL": "http://a/v1" }""");
        var env = new Dictionary<string, string> { [Settings.VERONICA_PROVIDER] = "claude" };

        var store = SettingsStore.Layer(file, k => env.GetValueOrDefault(k), Unprotect);

        Assert.Equal("claude", store.Get(Settings.VERONICA_PROVIDER));
        Assert.Equal("http://a/v1", store.Get(Settings.VERONICA_OPENAI_URL));
        Assert.Null(store.Get(Settings.VERONICA_MODEL));
    }

    [Fact]
    public void SecretsInTheFileAreUnprotected_FromTheEnvironmentTakenAsIs()
    {
        var file = SettingsFile.Parse("""{ "VERONICA_OPENAI_API_KEY": "blob" }""");

        Assert.Equal("plain:blob", SettingsStore.Layer(file, _ => null, Unprotect).Get(Settings.VERONICA_OPENAI_API_KEY));
        Assert.Equal("key", SettingsStore.Layer(file, k => k == Settings.VERONICA_OPENAI_API_KEY ? "key" : null, Unprotect).Get(Settings.VERONICA_OPENAI_API_KEY));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"VERONICA_PROVIDER\": 5 }")]
    public void BrokenFile_FallsBackToTheEnvironment(string text)
    {
        var warnings = new List<string>();

        var store = SettingsStore.From(() => text, "Veronica.settings.json", k => k == Settings.VERONICA_PROVIDER ? "claude" : null, Unprotect, warnings.Add);

        Assert.Equal("claude", store.Get(Settings.VERONICA_PROVIDER));
        Assert.True(warnings.Count <= 1);
    }

    [Fact]
    public void UnreadableFile_IsReported()
    {
        var warnings = new List<string>();

        var store = SettingsStore.From(() => throw new IOException("locked"), "Veronica.settings.json", _ => null, Unprotect, warnings.Add);

        Assert.Null(store.Get(Settings.VERONICA_PROVIDER));
        Assert.Contains("locked", Assert.Single(warnings));
    }

    [Fact]
    public void UnknownEnvironmentVariablesAreIgnored()
    {
        var store = SettingsStore.Layer(SettingsFile.Empty, k => k == "PATH" ? "x" : null, Unprotect);

        Assert.Null(store.Get("PATH"));
    }

    [Fact]
    public void With_ProtectsSecrets_RemovesBlanks_KeepsOtherKeys()
    {
        var file = SettingsFile.Parse("""{ "VERONICA_RELEASES_URL": "http://gitea/latest", "VERONICA_CLAUDE_MODEL": "opus" }""");

        var changed = file.With(new Dictionary<string, string?>
        {
            [Settings.VERONICA_PROVIDER] = " openai ",
            [Settings.VERONICA_OPENAI_API_KEY] = "key",
            [Settings.VERONICA_CLAUDE_MODEL] = "",
        }, plain => "protected:" + plain);

        Assert.Equal(
            new Dictionary<string, string>
            {
                [Settings.VERONICA_RELEASES_URL] = "http://gitea/latest",
                [Settings.VERONICA_PROVIDER] = "openai",
                [Settings.VERONICA_OPENAI_API_KEY] = "protected:key",
            },
            changed.Values);
        Assert.Equal(changed.Values, SettingsFile.Parse(changed.Serialize()).Values);
    }
}

public class SafePathTests
{
    [Theory]
    [InlineData("a.md")]
    [InlineData("sub/a.md")]
    public void PlainRelativePath_StaysUnderTheRoot(string relative) =>
        Assert.StartsWith(@"C:\V\Skills\", Veronica.Setup.Core.SafePath.Under(@"C:\V\Skills", relative));

    [Theory]
    [InlineData("../a.md")]
    [InlineData("sub/../../a.md")]
    [InlineData("/a.md")]
    [InlineData("C:/a.md")]
    [InlineData("sub//a.md")]
    [InlineData("")]
    public void EscapingOrRootedPath_IsRefused(string relative) =>
        Assert.Throws<FormatException>(() => Veronica.Setup.Core.SafePath.Under(@"C:\V\Skills", relative));
}

public class ProviderSettingTests
{
    [Theory]
    [InlineData("webqwen")]
    [InlineData("claude")]
    [InlineData("openai")]
    [InlineData("bare_mcp")]
    public void RoundTrips(string value) =>
        Assert.Equal(value, Veronica.Setup.Ui.Providers.SettingsValue(Veronica.Setup.Ui.Providers.FromSettings(value)));
}
