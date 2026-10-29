using System.Text.Json;
using Keen.VRage.Core;
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
    public void Dump_shows_hidden_fields_the_Editor_edits_in_its_own_window()
    {
        string dump = FieldReader.Read(new ObjectManipulator(new ParticleEffectDefinitionObjectBuilder()).Root, "", _links);
        Assert.Contains("ParticleEmitters[0].Speed = 1 (float)", dump);
        Assert.DoesNotContain("Internal", dump);
    }

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
    public void An_unset_immutable_array_reads_as_empty()
    {
        Assert.Contains("Unset: empty list of Entry", Read("Unset"));
        Assert.Equal("'Unset' is empty.", Fails(() => Read("Unset[0].Speed")));
    }

    [Fact]
    public void A_path_does_not_walk_into_a_reference()
    {
        _sample.DummyType = _links.CreateDefinitionReference(FakeLinks.Thruster) as DummyTypeDefinition;
        Assert.Contains("'DummyType' is a reference to another item", Fails(() => Read("DummyType.Name")));
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
    public void Colour_prints_rgba_bytes_without_the_packed_value()
    {
        string dump = Read();

        Assert.Contains("Tint = {R: 0, G: 255, B: 255, A: 255} (ColorSRGB)", dump);
        Assert.DoesNotContain("PackedValue", dump);
        Assert.Contains("Fields: R, G, B, A", Fails(() => Read("Tint.PackedValue")));
    }

    [Fact]
    public void Colour_writes_in_the_printed_form_and_keeps_channels_left_out()
    {
        Write("Tint", "\"{R: 255, G: 0, B: 0}\"");
        Assert.Equal(new ColorSRGB(255, 0, 0, 255), _sample.Tint);

        Write("Tint", "{\"A\": 128}");
        Assert.Equal(new ColorSRGB(255, 0, 0, 128), _sample.Tint);
        Assert.Contains("Fields: R, G, B, A", Fails(() => Write("Tint.PackedValue", "1")));
    }

    [Fact]
    public void Transform_prints_position_and_euler_degrees_without_the_quaternion()
    {
        _sample.Dummies[0].Transform = new RelativeTransformWithEulerHint(new Vector3(0.125f, 0.125f, 0.5f), Quaternion.Identity);

        string dump = Read();

        Assert.Contains("Dummies[0].Transform = {Position: {X: 0.125, Y: 0.125, Z: 0.5}, Euler: {X: 0, Y: 0, Z: 0}} (RelativeTransformWithEulerHint)", dump);
        Assert.DoesNotContain("Dummies[0].Transform.Transform", dump);
        Assert.DoesNotContain("Orientation", dump);
        Assert.DoesNotContain(".EulerHint", dump);
        Assert.Contains("Dummies[0].Transform.Transform = {Position: {X: 0.125, Y: 0.125, Z: 0.5}, Euler: {X: 0, Y: 0, Z: 0}} (RelativeTransform)", Read("Dummies[0].Transform.Transform"));
    }

    [Fact]
    public void Transform_wrapper_writes_in_the_printed_form_and_keeps_the_euler_hint()
    {
        Write("Dummies[0].Transform", "\"{Position: {X: 0.125, Y: 0.125, Z: 0.6458988}, Euler: {X: 90}}\"");

        var transform = _sample.Dummies[0].Transform;
        Assert.Equal(new Vector3(0.125f, 0.125f, 0.6458988f), transform.Transform.Position);
        Assert.Equal(new Vector3(90, 0, 0), transform.EulerHint);
        Assert.Equal(Quaternion.CreateFromEuler(new Vector3(MathHelper.ToRadians(90), 0, 0)), transform.Transform.Orientation);
        Assert.Contains("Euler: {X: 90, Y: 0, Z: 0}", Read("Dummies[0].Transform"));
    }

    [Fact]
    public void Transform_wrapper_takes_euler_and_position_paths()
    {
        Write("Dummies[0].Transform.Euler.Z", "30");
        Write("Dummies[0].Transform.Position", "\"{Z: 2}\"");

        var transform = _sample.Dummies[0].Transform;
        Assert.Equal(new Vector3(0, 0, 2), transform.Transform.Position);
        Assert.Equal(new Vector3(0, 0, 30), transform.EulerHint);
        Assert.Equal("Dummies[0].Transform.Euler = {X: 0, Y: 0, Z: 30} (Euler degrees)", Read("Dummies[0].Transform.Euler"));
        Assert.Contains("Fields: Position, Euler", Fails(() => Write("Dummies[0].Transform.Rotation", "1")));
    }

    [Fact]
    public void Transform_writes_in_the_printed_form_and_keeps_the_euler_hint()
    {
        Write("Dummies[0].Transform.Transform", "\"{Position: {X: 0.125, Y: 0.125, Z: 0.6458988}, Euler: {X: 90}}\"");

        var transform = _sample.Dummies[0].Transform;
        Assert.Equal(new Vector3(0.125f, 0.125f, 0.6458988f), transform.Transform.Position);
        Assert.Equal(new Vector3(90, 0, 0), transform.EulerHint);
        Assert.Equal(Quaternion.CreateFromEuler(new Vector3(MathHelper.ToRadians(90), 0, 0)), transform.Transform.Orientation);
        Assert.Contains("Euler: {X: 90, Y: 0, Z: 0}", Read("Dummies[0].Transform.Transform"));
    }

    [Fact]
    public void Transform_takes_json_and_keeps_parts_left_out()
    {
        Write("Dummies[0].Transform.Transform", "{\"Euler\": {\"Y\": 45}}");
        Write("Dummies[0].Transform.Transform", "{\"Position\": {\"Z\": 2}}");

        var transform = _sample.Dummies[0].Transform;
        Assert.Equal(new Vector3(0, 0, 2), transform.Transform.Position);
        Assert.Equal(new Vector3(0, 45, 0), transform.EulerHint);
    }

    [Fact]
    public void Transform_with_a_bad_part_changes_nothing()
    {
        Assert.Contains("Position, Euler", Fails(() => Write("Dummies[0].Transform.Transform", "\"{Position: {Z: 1}, Rotation: {X: 1}}\"")));
        Assert.Equal(Vector3.Zero, _sample.Dummies[0].Transform.Transform.Position);
    }

    [Fact]
    public void Transform_cannot_be_written_null()
    {
        Assert.Contains("cannot be null", Fails(() => Write("Dummies[0].Transform.Transform", "null")));
    }

    [Fact]
    public void Transform_euler_reads_and_writes_by_path()
    {
        Write("Dummies[0].Transform.Transform.Euler", "\"{X: 90}\"");
        Write("Dummies[0].Transform.Transform.Euler.Z", "30");

        var transform = _sample.Dummies[0].Transform;
        Assert.Equal(new Vector3(90, 0, 30), transform.EulerHint);
        Assert.Equal(Quaternion.CreateFromEuler(new Vector3(MathHelper.ToRadians(90), 0, MathHelper.ToRadians(30))), transform.Transform.Orientation);
        Assert.Equal("Dummies[0].Transform.Transform.Euler = {X: 90, Y: 0, Z: 30} (Euler degrees)", Read("Dummies[0].Transform.Transform.Euler"));
        Assert.Equal("Dummies[0].Transform.Transform.Euler.X = 90 (float, degrees)", Read("dummies[0].transform.transform.euler.x"));
    }

    [Fact]
    public void Transform_euler_takes_json_and_refuses_other_axes()
    {
        Write("Dummies[0].Transform.Transform.Euler", "{\"Y\": 45}");
        Assert.Equal(new Vector3(0, 45, 0), _sample.Dummies[0].Transform.EulerHint);
        Assert.Contains("Fields: X, Y, Z", Fails(() => Write("Dummies[0].Transform.Transform.Euler.W", "1")));
    }

    [Fact]
    public void Transform_quaternion_is_not_addressable_and_errors_name_euler()
    {
        string read = Fails(() => Read("Dummies[0].Transform.Transform.Orientation"));
        Assert.Contains("Fields: Position, Euler", read);
        Assert.DoesNotContain("Orientation,", read);
        Assert.Contains("Fields: Position, Euler", Fails(() => Write("Dummies[0].Transform.Transform.Orientation", "\"{X: 90}\"")));
        Assert.Equal(Quaternion.Identity, _sample.Dummies[0].Transform.Transform.Orientation);
    }

    [Fact]
    public void Transform_parts_stay_writable_by_path()
    {
        Write("Dummies[0].Transform.Transform.Position", "\"{X: 1}\"");
        Assert.Equal(new Vector3(1, 0, 0), _sample.Dummies[0].Transform.Transform.Position);
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
