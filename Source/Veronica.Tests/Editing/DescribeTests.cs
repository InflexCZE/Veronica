using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Units;
using Keen.VRage.ContentPipeline.Definitions.Utility;
using Keen.VRage.Core.Model;
using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

/// <summary>
/// A machine that pushes things, see <see cref="T:Veronica.Tests.Editing.Pusher"/>.
/// </summary>
[Serialize]
public partial class Machine
{
    /// <summary>
    /// How far it reaches.<br/>Measured from the block's centre.
    /// </summary>
    [Unit(Unit.Meters)]
    public float Reach = 5;

    /// <summary>
    /// What it does when idle.
    /// </summary>
    public Mode Idle;

    /// <summary>
    /// The parts it is made of.
    /// </summary>
    public List<Part> Parts = [new Wheel()];

    /// <summary>
    /// Its settings.
    /// </summary>
    public Settings Setup = new();

    /// <summary>
    /// The dummy type it places.
    /// </summary>
    public DummyTypeDefinition? Dummy;

    /// <summary>
    /// The main part.
    /// </summary>
    public Part Main = new Wheel();

    public int Undocumented;

    public int Total => Reach > 0 ? 1 : 0;
}

/// <summary>
/// A machine that also lifts.
/// </summary>
[Serialize]
public partial class Lifter : Machine
{
    /// <summary>
    /// How much it lifts.
    /// </summary>
    [Mass]
    public float Load;
}

/// <summary>
/// One part of a machine.
/// </summary>
[Serialize]
public abstract partial class Part
{
    /// <summary>
    /// The part's name.
    /// </summary>
    public string Name = "";
}

/// <summary>
/// A part that rolls.
/// </summary>
[Serialize]
public partial class Wheel : Part
{
    /// <summary>
    /// Wheel size.
    /// </summary>
    [Length]
    public float Radius = 1;
}

[Serialize]
public partial class Gear : Part
{
    public int Teeth = 12;
}

[Serialize]
public partial class Settings
{
    /// <summary>
    /// Speed when running.
    /// </summary>
    public float Speed;

    public int Plain;
}

public sealed class FakeCatalog : ITypeCatalog
{
    public MemberReference? Fields(Type type) => TypeCatalog.Instance(type);

    public IReadOnlyList<Type> ConcreteTypes(Type type) =>
        typeof(FakeCatalog).Assembly.GetTypes().Where(t => type.IsAssignableFrom(t) && !t.IsAbstract && t != type).ToList();

    public Type? DefinitionOf(Type component) => component == typeof(Machine) ? typeof(DummyTypeDefinition) : null;

    public IReadOnlyList<Type> ComponentsTaking(Type definition) => definition == typeof(DummyTypeDefinition) ? [typeof(Machine)] : [];

    public Type Shown(Type type) => TypeCatalog.Shown(type);

    public string? Summary(Type type) => TypeCatalog.Summary(type);
}

public class DescribeTests
{
    private static readonly FakeCatalog Catalog = new();

    [Fact]
    public void ACrefIsItsShortNameAndABreakANewLine()
    {
        Assert.Equal("Uses ThrusterComponent.", DocText.Render("Uses <see cref=\"T:Keen.Game2.ThrusterComponent\"/>."));
        Assert.Equal("First.\nSecond.", DocText.Render("First.<br/>Second."));
        Assert.Equal("null when none.", DocText.Render("<see langword=\"null\"/> when none."));
        Assert.Null(DocText.Render("  "));
        Assert.Equal("Definition for Block.", DocText.Render("Definition for <see cref=\"T:A.Block\"/>.<br/><see href=\"https://x\"><b>Game | Block</b></see><br/><seealso href=\"https://y\"><b>SE2 | Effects</b></seealso><br/>"));
        Assert.Equal("A single effect (ParticleEffectDefinition) or a set.", DocText.Render("A single effect (<see cref=\"T:A.ParticleEffectDefinition\"/>) or a set."));
    }

    [Fact]
    public void AListingNamesMembersWithoutTheirSummaries()
    {
        string text = Describer.OfType(typeof(Machine), "", Catalog);
        Assert.StartsWith("Machine\nA machine that pushes things, see Pusher.", text);
        Assert.Contains("  Reach: float, m", text);
        Assert.Contains("  Parts: list of Part", text);
        Assert.Contains("  Dummy: ref DummyTypeDefinition", text);
        Assert.Contains("  Idle: Mode", text);
        Assert.DoesNotContain("How far it reaches", text);
        Assert.DoesNotContain("Total", text);
    }

