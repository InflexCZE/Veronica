using Keen.VRage.Library.UI.Attributes;
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
    [InlineData(@"Mods\Gyro\Gyro_PowerDefinition .partialdef", "Gyro_PowerDefinition", true)]
    [InlineData(@"Mods\Gyro\Gyro_PowerDefinition .partialdef", "Gyro_PowerDefinition ", true)]
    [InlineData(@"Mods\Gyro\Gyro_PowerDefinition .partialdef", @"Mods\Gyro\Gyro_PowerDefinition", true)]
    [InlineData(@"Mods\Gyro\Gyro_PowerDefinition .partialdef", "Gyro_PowerDefinition .partialdef", true)]
    public void Content_names(string path, string query, bool matches) => Assert.Equal(matches, ContentNames.Matches(path, query));

    [Theory]
    [InlineData(@"Assets\Blocks\Ion\100\Models\Deformed.model", @"Blocks/Ion/100/Models/Deformed", true)]
    [InlineData(@"Assets\Blocks\Ion\100\Models\Deformed.model", @"Models\Deformed.model", true)]
    [InlineData(@"Assets\Blocks\Ion\100\Models\Deformed.model", @"/100/models/deformed/", true)]
    [InlineData(@"Assets\Blocks\Ion\100\Models\Deformed.model", @"Ion\Models\Deformed", false)]
    [InlineData(@"Assets\Blocks\Ion\100\Models\Deformed.model", "Deformed", false)]
    [InlineData(@"Assets\Blocks\Ion\100\Models\Deformed.model", @"odels\Deformed", false)]
    public void Path_endings_match_whole_segments(string path, string query, bool matches) =>
        Assert.Equal(matches, ContentNames.EndsWithPath(path, query));

    private static readonly (string Path, string Project)[] Items =
    [
        (@"Assets\Blocks\Ion\100\Models\Deformed.model", "Vanilla"),
        (@"Assets\Blocks\Ion\200\Models\Deformed.model", "Vanilla"),
        (@"Assets\Mk2\Deformed.model", "MyMod"),
        (@"Assets\Vanilla\Note.def", "MyMod"),
    ];

    private static List<string> Pick(string query) => ContentNames.Pick(Items, i => i.Path, i => i.Project, query).Select(i => i.Path).ToList();

    [Theory]
    [InlineData(@"Blocks/Ion/100/Models/Deformed", @"Assets\Blocks\Ion\100\Models\Deformed.model")]
    [InlineData(@"100\Models\Deformed", @"Assets\Blocks\Ion\100\Models\Deformed.model")]
    [InlineData(@"Vanilla/Assets/Blocks/Ion/200/Models/Deformed.model", @"Assets\Blocks\Ion\200\Models\Deformed.model")]
    [InlineData(@"MyMod\Deformed", @"Assets\Mk2\Deformed.model")]
    [InlineData(@"Vanilla\Note", @"Assets\Vanilla\Note.def")]
    [InlineData(@"MyMod:Assets/Mk2/Deformed.model", @"Assets\Mk2\Deformed.model")]
    [InlineData(@"Vanilla:Assets\Blocks\Ion\200\Models\Deformed", @"Assets\Blocks\Ion\200\Models\Deformed.model")]
    public void Pick_takes_shortened_paths_and_project_names(string query, string expected) => Assert.Equal([expected], Pick(query));

    [Fact]
    public void Pick_returns_every_candidate_when_ambiguous()
    {
        Assert.Equal(3, Pick("Deformed").Count);
        Assert.Equal(2, Pick(@"Models\Deformed").Count);
        Assert.Equal(2, Pick(@"Vanilla\Deformed").Count);
    }

    private static List<string> Pick((string Path, string Project)[] items, string query) =>
        ContentNames.Pick(items, i => i.Path, i => i.Project, query).Select(i => $"{i.Project}:{i.Path}").ToList();

    [Fact]
    public void Pick_prefers_exact_names_over_endings() =>
        Assert.Equal(["MyMod:Assets\\Mk2\\Deformed.model"], Pick(
        [
            (@"Assets\Mk2\Deformed.model", "MyMod"),
            (@"Assets\Other\Assets\Mk2\Deformed.model", "Vanilla"),
        ], @"Assets\Mk2\Deformed"));

    [Fact]
    public void Pick_lets_an_exact_path_beat_a_project_named_assets() =>
        Assert.Equal(["MyMod:Assets\\Mk2\\Deformed.model"], Pick(
        [
            (@"Assets\Mk2\Deformed.model", "MyMod"),
            (@"Assets\X\Mk2\Deformed.model", "Assets"),
        ], @"Assets\Mk2\Deformed"));

    [Fact]
    public void Pick_prefers_a_global_ending_over_none_in_a_project_named_like_a_folder() =>
        Assert.Equal(["Vanilla:Assets\\Blocks\\Ion\\Models\\Deformed.model"], Pick(
        [
            (@"Assets\Blocks\Ion\Models\Deformed.model", "Vanilla"),
            (@"Assets\Other\Models\Deformed.model", "Blocks"),
        ], @"Blocks\Ion\Models\Deformed"));

    [Fact]
    public void Pick_narrows_to_a_project_named_like_a_folder_when_nothing_is_exact() =>
        Assert.Equal(["Blocks:Assets\\Mine\\Ion\\Models\\Deformed.model"], Pick(
        [
            (@"Assets\Blocks\Ion\Models\Deformed.model", "Vanilla"),
            (@"Assets\Mine\Ion\Models\Deformed.model", "Blocks"),
        ], @"Blocks\Ion\Models\Deformed"));

    private static readonly (string Path, string Project)[] Mod =
    [
        (@"Assets\IonThruster100Mk2\Ion Thruster 100 Mk2_Client.partialdef", "Mod"),
        (@"Assets\IonThruster100Mk2\Ion Thruster 100 Mk2_ClientComposition.partialdef", "Mod"),
        (@"Assets\IonThruster100Mk2\Ion Thruster 100 Mk2_IonThrusterDefinition.partialdef", "Mod"),
        (@"Assets\Blocks\Ion@\IonThruster100_Client.def", "Vanilla"),
        (@"Assets\Blocks\Ion@\Lamp_Client.def", "Vanilla"),
    ];

    private static List<string> Nearest(string query) =>
        ContentNames.Nearest(Mod, i => i.Path, i => i.Project == "Mod", query).Select(i => i.Path).ToList();

    [Theory]
    [InlineData("Ion Thruster 100 Mk2.Client")]
    [InlineData("Assets/IonThruster100Mk2/Ion Thruster 100 Mk2.Client.partialdef")]
    [InlineData("ion thruster 100 mk2 client")]
    public void Nearest_answers_a_misspelled_separator_with_the_real_name(string query) =>
        Assert.Equal(@"Assets\IonThruster100Mk2\Ion Thruster 100 Mk2_Client.partialdef", Nearest(query)[0]);

    [Fact]
    public void Nearest_offers_nothing_when_nothing_is_close() => Assert.Empty(Nearest("Hydrogen Tank Large"));

    [Theory]
    [InlineData("UI")]
    [InlineData("Lod")]
    public void Nearest_offers_nothing_for_a_short_name_that_is_not_near(string query) =>
        Assert.Empty(ContentNames.Nearest(new[] { "Fx", "Ion", "LOD" }.Where(n => !string.Equals(n, query, StringComparison.OrdinalIgnoreCase)), i => i, _ => false, query));

    [Fact]
    public void Nearest_puts_the_preferred_project_first_on_ties() =>
        Assert.Equal(["Mod", "Vanilla"], ContentNames.Nearest(new[] { (@"B\Xyz_a.def", "Vanilla"), (@"A\Xyz_b.def", "Mod") }, i => i.Item1, i => i.Item2 == "Mod", "Xyz_c").Select(i => i.Item2).ToList());

    [Theory]
    [InlineData("Vanilla", "Vanilla")]
    [InlineData("vanilla/", "Vanilla")]
    [InlineData("Vanilla:", "Vanilla")]
    [InlineData("Vanilla/Assets", null)]
    [InlineData("Assets", null)]
    public void A_bare_project_name_is_its_root(string folder, string? expected) =>
        Assert.Equal(expected, ContentNames.ProjectRoot(["Vanilla", "Mod"], folder));

    [Theory]
    [InlineData("particle configuration", "ParticleEffectsBudgetConfiguration.def", null, true)]
    [InlineData("particle configuration", "Override of X.partialdef", "ParticleEffectsConfiguration", true)]
    [InlineData("ion_thruster", "IonThrusterDefinition.def", null, true)]
    [InlineData("ThrusterDefinition", "Engine.partialdef", "ThrusterDefinition", true)]
    [InlineData("CubeBlockDefinition", "MyThruster.def", "PowerableBlockDefinition,CubeBlockDefinition,Definition", true)]
    [InlineData("CubeBlockDefinition", "MyThruster.def", "ThrusterDefinition,Definition", false)]
    [InlineData("particle water", "ParticleEffectsConfiguration.def", null, false)]
    [InlineData(" ", "Anything.def", null, false)]
    public void Find_content_needs_every_word(string query, string fileName, string? typeNames, bool matches) =>
        Assert.Equal(matches, ContentNames.QueryMatches(query, fileName, typeNames?.Split(',') ?? []));

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

    [Fact]
    public void Synthetic_template_lists_beside_its_source_and_resolves_alone()
    {
        var found = All.ToDictionary(t => t, t => t, StringComparer.OrdinalIgnoreCase);
        Templates.AddSynthetic(found);
        Assert.Equal("Blocks/Breakable/Ion Thrusters", found["Blocks/Breakable/Atmospheric Thrusters"]);
        Assert.Equal(["Blocks/Breakable/Atmospheric Thrusters"], Templates.Resolve(found.Keys, "Atmospheric Thrusters"));
        Assert.Equal(["Blocks/Breakable/Ion Thrusters"], Templates.Resolve(found.Keys, "Ion Thrusters"));
        Assert.Equal(4, Templates.Filter(found.Keys, "Thrust").Count);
        Assert.Equal("Blocks/Breakable/Ion Thrusters", Templates.BaseOf("blocks/breakable/atmospheric thrusters"));
    }

    [Theory]
    [InlineData("Blocks/Breakable/Atmospheric Thrusters", "MyAtmo_IonThrusterDefinition", "MyAtmo_ThrusterDefinition")]
    [InlineData("Blocks/Breakable/Atmospheric Thrusters", "MyAtmo_IonThrusters_ThrusterEffectsComponentDefinition", "MyAtmo_Thrusters_ThrusterEffectsComponentDefinition")]
    [InlineData("Blocks/Breakable/Atmospheric Thrusters", "MyAtmo_ServerComposition", "MyAtmo_ServerComposition")]
    [InlineData("Blocks/Breakable/Ion Thrusters", "MyIon_IonThrusterDefinition", "MyIon_IonThrusterDefinition")]
    public void Synthetic_template_renames_its_source_family_files(string template, string generated, string expected) =>
        Assert.Equal(expected, Templates.FileStem(template, generated));

    [Fact]
    public void Synthetic_template_needs_its_source()
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Blocks/Armor/Ion"] = "" };
        Templates.AddSynthetic(found);
        Assert.Single(found);
    }

    private sealed class BlockKindDefinition;
    private sealed class BlockGroupDefinition;
    private sealed class VoxelMaterialDefinition;
    private sealed class VoxelRenderMaterialDefinition;

    private static readonly Type[] DefinitionChoices =
        [typeof(BlockKindDefinition), typeof(BlockGroupDefinition), typeof(VoxelMaterialDefinition), typeof(VoxelRenderMaterialDefinition)];

    [Theory]
    [InlineData("BlockKindDefinition")]
    [InlineData("blockkind")]
    [InlineData(" BlockKind ")]
    public void Definition_type_by_short_name_with_or_without_suffix(string query) =>
        Assert.Equal(typeof(BlockKindDefinition), DefinitionTypes.Pick(DefinitionChoices, query));

    [Fact]
    public void Definition_type_by_full_name() =>
        Assert.Equal(typeof(VoxelMaterialDefinition), DefinitionTypes.Pick(DefinitionChoices, typeof(VoxelMaterialDefinition).FullName!));

    [Fact]
    public void Definition_type_never_guesses_from_words()
    {
        var e = Assert.Throws<EditError>(() => DefinitionTypes.Pick(DefinitionChoices, "voxel material"));
        Assert.Contains("types containing its words: VoxelMaterialDefinition, VoxelRenderMaterialDefinition.", e.Message);
    }

    public static class PublicTypes
    {
        public sealed class SpinComponent;
    }

    private static class InternalTypes
    {
        public sealed class SpinComponent;
    }

    [Fact]
    public void Same_name_prefers_the_only_public_type() =>
        Assert.Equal(typeof(PublicTypes.SpinComponent),
            DefinitionTypes.Pick([typeof(InternalTypes.SpinComponent), typeof(PublicTypes.SpinComponent)], "Spin", "Component", "component type", "X"));

    [TypeSearchInfo(hideInCreationMenu: true)]
    internal sealed class LeftoverComponent;

    [TypeSearchInfo(hideInCreationMenu: true)]
    public sealed class HiddenPublicComponent;

    internal sealed class InternalComponent;

    [Fact]
    public void A_leftover_is_internal_and_hidden_from_the_creation_menu()
    {
        Assert.True(TypeCatalog.IsLeftover(typeof(LeftoverComponent)));
        Assert.False(TypeCatalog.IsLeftover(typeof(HiddenPublicComponent)));
        Assert.False(TypeCatalog.IsLeftover(typeof(InternalComponent)));
    }

    [Fact]
    public void A_leftover_is_never_picked()
    {
        var e = Assert.Throws<EditError>(() => DefinitionTypes.Pick([typeof(LeftoverComponent)], "Leftover", "Component", "component type", "X"));
        Assert.Contains("No component type is named 'Leftover'", e.Message);
    }

    [Fact]
    public void Definition_type_without_candidates_says_so()
    {
        var e = Assert.Throws<EditError>(() => DefinitionTypes.Pick(DefinitionChoices, "Thruster"));
        Assert.Contains("none contains its words", e.Message);
    }
}
