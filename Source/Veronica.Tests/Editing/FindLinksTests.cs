using Keen.VRage.Core.Model;
using Keen.VRage.Library.Serialization;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class FindLinksTests
{
    private readonly Sample _sample = new();
    private readonly FakeLinks _links = new();

    private DummyTypeDefinition Reference(Guid id) => (DummyTypeDefinition)_links.CreateDefinitionReference(id);

    private List<string> Find(Guid target) => FieldReader.FindLinks(new ObjectManipulator(_sample).Root, target, _links);

    [Fact]
    public void Finds_a_reference_field()
    {
        _sample.DummyType = Reference(FakeLinks.Thruster);
        Assert.Equal(["DummyType"], Find(FakeLinks.Thruster));
    }

    [Fact]
    public void Finds_elements_past_the_ones_read_shows()
    {
        for (int i = 0; i < FieldReader.MaxElements + 2; i++)
            _sample.Types.Add(Reference(i == FieldReader.MaxElements + 1 ? FakeLinks.Thruster : FakeLinks.Conveyor));
        Assert.Equal([$"Types[{FieldReader.MaxElements + 1}]"], Find(FakeLinks.Thruster));
    }

    [Fact]
    public void Finds_a_definition_dictionary_key()
    {
        _sample.ByType[Reference(FakeLinks.Thruster)] = new Entry();
        Assert.Equal([@"ByType[System\DummyTypes\Thruster.def]"], Find(FakeLinks.Thruster));
    }

    [Fact]
    public void Finds_nothing_when_nothing_points_at_the_target()
    {
        _sample.DummyType = Reference(FakeLinks.Conveyor);
        Assert.Empty(Find(FakeLinks.Thruster));
    }

    [Fact]
    public void Paths_carry_the_base_path()
    {
        _sample.DummyType = Reference(FakeLinks.Thruster);
        Assert.Equal(["Components[Thruster].DummyType"],
            FieldReader.FindLinks(new ObjectManipulator(_sample).Root, FakeLinks.Thruster, _links, "Components[Thruster]"));
    }
}