    [Fact]
    public void RelationsAreListed()
    {
        string text = Describer.OfType(typeof(Machine), "", Catalog);
        Assert.Contains("Derived: Lifter", text);
        Assert.Contains("Takes definition: DummyTypeDefinition", text);
    }

    [Fact]
    public void InheritedFieldsAreGroupedByTheirBase()
    {
        string text = Describer.OfType(typeof(Lifter), "", Catalog);
        Assert.Contains("Bases: Machine", text);
        Assert.Contains("Fields:\n  Load: float, kg\nFields from Machine:\n  Reach: float, m", text);
    }

    [Fact]
    public void AMemberShowsItsSummaryThenItsTypesMembers()
    {
        string text = Describer.OfType(typeof(Machine), "Setup", Catalog);
        Assert.StartsWith("Machine:\nSetup: Settings\nIts settings.\n\nSettings\nFields:\n  Speed: float", text);
    }

    [Fact]
    public void AScalarMemberStopsAfterItsSummary() =>
        Assert.Equal("Machine:\nReach: float, m\nHow far it reaches.\nMeasured from the block's centre.", Describer.OfType(typeof(Machine), "reach", Catalog));

    [Fact]
    public void AnEnumMemberListsItsValues() =>
        Assert.Equal("Machine:\nIdle: Mode\nWhat it does when idle.\nValues: Forward, Backward", Describer.OfType(typeof(Machine), "Idle", Catalog));

    [Fact]
    public void AnUndocumentedMemberHasNoSummaryLine() =>
        Assert.Equal("Machine:\nUndocumented: int", Describer.OfType(typeof(Machine), "Undocumented", Catalog));

    [Fact]
    public void AnAbstractElementTypeListsItsConcreteTypes()
    {
        string text = Describer.OfType(typeof(Machine), "Parts", Catalog);
        Assert.Contains("The parts it is made of.", text);
        Assert.Contains("Part (abstract)\nOne part of a machine.\nDerived: Gear, Wheel\nIts fields depend on the concrete type", text);
    }

    [Fact]
    public void OnATypeAnIndexWalksTheDeclaredElementType()
    {
        var e = Assert.Throws<EditError>(() => Describer.OfType(typeof(Machine), "Parts[0].Name", Catalog));
        Assert.Contains("'Parts[0]' holds a Part, which is abstract; describe one of its types: Gear, Wheel.", e.Message);
    }

    [Fact]
    public void OnAValueAnIndexIsTheElementThere()
    {
        var root = new ObjectManipulator(new Machine()).Root;
        Assert.StartsWith("Parts[0]: Wheel\nWheel\nA part that rolls.\nBases: Part\nFields:\n  Radius: float, m\nFields from Part:\n  Name: string",
            Describer.OfValue(root, "Parts[0]", Catalog, new FakeLinks()));
        Assert.Equal("Parts[0].Radius: float, m\nWheel size.", Describer.OfValue(root, "Parts[0].Radius", Catalog, new FakeLinks()));
    }

    [Fact]
    public void OnAValueAMissingIndexIsReadsError()
    {
        var root = new ObjectManipulator(new Machine()).Root;
        var e = Assert.Throws<EditError>(() => Describer.OfValue(root, "Parts[3]", Catalog, new FakeLinks()));
        Assert.Contains("has 1 element", e.Message);
    }

    [Fact]
    public void AReferenceMemberListsTheReferencedType()
    {
        string text = Describer.OfType(typeof(Machine), "Dummy", Catalog);
        Assert.StartsWith("Machine:\nDummy: ref DummyTypeDefinition\nThe dummy type it places.\n\nDummyTypeDefinition", text);
        Assert.Contains("Taken by components: Machine", text);
    }

    [Fact]
    public void AStepThroughAReferenceIsRefused()
    {
        var e = Assert.Throws<EditError>(() => Describer.OfType(typeof(Machine), "Dummy.Name", Catalog));
        Assert.Contains("describe DummyTypeDefinition for its fields", e.Message);
    }

