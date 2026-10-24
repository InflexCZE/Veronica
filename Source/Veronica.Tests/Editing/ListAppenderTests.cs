using Keen.VRage.Library.Serialization;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class ListAppenderTests
{
    private readonly Sample _sample = new();
    private readonly FakeLinks _links = new();

    private string Append(string path, string? type = null) =>
        ListAppender.Append(new ObjectManipulator(_sample).Root, path, type, _links);

    [Fact]
    public void Appends_a_default_element_and_shows_its_fields()
    {
        string reply = Append("Dummies");
        Assert.StartsWith("Added Dummies[1]:", reply);
        Assert.Contains("Dummies[1].Scale = {X: 1, Y: 1, Z: 1} (Vector3)", reply);
        Assert.Equal(2, _sample.Dummies.Count);
    }

    [Fact]
    public void Creates_a_null_list_first()
    {
        _sample.Numbers = null!;
        Append("Numbers");
        Assert.Equal([0], _sample.Numbers);
    }

    [Fact]
    public void Abstract_elements_need_a_type_from_the_list()
    {
        string message = Assert.Throws<EditError>(() => Append("Shapes")).Message;
        Assert.Contains("Box", message);
        Assert.Contains("Ball", message);
    }

    [Fact]
    public void Type_matches_by_short_name()
    {
        Append("Shapes", "ball");
        Assert.IsType<Ball>(Assert.Single(_sample.Shapes));
    }

    [Fact]
    public void Type_is_ignored_for_concrete_elements()
    {
        Append("Dummies", "Box");
        Assert.Equal(2, _sample.Dummies.Count);
    }

    [Fact]
    public void Definition_elements_start_as_empty_references()
    {
        Assert.Contains("Types[0] = none (DummyTypeDefinition)", Append("Types"));
        Assert.Null(Assert.Single(_sample.Types));
    }

    [Fact]
    public void Not_a_list() => Assert.Contains("not a list", Assert.Throws<EditError>(() => Append("Classes")).Message);
}
