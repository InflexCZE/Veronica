using Keen.VRage.ContentPipeline.Definitions.Utility;
using Keen.VRage.Library.Collections;
using Keen.VRage.Core.Model;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Localization;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.Validation;
using Keen.VRage.Library.UI.PropertyInspector.Visibility;
using Keen.VRage.Library.Utils;
using Veronica.Editing;

namespace Veronica.Tests.Editing;

public enum Mode
{
    Forward,
    Backward,
}

[Serialize]
public partial class Sample
{
    public string? Title = "Placeholder";
    public float Power = 10000;
    public int Count = 3;
    public bool Enabled;
    public Mode Mode;
    public Vector3 Scale = Vector3.One;
    public ColorSRGB Tint = new(0, 255, 255, 255);
    public LocKey Label;
    public StringId ThrustClass;
    public List<ModelDummy> Dummies = [new ModelDummy { Name = "Nozzle" }];
    public Dictionary<string, Entry> Classes = new() { ["Ion"] = new Entry { Speed = 1 }, ["Hydrogen"] = new Entry { Speed = 2 } };
    public Dictionary<DummyTypeDefinition, Entry> ByType = [];
    public ResourceHandle<ModelAsset> Model;
    public DummyTypeDefinition? DummyType;
    public Inner? Child = new();
    public Inner? Missing;
    public List<Shape> Shapes = [];
    public List<int> Numbers = [0, 1, 2, 3, 4, 5, 6, 7];
    public Vector3? Offset;
    public List<DummyTypeDefinition?> Types = [];
    public Entry Required = new();
    public Shape? Form;
    public MergeableList<ModelDummy> Merged = new();
    public ImmutableMergeableList<Entry> Frozen = ImmutableMergeableList<Entry>.Empty;
    public KeyedList<string, Entry> Named = new();
    public System.Collections.Immutable.ImmutableArray<Entry> Unset;

    public Entry Settings { get; } = new();

    public Vector3 Fixed { get; } = Vector3.One;

    [HideInInspector]
    public int Hidden;
}

// Stands in for the engine type of the same name: forced fields are matched by type and field name.
[Serialize]
public partial class ParticleEffectDefinitionObjectBuilder
{
    [HideInInspector]
    public List<Entry> ParticleEmitters = [new Entry { Speed = 1 }];

    [HideInInspector]
    public int Internal;
}

[Serialize]
public partial class Entry
{
    public float Speed;
    public string? Note;
}

[Serialize]
public partial class Inner
{
    public Level2 Next = new();
    public int Depth = 1;
}

[Serialize]
public partial class Level2
{
    public Level3 Next = new();
    public int Depth = 2;
}

[Serialize]
public partial class Level3
{
    public Level4 Next = new();
    public int Depth = 3;
}

[Serialize]
public partial class Level4
{
    public int Depth = 4;
}

[Serialize]
public abstract partial class Shape;

[Serialize]
public partial class Box : Shape
{
    public float Size = 1;
}

[Serialize]
public partial class Ball : Shape
{
    public float Radius = 1;
}

[Serialize]
public partial class Bounded
{
    [Range<float>(0, 50_000, RangeMode.ExIn)]
    public float Reach = 100;

    [FixedPointRange(0, nameof(Discharge), RangeMode.InEx)]
    public FixedPoint Charge = (FixedPoint)1;

    [FixedPointRange(nameof(Charge), float.MaxValue, RangeMode.ExIn)]
    public FixedPoint Discharge = (FixedPoint)3;

    [StringLength(1, 8)]
    public string Label = "Short";

    [UnitVector]
    public Vector3 Direction = Vector3.Forward;

    [DefinitionTestRange(0, "Limit")]
    public float Speed = 1;

    [Range<int>(0, 10)]
    public int? Optional;
}

// Stands in for the game's DefinitionRange, named like it: the bound sits on a definition found through IDefinitionLocatorStrategy.
public sealed class DefinitionTestRangeAttribute(float min, string maxMember) : BaseRangeAttribute<float, (ValidationContext, MemberReference)>(min, maxMember)
{
    protected override Keen.VRage.Library.Threading.Task<(ValidationContext, MemberReference)> GetRangeLimitsContext(ValidationContext context, MemberReference member) =>
        Keen.VRage.Library.Threading.Task.FromResult((context, member));

    protected override MemberReference GetRangeLimitMember((ValidationContext, MemberReference) context, string identifier) =>
        context.Item1.Get<IDefinitionLocatorStrategy>().GetDefinitionMember(context.Item2)[identifier];
}

[Serialize]
public partial class OwnerDefinition
{
    public float Limit = 5;
}

// The project side of references, over a fixed set of items.
public sealed class FakeLinks : IContentLinks
{
    public static readonly Guid Thruster = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Conveyor = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Engine = new("33333333-3333-3333-3333-333333333333");

    private readonly Dictionary<Guid, string> _paths = new()
    {
        [Thruster] = @"System\DummyTypes\Thruster.def",
        [Conveyor] = @"System\DummyTypes\Conveyor.def",
        [Engine] = @"Mods\Thruster\Engine.model",
    };

    public string? PathOf(Guid id) => _paths.GetValueOrDefault(id);

    public ContentLink Find(string name, ReferenceSpec spec)
    {
        bool definition = spec.Kind == ReferenceKind.Definition;
        var matches = _paths.Where(p => ContentNames.Matches(p.Value, name) && p.Value.EndsWith(".def") == definition).ToList();
        return matches.Count == 1
            ? new ContentLink(matches[0].Key, matches[0].Value)
            : throw new EditError($"No single item named '{name}'.");
    }

    public Definition CreateDefinitionReference(Guid id) => DefinitionAccessHelper.CreatePlaceholder(typeof(DummyTypeDefinition), id);
}
