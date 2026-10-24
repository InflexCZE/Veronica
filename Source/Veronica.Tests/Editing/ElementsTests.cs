using Keen.VRage.Core.Model;
using Keen.VRage.Library.Serialization;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class ElementsTests
{
    private readonly Sample _sample = new();
    private readonly FakeLinks _links = new();

    private MemberReference Root => new ObjectManipulator(_sample).Root;

    private string Add(string path, string? key = null, string? type = null) => Elements.Add(Root, path, key, type, _links);

    private string Remove(string path) => Elements.Remove(Root, path);

    private static string Fails(Func<string> action) => Assert.Throws<EditError>(action).Message;

    [Fact]
    public void Appends_a_default_element_and_shows_its_fields()
    {
        string reply = Add("Dummies");
        Assert.StartsWith("Added Dummies[1]:", reply);
        Assert.Contains("Dummies[1].Scale = {X: 1, Y: 1, Z: 1} (Vector3)", reply);
        Assert.Equal(2, _sample.Dummies.Count);
    }

    [Fact]
    public void Creates_a_null_list_first()
    {
        _sample.Numbers = null!;
        Add("Numbers");
        Assert.Equal([0], _sample.Numbers);
    }

    [Fact]
    public void Abstract_elements_need_a_type_from_the_list()
    {
        string message = Fails(() => Add("Shapes"));
        Assert.Contains("Box", message);
        Assert.Contains("Ball", message);
    }

    [Fact]
    public void Type_matches_by_short_name()
    {
        Add("Shapes", type: "ball");
        Assert.IsType<Ball>(Assert.Single(_sample.Shapes));
    }

    [Fact]
    public void Type_is_ignored_for_concrete_elements()
    {
        Add("Dummies", type: "Box");
        Assert.Equal(2, _sample.Dummies.Count);
    }

    [Fact]
    public void Definition_elements_start_as_empty_references()
    {
        Assert.Contains("Types[0] = none (DummyTypeDefinition)", Add("Types"));
        Assert.Null(Assert.Single(_sample.Types));
    }

    [Fact]
    public void Dictionary_entries_are_added_under_a_key()
    {
        string reply = Add("Classes", key: "Water");
        Assert.StartsWith("Added Classes[Water]:", reply);
        Assert.Contains("Classes[Water].Speed = 0 (float)", reply);
        Assert.Equal(3, _sample.Classes.Count);
    }

    [Fact]
    public void Dictionary_needs_a_new_key()
    {
        Assert.Contains("pass key", Fails(() => Add("Classes")));
        Assert.Contains("already has key 'Ion'", Fails(() => Add("Classes", key: "Ion")));
    }

    [Fact]
    public void Not_a_collection() => Assert.Contains("not a list or dictionary", Fails(() => Add("Power")));

    [Fact]
    public void Removes_a_list_element_and_says_the_rest_moved()
    {
        _sample.Dummies.Add(new ModelDummy { Name = "Second" });
        Assert.Equal("Removed Dummies[0]; the elements after it moved up one index, 1 left.", Remove("Dummies[0]"));
        Assert.Equal("Second", Assert.Single(_sample.Dummies).Name);
    }

    [Fact]
    public void Removes_a_dictionary_entry_by_either_key_form()
    {
        Assert.Equal("Removed Classes[Ion]; 1 left.", Remove("Classes.Ion"));
        Remove("Classes[Hydrogen]");
        Assert.Empty(_sample.Classes);
    }

    [Fact]
    public void Remove_takes_only_elements()
    {
        Assert.Contains("not a list or dictionary element", Fails(() => Remove("Dummies")));
        Assert.Contains("not a list or dictionary element", Fails(() => Remove("Dummies[0].Name")));
        Assert.Contains("has 1 element", Fails(() => Remove("Dummies[4]")));
    }
}
