using System.Text.Json;
using Keen.VRage.Core.Model;
using Keen.VRage.Library.Extensions;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Utils;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class FieldEditTests
{
    private readonly Sample _sample = new();
    private readonly FakeLinks _links = new();

    private MemberReference Root => new ObjectManipulator(_sample).Root;

    private string Read(string path = "") => FieldReader.Read(Root, path, _links);

    private string Write(string path, string json)
    {
        using var document = JsonDocument.Parse(json);
        return FieldWriter.Write(Root, path, document.RootElement, _links);
    }

    private string Fails(Func<string> action) => Assert.Throws<EditError>(action).Message;

    [Fact]
    public void Dump_prints_one_line_per_field_with_full_paths()
    {
        string dump = Read();
        Assert.Contains("Title = \"Placeholder\" (string)", dump);
        Assert.Contains("Power = 10000 (float)", dump);
        Assert.Contains("Mode = Forward (Mode)", dump);
        Assert.Contains("Dummies[0].Name = \"Nozzle\" (string)", dump);
        Assert.Contains("Child.Next.Depth = 2 (int)", dump);
    }

    [Fact]
    public void Dump_hides_what_the_inspector_hides() => Assert.DoesNotContain("Hidden", Read());

    [Fact]
    public void Small_structs_print_inline()
    {
        Assert.Contains("Scale = {X: 1, Y: 1, Z: 1} (Vector3)", Read());
        Assert.Contains("Dummies[0].Scale = {X: 1, Y: 1, Z: 1} (Vector3)", Read());
    }

    [Fact]
    public void Lists_show_five_elements_then_a_hint()
    {
        string dump = Read("Numbers");
        Assert.Contains("Numbers: list of 8 int", dump);
        Assert.Contains("Numbers[4] = 4 (int)", dump);
        Assert.DoesNotContain("Numbers[5] =", dump);
        Assert.Contains("+3 more, read \"Numbers[5]\"", dump);
    }

    [Fact]
    public void Dictionary_entries_print_with_bracket_keys()
    {
        string dump = Read("Classes");
        Assert.Contains("Classes[Ion].Speed = 1 (float)", dump);
        Assert.Contains("Classes[Hydrogen].Speed = 2 (float)", dump);
    }

    [Fact]
    public void Dictionary_keys_accept_dot_and_brackets()
    {
        Assert.Equal("Classes[Ion].Speed = 1 (float)", Read("Classes.Ion.Speed"));
        Assert.Equal("Classes[Ion].Speed = 1 (float)", Read("Classes[Ion].Speed"));
    }

    [Fact]
    public void Deep_objects_collapse_to_field_names()
    {
        string dump = Read();
        Assert.Contains("Child.Next.Next.Next: Level4 {Depth} (read \"Child.Next.Next.Next\")", dump);
        Assert.Contains("Child.Next.Next.Next.Depth = 4 (int)", Read("Child.Next.Next.Next"));
    }

    [Fact]
    public void Null_objects_print_as_null() => Assert.Contains("Missing = null (Inner)", Read());

    [Fact]
    public void References_print_paths_and_none()
    {
        _sample.DummyType = (DummyTypeDefinition)_links.CreateDefinitionReference(FakeLinks.Thruster);
        string dump = Read();
        Assert.Contains(@"DummyType = System\DummyTypes\Thruster.def (DummyTypeDefinition)", dump);
        Assert.Contains("Model = none (ResourceHandle<ModelAsset>)", dump);
    }

    [Fact]
    public void Enum_read_lists_its_values() => Assert.Contains("values: Forward, Backward", Read("Mode"));

    [Fact]
    public void Leading_root_is_accepted() => Assert.Equal("Power = 10000 (float)", Read("$Root.Power"));

    [Fact]
    public void Bad_field_lists_the_fields()
    {
        string message = Fails(() => Read("Dummies[0].Nmae"));
        Assert.Contains("'Nmae' is not a field of 'Dummies[0]'", message);
        Assert.Contains("Name", message);
        Assert.Contains("Scale", message);
    }

    [Fact]
    public void Field_names_forgive_case() => Assert.Equal("Power = 10000 (float)", Read("power"));

    [Fact]
    public void Index_out_of_range_says_the_range() => Assert.Contains("[0] to [0]", Fails(() => Read("Dummies[3]")));

    [Fact]
    public void Missing_key_lists_the_keys() => Assert.Contains("Ion, Hydrogen", Fails(() => Read("Classes[Water]")));

    [Fact]
    public void Read_cuts_at_the_line_cap()
    {
        string[] lines = FieldReader.Read(Root, "", _links, maxLines: 10).Split('\n');
        Assert.Equal(11, lines.Length);
        Assert.Equal("... output cut at 10 lines; read a narrower path.", lines[^1]);
    }

    [Fact]
    public void Write_reports_old_and_new()
    {
        Assert.Equal("Power: 10000 -> 25000", Write("Power", "25000"));
        Assert.Equal(25000f, _sample.Power);
    }

    [Theory]
    [InlineData("\"30.5\"")]
    [InlineData("30.5")]
    public void Numbers_come_as_numbers_or_text(string json)
    {
        Write("Power", json);
        Assert.Equal(30.5f, _sample.Power);
    }

    [Fact]
    public void Write_into_list_elements_and_nested_structs()
    {
        Write("Dummies[0].Transform.Transform.Position.Z", "0.6458988");
        Assert.Equal(0.6458988f, _sample.Dummies[0].Transform.Transform.Position.Z);
    }

    [Fact]
    public void Write_text_enum_bool_and_ids()
    {
        Write("Title", "\"Modded Thruster\"");
        Write("Mode", "\"backward\"");
        Write("Enabled", "true");
        Write("ThrustClass", "\"Ion\"");
        Write("Label", "\"DisplayName_Thruster\"");
        Assert.Equal("Modded Thruster", _sample.Title);
        Assert.Equal(Mode.Backward, _sample.Mode);
        Assert.True(_sample.Enabled);
        Assert.Equal("Ion", _sample.ThrustClass.ToString());
        Assert.Equal("DisplayName_Thruster", _sample.Label.TextId.ToString());
    }

    [Fact]
    public void Bad_enum_lists_the_values() => Assert.Contains("Forward, Backward", Fails(() => Write("Mode", "\"Up\"")));

    [Fact]
    public void Bad_number_changes_nothing()
    {
        Assert.Contains("not a valid float", Fails(() => Write("Power", "\"lots\"")));
        Assert.Equal(10000f, _sample.Power);
    }

    [Fact]
    public void Inline_struct_writes_in_the_printed_form()
    {
        Assert.Equal("Dummies[0].Scale: {X: 1, Y: 1, Z: 1} -> {X: 0.1, Y: 0.1, Z: 0.1}", Write("Dummies[0].Scale", "\"{X: 0.1, Y: 0.1, Z: 0.1}\""));
        Assert.Equal(new Vector3(0.1f, 0.1f, 0.1f), _sample.Dummies[0].Scale);
    }

    [Fact]
    public void Inline_struct_takes_json_and_keeps_fields_left_out()
    {
        Write("Scale", "{\"y\": 2}");
        Assert.Equal(new Vector3(1, 2, 1), _sample.Scale);
    }

    [Fact]
    public void Inline_struct_with_a_bad_value_changes_nothing()
    {
        Fails(() => Write("Scale", "\"{X: 5, Y: wide}\""));
        Assert.Equal(Vector3.One, _sample.Scale);
    }

    [Fact]
    public void Composite_write_names_its_fields() => Assert.Contains("Child.Depth", Fails(() => Write("Child", "1")));

    [Fact]
    public void Collection_write_points_at_elements() => Assert.Contains("element_add", Fails(() => Write("Dummies", "1")));

    [Fact]
    public void References_resolve_by_name()
    {
        Assert.Equal(@"DummyType: none -> System\DummyTypes\Thruster.def", Write("DummyType", "\"Thruster\""));
        Assert.Equal(FakeLinks.Thruster, _sample.DummyType!.Guid);

        Write("Model", "\"Engine\"");
        Assert.Equal(FakeLinks.Engine, _sample.Model.TryGetGuid());
    }

    [Fact]
    public void Null_clears_to_the_default()
    {
        Write("DummyType", "\"Thruster\"");
        Write("DummyType", "null");
        Write("Title", "null");
        Write("Scale", "null");
        Assert.Null(_sample.DummyType);
        Assert.Null(_sample.Title);
        Assert.Equal(default, _sample.Scale);
    }

    [Fact]
    public void Path_needs_a_field()
    {
        Fails(() => Write(" ", "1"));
        Fails(() => Write("$Root", "1"));
        Fails(() => Read("$RootPower"));
    }

    [Fact]
    public void Comma_decimals_are_refused()
    {
        Assert.Contains("use a dot", Fails(() => Write("Power", "\"0,5\"")));
        Assert.Equal(10000f, _sample.Power);
    }

    [Fact]
    public void Null_never_removes_a_list_or_an_object()
    {
        Fails(() => Write("Dummies", "null"));
        Fails(() => Write("Required", "null"));
        Assert.Single(_sample.Dummies);
        Assert.NotNull(_sample.Required);
    }

    [Fact]
    public void Null_nullable_struct_takes_the_inline_form()
    {
        Assert.Equal("Offset: null -> {X: 9, Y: 0, Z: 0}", Write("Offset", "\"{X: 9}\""));
        Assert.Equal(new Vector3(9, 0, 0), _sample.Offset);
    }

    [Fact]
    public void Failed_inline_create_leaves_the_struct_null()
    {
        Fails(() => Write("Offset", "\"{X: wide}\""));
        Assert.Null(_sample.Offset);
    }

    [Fact]
    public void Writes_create_null_parents()
    {
        Assert.Equal("Missing.Next.Depth: 2 -> 5 (created Missing)", Write("Missing.Next.Depth", "5"));
        Assert.Equal(5, _sample.Missing!.Next.Depth);

        Write("Offset.Y", "3");
        Assert.Equal(new Vector3(0, 3, 0), _sample.Offset);
    }

    [Fact]
    public void Failed_write_removes_the_parents_it_created()
    {
        Fails(() => Write("Missing.Depth", "\"deep\""));
        Fails(() => Write("Missing.Nope", "1"));
        Assert.Null(_sample.Missing);
    }

    [Fact]
    public void Abstract_null_parents_name_the_types()
    {
        string message = Fails(() => Write("Form.Size", "2"));
        Assert.Contains("Box", message);
        Assert.Null(_sample.Form);
    }

    [Fact]
    public void Reads_never_create() => Assert.Contains("is null", Fails(() => Read("Missing.Depth")));

    [Fact]
    public void Get_only_objects_show_and_take_writes()
    {
        Assert.Contains("Settings.Speed = 0 (float)", Read());
        Write("Settings.Speed", "4");
        Assert.Equal(4f, _sample.Settings.Speed);
    }

    [Fact]
    public void Get_only_structs_are_read_only_all_the_way_down()
    {
        Assert.DoesNotContain("Fixed", Read());
        Assert.Contains("read-only", Read("Fixed.X"));
        Assert.Contains("read-only", Fails(() => Write("Fixed.X", "2")));
    }
}
