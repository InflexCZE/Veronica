using System.Text.Json;
using Keen.VRage.Core.Model;
using Keen.VRage.Library.Serialization;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class DefinitionKeyTests
{
    private const string ThrusterKey = @"ByType[System\DummyTypes\Thruster.def]";
    private const string ConveyorKey = @"ByType[System\DummyTypes\Conveyor.def]";

    private readonly Sample _sample = new();
    private readonly FakeLinks _links = new();

    public DefinitionKeyTests()
    {
        var thruster = (DummyTypeDefinition)_links.CreateDefinitionReference(FakeLinks.Thruster);
        _sample.ByType[thruster] = new Entry { Speed = 5 };
    }

    private MemberReference Root => new ObjectManipulator(_sample).Root;

    private string Read(string path) => FieldReader.Read(Root, path, _links);

    private string Write(string path, string json)
    {
        using var document = JsonDocument.Parse(json);
        return FieldWriter.Write(Root, path, document.RootElement, _links);
    }

    private static string Fails(Func<string> action) => Assert.Throws<EditError>(action).Message;

    private Entry Entry(Guid id) => _sample.ByType.Single(p => p.Key.Guid == id).Value;

    [Fact]
    public void Read_prints_a_definition_key_as_its_path() =>
        Assert.Contains($"{ThrusterKey}.Speed = 5 (float)", Read("ByType"));

    [Fact]
    public void A_definition_key_is_addressed_by_its_name() =>
        Assert.Equal($"{ThrusterKey}.Speed = 5 (float)", Read("ByType[Thruster].Speed"));

    [Fact]
    public void Write_reaches_the_entry_under_a_definition_key()
    {
        Write("ByType[Thruster.def].Speed", "7");
        Assert.Equal(7, Entry(FakeLinks.Thruster).Speed);
    }

    [Fact]
    public void Add_takes_a_definition_path_as_the_key()
    {
        string reply = Elements.Add(Root, "ByType", "Conveyor", null, _links);
        Assert.StartsWith($"Added {ConveyorKey}:", reply);
        Assert.Equal(0, Entry(FakeLinks.Conveyor).Speed);
        Assert.Equal(2, _sample.ByType.Count);
    }

    [Fact]
    public void Add_refuses_a_key_already_there_and_names_its_path()
    {
        string message = Fails(() => Elements.Add(Root, "ByType", "Thruster", null, _links));
        Assert.Contains($"write to {ThrusterKey} instead", message);
        Assert.Single(_sample.ByType);
    }

    [Fact]
    public void Remove_takes_the_path_read_prints()
    {
        Assert.Equal($"Removed {ThrusterKey}; 0 left.", Elements.Remove(Root, ThrusterKey, _links));
        Assert.Empty(_sample.ByType);
    }

    [Fact]
    public void A_missing_key_lists_the_keys_as_paths() =>
        Assert.Contains(@"Keys: System\DummyTypes\Thruster.def", Fails(() => Read("ByType[Conveyor]")));
}
