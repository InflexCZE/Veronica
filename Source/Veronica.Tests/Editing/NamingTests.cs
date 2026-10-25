using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class NamingTests
{
    [Theory]
    [InlineData("Dummies[0].Name", "Dummies|[0]|Name")]
    [InlineData("$Root.UIData.Name", "UIData|Name")]
    [InlineData("ThrustClasses[Ion.Large].X", "ThrustClasses|[Ion.Large]|X")]
    [InlineData("A[0][1]", "A|[0]|[1]")]
    [InlineData(" Power ", "Power")]
    public void Paths_parse(string path, string expected) =>
        Assert.Equal(expected, string.Join("|", FieldPath.Parse(path).Select(s => s.Bracketed ? $"[{s.Text}]" : s.Text)));

    [Theory]
    [InlineData("A..B")]
    [InlineData("A.")]
    [InlineData(".A")]
    [InlineData("A[0")]
    [InlineData("A[]")]
    [InlineData("A]")]
    [InlineData("A[0]B")]
    public void Bad_paths_are_errors(string path) => Assert.Throws<EditError>(() => FieldPath.Parse(path));

    [Theory]
    [InlineData(@"Mods\Thruster\Engine.def", @"Mods\Thruster\Engine.def", true)]
    [InlineData(@"Mods\Thruster\Engine.def", "mods/thruster/engine", true)]
    [InlineData(@"Mods\Thruster\Engine.def", "Engine.def", true)]
    [InlineData(@"Mods\Thruster\Engine.def", "engine", true)]
    [InlineData(@"Mods\Thruster\Engine.def", @"Thruster\Engine", false)]
    [InlineData(@"Mods\Thruster\Engine.def", "Engin", false)]
    [InlineData(@"Mods\Thruster\Engine.def", "", false)]
    public void Content_names(string path, string query, bool matches) => Assert.Equal(matches, ContentNames.Matches(path, query));

    [Theory]
    [InlineData("particle configuration", "ParticleEffectsBudgetConfiguration.def", null, true)]
    [InlineData("particle configuration", "Override of X.partialdef", "ParticleEffectsConfiguration", true)]
    [InlineData("ion_thruster", "IonThrusterDefinition.def", null, true)]
    [InlineData("ThrusterDefinition", "Engine.partialdef", "ThrusterDefinition", true)]
    [InlineData("particle water", "ParticleEffectsConfiguration.def", null, false)]
    [InlineData(" ", "Anything.def", null, false)]
    public void Find_content_needs_every_word(string query, string fileName, string? typeName, bool matches) =>
        Assert.Equal(matches, ContentNames.QueryMatches(query, fileName, typeName));

    private static readonly string[] All =
    [
        "Blocks/Breakable/Ion Thrusters",
        "Blocks/Breakable/Hydrogen Thrusters",
        "Blocks/Breakable/Ion Thrusters Large",
        "Blocks/Armor/Ion",
    ];

    [Theory]
    [InlineData("Blocks/Breakable/Ion Thrusters", "Blocks/Breakable/Ion Thrusters")]
    [InlineData("blocks\\breakable\\ion thrusters", "Blocks/Breakable/Ion Thrusters")]
    [InlineData("Ion Thrusters", "Blocks/Breakable/Ion Thrusters")]
    [InlineData("hydrogen", "Blocks/Breakable/Hydrogen Thrusters")]
    [InlineData("ion", "Blocks/Armor/Ion")]
    public void Template_tiers_pick_one(string query, string expected) => Assert.Equal([expected], Templates.Resolve(All, query));

    [Fact]
    public void Template_words_can_match_several() => Assert.Equal(2, Templates.Resolve(All, "Thrusters Ion").Count);

    [Fact]
    public void Template_plurals_are_not_guessed() => Assert.Empty(Templates.Resolve(All, "Ion Thruster"));

    [Fact]
    public void Template_filter_without_words_lists_all_sorted()
    {
        Assert.Equal(All.OrderBy(t => t, StringComparer.OrdinalIgnoreCase), Templates.Filter(All, null));
        Assert.Equal(3, Templates.Filter(All, "breakable").Count);
        Assert.Equal(3, Templates.Filter(All, "Thrust").Count);
        Assert.Equal(["Blocks/Breakable/Hydrogen Thrusters"], Templates.Filter(All, "hydro thrust"));
    }
}
