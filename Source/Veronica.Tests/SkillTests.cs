using System.Text.Json;
using Veronica.Agent;
using Xunit;

namespace Veronica.Tests;

public sealed class SkillTests
{
    private static string Dir([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "Veronica", "Skills");

    [Fact]
    public void Parse_ReadsFrontmatterAndBody()
    {
        var skill = SkillFiles.Parse("---\nname: create-block\ndescription: Use for any\n  new block.\n---\n\nPlan:\n- Do a\n- Do b\n\nSteps:\n1. a\n");

        Assert.Equal("create-block", skill.Name);
        Assert.Equal("Use for any new block.", skill.Description);
        Assert.Equal("Plan:\n- Do a\n- Do b\n\nSteps:\n1. a", skill.Body);
        Assert.Equal(["Do a", "Do b"], skill.Plan);
        Assert.Equal("Steps:\n1. a", skill.Overview);
    }

    [Fact]
    public void Overview_drops_only_the_plan_section()
    {
        var skill = SkillFiles.Parse("---\nname: x\ndescription: d\n---\nNeeds:\n- a\n\nPlan:\n- Do a\n\nIntro.\n\nSteps:\n1. a");

        Assert.Equal("Needs:\n- a\n\nIntro.\n\nSteps:\n1. a", skill.Overview);
    }

    [Theory]
    [InlineData("name: x\n---\nbody", "does not start")]
    [InlineData("---\nname: x\ndescription: d\n", "no closing")]
    [InlineData("---\ndescription: d\n---\nbody", "no name")]
    [InlineData("---\nname: x\n---\nbody", "no description")]
    [InlineData("---\nname: x\ndescription: d\n---\n  \n", "no body")]
    [InlineData("---\nname x\n---\nbody", "line 2")]
    [InlineData("---\nname: x\ndescription: d\n---\nPlan:\n\n- a", "needs one '- item' line")]
    [InlineData("---\nname: x\ndescription: d\n---\nPlan:\n- a\n  wrapped\n- b", "needs one '- item' line")]
    [InlineData("---\nname: x\ndescription: d\n---\nPlan:\n- a\n-b", "needs one '- item' line")]
    public void Parse_RejectsMalformed(string text, string message)
    {
        var e = Assert.Throws<FormatException>(() => SkillFiles.Parse(text));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void Parse_WithoutPlanSection_IsAReference()
    {
        var skill = SkillFiles.Parse("---\nname: x\ndescription: d\n---\nFacts:\n- a");

        Assert.Empty(skill.Plan);
        Assert.Equal("Facts:\n- a", skill.Overview);
    }

    [Fact]
    public void Load_SkipsBrokenAndMismatchedFiles()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "skill-load-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "good.md"), "---\nname: good\ndescription: d\n---\nPlan:\n- a");
            File.WriteAllText(Path.Combine(dir, "broken.md"), "no frontmatter");
            File.WriteAllText(Path.Combine(dir, "other.md"), "---\nname: renamed\ndescription: d\n---\nPlan:\n- a");

            var load = SkillFiles.Load(dir);

            Assert.Equal(["good"], load.Skills.Select(s => s.Name));
            Assert.Equal(2, load.Problems.Count);
            Assert.Contains(load.Problems, p => p.StartsWith("broken.md:"));
            Assert.Contains(load.Problems, p => p.StartsWith("other.md:") && p.Contains("differs"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_MissingDirectoryIsEmpty()
    {
        var load = SkillFiles.Load(Path.Combine(AppContext.BaseDirectory, "no-such-skills"));

        Assert.Empty(load.Skills);
        Assert.Empty(load.Problems);
        Assert.Equal(Prompts.System + "\n\n" + Prompts.AskForMissing, Prompts.SystemWith(load.Skills));
    }

    [Fact]
    public void SystemPrompt_ListsOneLinePerSkill()
    {
        var prompt = Prompts.SystemWith([new Skill("a", "Use for A.", "x", ["a"]), new Skill("b", "Use for B.", "y", ["b"])]);

        Assert.EndsWith(Prompts.HowWeWork + "\n- a: Use for A.\n- b: Use for B.", prompt);
        Assert.Contains("How we work:", prompt);
        Assert.Contains("mark_completed", prompt);
    }

    [Fact]
    public async Task Tool_ReturnsOverviewWithoutPlan_IgnoringCase()
    {
        var tool = new SkillTool(() => [new Skill("create-block", "d", "Plan:\n- a\n\nthe steps", ["a"])], new SkillPlan());

        var result = await tool.ExecuteAsync(Args("""{"name":"Create-Block"}"""), CancellationToken.None);

        Assert.Equal("the steps", result.Output);
    }

    [Fact]
    public async Task Tool_UnknownNameListsSkills()
    {
        var tool = new SkillTool(() => [new Skill("create-block", "d", "x", ["a"]), new Skill("block-thruster", "d", "y", ["b"])], new SkillPlan());

        var result = await tool.ExecuteAsync(Args("""{"name":"thruster"}"""), CancellationToken.None);

        Assert.Equal("There is no skill named 'thruster'; the skills are: create-block, block-thruster.", result.Output);
    }

    [Fact]
    public void ShippedSkills_LoadCleanly_InAscii()
    {
        var load = SkillFiles.Load(Dir());

        Assert.Empty(load.Problems);
        Assert.NotEmpty(load.Skills);
        Assert.Contains(load.Skills, s => s.Plan.Count > 0);
        foreach (var file in Directory.EnumerateFiles(Dir(), "*" + SkillFiles.Extension))
        {
            var text = File.ReadAllText(file);
            int bad = text.ToList().FindIndex(c => c > 127 || (c < 32 && c != '\n' && c != '\r' && c != '\t'));
            Assert.True(bad < 0, $"{Path.GetFileName(file)} has a non-ASCII or control character at {bad}");
        }
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
