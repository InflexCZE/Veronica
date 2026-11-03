using System.Text.Json;
using Keen.VRage.Core.Model;
using Keen.VRage.Library.Serialization;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

[Serialize]
public partial class SetSample
{
    public HashSet<string> Tags = ["Inventory", "Conveyor"];
    public HashSet<DummyTypeDefinition> Allowed = [];
    public HashSet<string>? Missing;
    public Dictionary<string, float> Speeds = [];
}

public sealed class SetTests
{
    private const string ThrusterElement = @"Allowed[System\DummyTypes\Thruster.def]";
    private const string ConveyorElement = @"Allowed[System\DummyTypes\Conveyor.def]";

    private readonly SetSample _sample = new();
    private readonly FakeLinks _links = new();

    public SetTests() => _sample.Allowed.Add((DummyTypeDefinition)_links.CreateDefinitionReference(FakeLinks.Thruster));

    private MemberReference Root => new ObjectManipulator(_sample).Root;

    private string Read(string path) => FieldReader.Read(Root, path, _links);

    private string Add(string path, string? key = null, string? value = null) => Elements.Add(Root, path, key, null, _links, value: value);

    private string Remove(string path) => Elements.Remove(Root, path, _links);

    private string Write(string path, string json)
    {
        using var document = JsonDocument.Parse(json);
        return FieldWriter.Write(Root, path, document.RootElement, _links);
    }

    private static string Fails(Func<string> action) => Assert.Throws<EditError>(action).Message;

    private IEnumerable<Guid> AllowedIds => _sample.Allowed.Select(d => d.Guid);

    [Fact]
    public void Read_lists_the_elements_as_paths()
    {
        string text = Read("Tags");
        Assert.Contains("Tags: set of 2 string", text);
        Assert.Contains("Tags[Inventory]", text);
        Assert.Contains("Tags[Conveyor]", text);
    }

    [Fact]
    public void Read_prints_a_definition_element_as_its_path()
    {
        string text = Read("Allowed");
        Assert.Contains("Allowed: set of 1 DummyTypeDefinition", text);
        Assert.Contains(ThrusterElement, text);
    }

    [Fact]
    public void Read_cuts_a_long_set_and_names_the_rest()
    {
        foreach (var name in new[] { "A", "B", "C", "D", "E" })
            _sample.Tags.Add(name);
        Assert.Contains("Tags: ... (+2 more: D, E)", Read("Tags"));
    }

    [Fact]
    public void An_element_is_addressed_by_its_name() =>
        Assert.StartsWith(ThrusterElement, Read("Allowed[Thruster]"));

    [Fact]
    public void A_missing_element_lists_the_elements() =>
        Assert.Contains("Elements: Inventory, Conveyor", Fails(() => Read("Tags[Power]")));

    [Fact]
    public void Add_takes_the_element_as_key()
    {
        Assert.Equal("Added Tags[Power]; 3 in the set.", Add("Tags", key: "Power"));
        Assert.Contains("Power", _sample.Tags);
    }

    [Fact]
    public void Add_takes_the_element_as_value()
    {
        Add("Tags", value: "Power");
        Assert.Contains("Power", _sample.Tags);
    }

    [Fact]
    public void Add_resolves_a_definition_by_name()
    {
        Assert.StartsWith($"Added {ConveyorElement};", Add("Allowed", key: "Conveyor"));
        Assert.Equal([FakeLinks.Thruster, FakeLinks.Conveyor], AllowedIds.OrderBy(g => g));
    }

    [Fact]
    public void Add_refuses_an_element_already_there_and_names_it()
    {
        Assert.Contains($"already has {ThrusterElement}", Fails(() => Add("Allowed", key: "Thruster.def")));
        Assert.Single(_sample.Allowed);
    }

    [Fact]
    public void Add_needs_the_element() =>
        Assert.Contains("pass the element to add as key", Fails(() => Add("Tags")));

    [Fact]
    public void Add_refuses_two_different_elements() =>
        Assert.Contains("Pass the element once", Fails(() => Add("Tags", key: "A", value: "B")));

    [Fact]
    public void Add_creates_a_null_set_first()
    {
        Add("Missing", key: "Power");
        Assert.Equal(["Power"], _sample.Missing!);
    }

    [Fact]
    public void Remove_takes_the_path_read_prints()
    {
        Assert.Equal($"Removed {ThrusterElement}; 0 left.", Remove(ThrusterElement));
        Assert.Empty(_sample.Allowed);
    }

    [Fact]
    public void Remove_takes_the_name_too()
    {
        Remove("Tags[Inventory]");
        Assert.Equal(["Conveyor"], _sample.Tags);
    }

    [Fact]
    public void Write_on_a_set_points_at_add_and_remove()
    {
        Assert.Contains("element_add", Fails(() => Write("Tags", "\"Power\"")));
        Assert.Contains("element_remove", Fails(() => Write("Tags", "null")));
    }

    [Fact]
    public void Write_on_an_element_says_to_replace_it() =>
        Assert.Contains("remove it with element_remove", Fails(() => Write("Tags[Inventory]", "\"Power\"")));

    [Fact]
    public void Find_names_the_element_that_links()
    {
        var found = FieldReader.FindLinks(Root, FakeLinks.Thruster, _links);
        Assert.Equal([ThrusterElement], found);
    }

    [Fact]
    public void Add_with_value_writes_a_dictionary_entry()
    {
        Assert.Equal("Added Speeds[Ion]; Speeds[Ion]: 0 -> 2.5", Add("Speeds", key: "Ion", value: "2.5"));
        Assert.Equal(2.5f, _sample.Speeds["Ion"]);
    }

    [Fact]
    public void A_bad_value_leaves_no_entry()
    {
        Fails(() => Add("Speeds", key: "Ion", value: "fast"));
        Assert.Empty(_sample.Speeds);
    }
}
