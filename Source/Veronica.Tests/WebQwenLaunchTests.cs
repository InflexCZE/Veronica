using Veronica.Agent.WebQwen;
using Xunit;

namespace Veronica.Tests;

public sealed class WebQwenLaunchTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Unset_context_size_is_the_default_without_a_problem(string? setting)
    {
        Assert.Equal(WebQwenLaunch.DefaultContextWindowSize, WebQwenLaunch.ContextWindowSizeFrom(setting, out string? problem));
        Assert.Null(problem);
    }

    [Fact]
    public void Context_size_setting_is_taken_trimmed()
    {
        Assert.Equal(32768, WebQwenLaunch.ContextWindowSizeFrom(" 32768 ", out string? problem));
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("50K")]
    [InlineData("99999999999")]
    public void Unusable_context_size_falls_back_to_the_default_and_says_why(string setting)
    {
        Assert.Equal(WebQwenLaunch.DefaultContextWindowSize, WebQwenLaunch.ContextWindowSizeFrom(setting, out string? problem));
        Assert.Contains("VERONICA_CONTEXT_SIZE", problem);
        Assert.Contains(setting, problem);
    }

    [Fact]
    public void Arguments_carry_the_context_size()
    {
        var launch = new WebQwenLaunch("WebQwen.exe", "Model-MLC", @"C:\models", 20000);
        Assert.Equal(["serve", "--model", "Model-MLC", "--models", @"C:\models", "--context", "20000"], launch.Arguments());
    }

    [Fact]
    public void Arguments_without_a_models_folder_still_carry_the_context_size()
    {
        var launch = new WebQwenLaunch("WebQwen.exe", "Model-MLC", null, 50000);
        Assert.Equal(["serve", "--model", "Model-MLC", "--context", "50000"], launch.Arguments());
    }

    [Fact]
    public void Provider_reports_the_context_size_it_launches_with()
    {
        using var provider = new WebQwenProvider(new WebQwenLaunch("WebQwen.exe", "Model-MLC", null, 30000), _ => { });
        Assert.Equal(30000, provider.ContextWindow);
    }
}