    [Fact]
    public void ALoneComponentBuilderShowsOnlyWhatAPrefabShows()
    {
        string text = Describer.Listing(typeof(PartBuilder), TypeCatalog.Instance(typeof(PartBuilder), inPrefab: true), Catalog);
        Assert.Contains("  Shown: float", text);
        Assert.DoesNotContain("Internal", text);
        Assert.Contains("Internal", Describer.Listing(typeof(PartBuilder), TypeCatalog.Instance(typeof(PartBuilder)), Catalog));
    }

    [Fact]
    public void AnUndocumentedFieldInADocumentedOneHasNoSummary() =>
        Assert.Equal("Machine:\nSetup.Plain: int", Describer.OfType(typeof(Machine), "Setup.Plain", Catalog));

    [Fact]
    public void AnImmutableMergeableListIsTheListItWraps()
    {
        Assert.Contains("Frozen: list of Entry", Describer.OfType(typeof(Sample), "Frozen", Catalog));
        Assert.Contains("Frozen[0].Speed: float", Describer.OfType(typeof(Sample), "Frozen[0].Speed", Catalog));
    }

    [Fact]
    public void ATransformIsItsPositionAndEulerAngles()
    {
        string listing = Describer.OfType(typeof(Sample), "Dummies[0].Transform", Catalog);
        Assert.Contains("  Euler: Euler angles, degrees", listing);
        Assert.DoesNotContain("Quaternion", listing);
        Assert.Contains("Dummies[0].Transform.Euler.Y: float, degrees", Describer.OfType(typeof(Sample), "Dummies[0].Transform.Euler.Y", Catalog));
        var root = new ObjectManipulator(new Sample()).Root;
        Assert.StartsWith("Dummies[0].Transform.Euler: Euler angles, degrees", Describer.OfValue(root, "Dummies[0].Transform.Euler", Catalog, new FakeLinks()));
    }

    [Fact]
    public void AStepPastAListedReferenceIsRefused()
    {
        var e = Assert.Throws<EditError>(() => Describer.OfType(typeof(Sample), "Types[0].Name", Catalog));
        Assert.Contains("'Types[0]' is a reference to another item; describe DummyTypeDefinition", e.Message);
    }

    [Fact]
    public void ADictionaryEntryTakesTheDotForm() =>
        Assert.Contains("Classes[Ion].Speed: float", Describer.OfType(typeof(Sample), "Classes.Ion.Speed", Catalog));

    [Fact]
    public void OnATypeADerivedDefaultIsListedAsTheDeclaredType()
    {
        string text = Describer.OfType(typeof(Machine), "Main", Catalog);
        Assert.Contains("Part (abstract)", text);
        Assert.Contains("Its fields depend on the concrete type", text);
        Assert.DoesNotContain("Radius", text);
    }

    [Fact]
    public void AnUnknownMemberListsTheFields()
    {
        var e = Assert.Throws<EditError>(() => Describer.OfType(typeof(Machine), "Speed", Catalog));
        Assert.Contains("'Speed' is not a field of the definition. Fields: Reach, Idle, Parts, Setup, Dummy, Main, Undocumented", e.Message);
    }

    [Fact]
    public void FixedPointIsDescribedAsADecimalNumberNotItsStorage()
    {
        string text = Describer.OfType(typeof(FixedPoint), "", Catalog);
        Assert.StartsWith("FixedPoint\nA decimal number", text);
        Assert.DoesNotContain("RawValue", text);
        Assert.DoesNotContain("64-bit", text);
    }

    [Fact]
    public void AFixedPointMemberSaysHowItIsWritten()
    {
        string text = Describer.OfType(typeof(Bounded), "Charge", Catalog);
        Assert.StartsWith("Bounded:\nCharge: FixedPoint", text);
        Assert.Contains("like 0.5 or 2150400", text);
        Assert.DoesNotContain("RawValue", text);
    }

    [Fact]
    public void AnOverrideAddsAUnitTheGameLeavesOut()
    {
        string text = Describer.OfType(typeof(Keen.Game2.Simulation.WorldObjects.Items.InventoryDefinition), "MaxMass", Catalog);
        Assert.StartsWith("InventoryDefinition:\nMaxMass: FixedPoint, kg", text);
    }
}
