using System.Text.Json;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.Validation;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class ValueCheckTests
{
    private readonly Bounded _bounded = new();
    private readonly OwnerDefinition _owner = new();
    private readonly FakeLinks _links = new();

    private string Write(string path, string json, IDefinitionLocatorStrategy? definitions = null)
    {
        using var document = JsonDocument.Parse(json);
        return FieldWriter.Write(new ObjectManipulator(_bounded).Root, path, document.RootElement, _links, definitions: definitions);
    }

    [Fact]
    public void A_value_in_range_gets_no_hint() => Assert.Equal("Reach: 100 -> 5000", Write("Reach", "5000"));

    [Fact]
    public void A_value_out_of_range_is_written_with_the_games_message()
    {
        string reply = Write("Reach", "0");
        Assert.StartsWith("Reach: 100 -> 0 (game validation: ", reply);
        Assert.Contains("(0, 50000]", reply);
        Assert.Equal(0, _bounded.Reach);
    }

    [Fact]
    public void A_bound_set_by_another_field_names_that_field_and_its_value()
    {
        string reply = Write("Charge", "5");
        Assert.Contains("game validation", reply);
        Assert.Contains("Discharge is 3.", reply);
        Assert.Equal((FixedPoint)5, _bounded.Charge);
    }

    [Fact]
    public void Only_the_written_field_is_checked() => Assert.Equal("Discharge: 3 -> 2", Write("Discharge", "2"));

    [Fact]
    public void String_length_is_checked() => Assert.Contains("game validation", Write("Label", "\"Much too long\""));

    [Fact]
    public void A_unit_vector_is_checked_whole_and_by_axis()
    {
        Assert.Contains("unit length", Write("Direction", "{\"X\": 1, \"Y\": 1, \"Z\": 1}"));
        Assert.Contains("unit length", Write("Direction.X", "0.5"));
        Assert.DoesNotContain("game validation", Write("Direction", "{\"X\": 1, \"Y\": 0, \"Z\": 0}"));
    }

    [Fact]
    public void A_bound_on_the_definition_is_read_through_the_locator()
    {
        string reply = Write("Speed", "7", new Locator(new ObjectManipulator(_owner).Root));
        Assert.Contains("game validation", reply);
        Assert.Contains("Limit on the component's definition is 5.", reply);
    }

    [Fact]
    public void A_bound_on_a_definition_without_a_locator_says_nothing() => Assert.Equal("Speed: 1 -> 7", Write("Speed", "7"));

    [Fact]
    public void A_nullable_number_is_checked_when_set_and_not_when_cleared()
    {
        Assert.Contains("game validation", Write("Optional", "11"));
        Assert.DoesNotContain("game validation", Write("Optional", "null"));
    }

    private sealed class Locator(MemberReference definition) : IDefinitionLocatorStrategy
    {
        public MemberReference GetDefinitionMember(MemberReference source) => definition;
    }
}
