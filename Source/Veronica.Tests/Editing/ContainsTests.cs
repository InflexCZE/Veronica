using Keen.VRage.Core.Model;
using Keen.VRage.Library.Serialization;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class ContainsTests
{
    private readonly Sample _sample = new();
    private readonly SetSample _set = new();
    private readonly FakeLinks _links = new();

    private DummyTypeDefinition Reference(Guid id) => (DummyTypeDefinition)_links.CreateDefinitionReference(id);

    private string Search(IAccessible root, string path, string value, params Guid[] items) =>
        FieldSearch.Search(FieldReader.Entries(new ObjectManipulator(root).Root, path, _links), value, items.ToHashSet(), path.Length == 0 ? "the whole definition" : path);

    [Fact]
    public void Finds_a_set_element_by_name()
    {
        Assert.Equal("Tags[Conveyor]", Search(_set, "Tags", "conveyor"));
    }

    [Fact]
    public void Finds_a_list_element_past_the_ones_read_shows()
    {
        Assert.Equal("Numbers[7] = 7", Search(_sample, "Numbers", "7"));
    }

    [Fact]
    public void Finds_a_dictionary_key_and_a_value()
    {
        Assert.Equal("Classes[Ion]", Search(_sample, "Classes", "ion"));
        Assert.Equal("Classes[Hydrogen].Speed = 2", Search(_sample, "Classes", "2"));
    }

    [Fact]
    public void Finds_a_field_of_an_element()
    {
        Assert.Equal("Dummies[0].Name = \"Nozzle\"", Search(_sample, "Dummies", "Nozzle"));
    }

    [Fact]
    public void Finds_a_field_of_an_inline_struct()
    {
        _sample.Offset = new Keen.VRage.Library.Mathematics.Vector3(0, 0, 42);
        Assert.Equal("Offset.Z = 42", Search(_sample, "Offset", "42"));
    }

    [Fact]
    public void Lists_values_that_contain_the_text_when_none_matches_whole()
    {
        Assert.Equal("No exact match; similar:\nDummies[0].Name = \"Nozzle\"", Search(_sample, "Dummies", "nozz"));
    }

    [Fact]
    public void Says_what_was_searched_when_nothing_matches()
    {
        Assert.Equal("Nothing in Tags matches \"Thruster\" (2 values searched).", Search(_set, "Tags", "Thruster"));
    }

    [Fact]
    public void Searches_the_whole_definition_without_a_path()
    {
        Assert.Equal("Classes[Hydrogen]", Search(_sample, "", "Hydrogen"));
    }

    [Fact]
    public void Matches_a_reference_by_the_items_the_value_names()
    {
        _set.Allowed.Add(Reference(FakeLinks.Thruster));
        _set.Allowed.Add(Reference(FakeLinks.Conveyor));
        Assert.Equal(@"Allowed[System\DummyTypes\Thruster.def]", Search(_set, "Allowed", "SomeBlock", FakeLinks.Thruster, FakeLinks.Engine));
    }

    [Fact]
    public void Matches_a_reference_by_its_file_name()
    {
        _sample.DummyType = Reference(FakeLinks.Conveyor);
        Assert.Equal(@"DummyType = System\DummyTypes\Conveyor.def", Search(_sample, "DummyType", "Conveyor"));
    }

    [Fact]
    public void Lists_at_most_twenty_matches_and_counts_the_rest()
    {
        for (int i = 0; i < FieldSearch.MaxMatches + 3; i++)
            _sample.Dummies.Add(new ModelDummy { Name = "Port" });
        var lines = Search(_sample, "Dummies", "Port").Split('\n');
        Assert.Equal(FieldSearch.MaxMatches + 1, lines.Length);
        Assert.StartsWith("... and 3 more, not shown.", lines[^1]);
        Assert.Contains("narrow the search", lines[^1]);
    }
}
