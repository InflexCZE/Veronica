using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Units;

namespace Veronica.Editing;

// describe: what a type and its members mean, from the game's own doc comments. Listings name members only;
// a member's summary comes from asking for that member.
public static class Describer
{
    public const int MaxLines = FieldReader.MaxLines;
    public const int MaxDerived = 15;

    // A type named by the model. Without values, a step walks the declared type and an index the element type.
    public static string OfType(Type type, string member, ITypeCatalog catalog)
    {
        type = catalog.Shown(type);
        var segments = FieldPath.Parse(member);
        if (segments.Count == 0)
            return Listing(type, catalog.Fields(type), catalog);

        Type current = type;
        MemberReference? node = catalog.Fields(type);
        MemberReference? last = null;
        string canonical = "";
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            // A dictionary entry is [key] or .key, as in read.
            bool element = segment.Bracketed || last is { MemberKind: MemberValueKind.Dictionary };
            if (element)
            {
                if (last is not { } collection || !IsCollection(collection))
                    throw new EditError($"{Where(canonical)} is not a list or dictionary; remove the [{segment.Text}].");
                if (collection.MemberKind == MemberValueKind.Set)
                    throw new EditError($"'{canonical}' is a set; its items cannot be addressed.");
                current = ElementType(collection) is { } e ? Nullable.GetUnderlyingType(e) ?? e : throw new EditError($"The element type of '{canonical}' cannot be read.");
                canonical = FieldPath.Element(canonical, segment.Text);
                node = Fields.IsScalar(current) ? null : catalog.Fields(current);
                last = null;
                continue;
            }

            if (last is { } previous)
            {
                if (IsCollection(previous))
                    throw new EditError(previous.MemberKind == MemberValueKind.Set
                        ? $"'{canonical}' is a set; its items cannot be addressed."
                        : $"'{canonical}' is a list; address an element like '{canonical}[0]'.");
                if (Fields.GetReference(previous) != null)
                    throw ReferenceStep(canonical, ReferencedType(previous));
            }
            else if (canonical.Length > 0 && Fields.IsScalar(current))
            {
                if (typeof(Definition).IsAssignableFrom(current))
                    throw ReferenceStep(canonical, current);
                throw new EditError($"'{canonical}' is a {Name(current)} and has no fields.");
            }
            if (node is not { } parent)
                throw NoFields(current, canonical, catalog);

            if (EulerStep(parent, canonical, segments, i) is { } euler)
                return $"{Name(type)}:\n{euler}";

            var child = FieldWalker.Child(parent, canonical, segment.Text);
            canonical = FieldPath.Member(canonical, child.Name);
            child = Unwrap(child);
            last = child;
            current = Fields.ValueType(child);
            node = Nested(child);
            // On a type a step walks the declared type, even where the default holds a derived one.
            if (node is { } value && value.CurrentMemberType != current && !current.IsValueType)
                node = null;
            node ??= catalog.Fields(current);
        }

        return $"{Name(type)}:\n" + (last is { } found
            ? Member(found, canonical, current, node, catalog, actual: false)
            : Cap([$"{canonical}: {Name(current)}", Listing(current, node, catalog)]));
    }

    private static EditError ReferenceStep(string path, Type? referenced) =>
        new($"'{path}' is a reference to another item; describe {(referenced != null ? Name(referenced) : "that item")} for its fields.");

    private static MemberReference Unwrap(MemberReference member)
    {
        try
        {
            return Fields.Unwrap(member);
        }
        catch (Exception)
        {
            return member;
        }
    }

    // A transform's angles are its Euler path, as in read and write; the quaternion beneath is not a field.
    private static string? EulerStep(MemberReference transform, string path, IReadOnlyList<PathSegment> segments, int at)
    {
        if (segments[at].Bracketed || !string.Equals(segments[at].Text, Transforms.EulerName, StringComparison.OrdinalIgnoreCase)
            || !Fields.TryGetTransform(transform, out _, out _))
            return null;
        string euler = FieldPath.Member(path, Transforms.EulerName);
        if (at == segments.Count - 1)
            return EulerText(euler, null);
        string axis = segments[at + 1].Text.ToUpperInvariant();
        if (at + 2 < segments.Count || segments[at + 1].Bracketed || axis is not ("X" or "Y" or "Z"))
            throw new EditError($"'{segments[at + 1].Text}' is not a field of '{euler}'. Fields: X, Y, Z.");
        return EulerText(FieldPath.Member(euler, axis), axis);
    }

    private static string EulerText(string path, string? axis) => axis == null
        ? $"{path}: Euler angles, degrees\nThe rotation as angles about X, Y and Z, as the inspector shows it."
        : $"{path}: float, degrees\nThe rotation's angle about {axis}.";

    // A content item's values: the path walks what is there, so a polymorphic element is the type it holds.
    public static string OfValue(MemberReference root, string member, ITypeCatalog catalog, IContentLinks links, string basePath = "")
    {
        if (FieldPath.Parse(member).Count == 0)
            return Listing(catalog.Shown(root.CurrentMemberType), root, catalog);

        if (Transforms.WalkEuler(root, member, basePath, links) is { } euler)
            return EulerText(euler.Path, euler.Axis);

        var (found, canonical) = FieldWalker.Walk(root, member, basePath, links);
        bool hasFields = IsObject(found) && found.GetValue<object?>() != null;
        if (found.IsCollectionItem)
        {
            Type type = catalog.Shown(hasFields ? found.CurrentMemberType : Fields.ValueType(found));
            return Cap([$"{canonical}: {Name(type)}", Listing(type, hasFields ? found : catalog.Fields(type), catalog)]);
        }
        return Member(found, canonical, Fields.ValueType(found), hasFields ? found : null, catalog, actual: true);
    }

    public static string Listing(Type type, MemberReference? fields, ITypeCatalog catalog)
    {
        if (Values.PlainValueText(type) is { } plain)
            return $"{Name(type)}\n{plain}";

        var lines = new List<string> { type.IsAbstract && !type.IsSealed ? $"{Name(type)} (abstract)" : Name(type) };
        if (catalog.Summary(type) is { } summary)
            lines.Add(summary);
        lines.AddRange(Relations(type, catalog));

        if (type.IsEnum)
        {
            lines.Add($"Values: {string.Join(", ", Enum.GetNames(type))}");
            return string.Join("\n", lines);
        }

        if (fields is not { } root)
        {
            if (type.IsAbstract || type.IsInterface)
                lines.Add("Its fields depend on the concrete type; describe one of the types above.");
            return string.Join("\n", lines);
        }

        lines.AddRange(FieldGroups(root, catalog));
        return Cap(lines);
    }

    // read's cap: a reply stops at MaxLines with a notice.
    private static string Cap(IEnumerable<string> parts)
    {
        var lines = string.Join("\n", parts).Split('\n').ToList();
        if (lines.Count <= MaxLines + 1)
            return string.Join("\n", lines);
        return string.Join("\n", lines.Take(MaxLines).Append($"... output cut at {MaxLines} lines; describe a member for the rest."));
    }

    // One line per field: name, type, unit, and read-only where read marks it.
    public static string Line(MemberReference member, string name, bool actual)
    {
        string line = $"{name}: {Label(member, actual)}";
        if (UnitOf(member) is { } unit)
            line += $", {unit}";
        if (member.MemberKind == MemberValueKind.Value && Fields.IsScalar(Fields.ValueType(member)) && !Fields.IsWritable(member))
            line += " (read-only)";
        return line;
    }

    public static string? Summary(MemberReference member)
    {
        if (member.IsRoot || member.IsCollectionItem)
            return null;
        if (Doc(member) is { } doc)
            return doc;
        // An immutable mergeable list is read through the list it wraps; the doc sits on the wrapping field.
        var parent = member.GetParent();
        return !parent.IsRoot && parent.IsMember && IsMergeableWrapper(parent.DeclaredMemberType) ? Doc(parent) : null;
    }

    private static bool IsMergeableWrapper(Type type)
    {
        Type t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Keen.VRage.Library.Collections.ImmutableMergeableList<>);
    }

    private static string? Doc(MemberReference member)
    {
        if (OverrideOf(member)?.Summary is { } corrected)
            return corrected;
        try
        {
            var info = member.GetParent().CurrentTypeInfo;
            return info != null && info.MembersDocumentation.TryGetValue(member.MemberInfo, out string? xml) ? DocText.Render(xml) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Member(MemberReference member, string path, Type type, MemberReference? node, ITypeCatalog catalog, bool actual)
    {
        var lines = new List<string> { Line(member, path, actual) };
        if (Summary(member) is { } summary)
            lines.Add(summary);

        Type? next = null;
        MemberReference? fields = null;
        if (IsCollection(member))
        {
            next = ElementType(member) is { } element ? Nullable.GetUnderlyingType(element) ?? element : null;
            if (next != null)
                fields = catalog.Fields(next);
        }
        else if (Fields.GetReference(member) is { Kind: ReferenceKind.Definition })
        {
            next = ReferencedType(member);
            if (next != null)
                fields = catalog.Fields(next);
        }
        else if (Fields.GetReference(member) == null)
        {
            next = actual && node != null ? node.Value.CurrentMemberType : type;
            fields = node ?? catalog.Fields(next);
        }

        if (next == null || Fields.GetReference(member) is { Kind: not ReferenceKind.Definition })
            return string.Join("\n", lines);
        if (next.IsEnum)
        {
            lines.Add($"Values: {string.Join(", ", Enum.GetNames(next))}");
            return string.Join("\n", lines);
        }
        if (Fields.IsScalar(next) && !typeof(Definition).IsAssignableFrom(next))
        {
            if (Values.PlainValueText(next) is { } plain)
                lines.Add(plain);
            return string.Join("\n", lines);
        }

        lines.Add("");
        lines.Add(Listing(catalog.Shown(next), fields, catalog));
        return Cap(lines);
    }

    private static IEnumerable<string> Relations(Type type, ITypeCatalog catalog)
    {
        var bases = new List<string>();
        for (Type? b = type.BaseType; b != null && b != typeof(object) && b != typeof(ValueType) && b != typeof(Enum); b = b.BaseType)
            bases.Add(Name(b));
        if (bases.Count > 0)
            yield return $"Bases: {string.Join(", ", bases)}";

        var derived = catalog.ConcreteTypes(type).Where(t => t != type).Select(Name).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (derived.Count > 0)
        {
            string shown = string.Join(", ", derived.Take(MaxDerived));
            yield return $"Derived: {shown}{(derived.Count > MaxDerived ? $", ... and {derived.Count - MaxDerived} more" : "")}";
        }
        else if (type.IsAbstract && !type.IsSealed && !type.IsInterface)
        {
            yield return "Derived: none the Editor offers";
        }

        if (catalog.DefinitionOf(type) is { } definition)
            yield return $"Takes definition: {Name(definition)}";
        var components = catalog.ComponentsTaking(type).Select(Name).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (components.Count > 0)
            yield return $"Taken by components: {string.Join(", ", components)}";
    }

    // The type's own fields, then one group per base type, nearest first.
    private static IEnumerable<string> FieldGroups(MemberReference root, ITypeCatalog catalog)
    {
        // A transform's fields are its position and its Euler angles, as read prints it; never the quaternion.
        if (Fields.TryGetTransform(root, out var position, out _))
        {
            yield return "Fields:";
            yield return "  " + Line(position, position.Name, actual: true);
            yield return $"  {Transforms.EulerName}: Euler angles, degrees";
            yield break;
        }

        var chain = new List<Type>();
        for (Type? t = root.CurrentMemberType; t != null; t = t.BaseType)
            chain.Add(t);

        var groups = new SortedDictionary<int, (Type Owner, List<string> Lines)>();
        foreach (var visible in Fields.VisibleMembers(root))
        {
            var field = Fields.Unwrap(visible);
            if (Fields.IsComputed(field))
                continue;
            Type owner = visible.MemberInfo.DeclaringType ?? root.CurrentMemberType;
            int index = chain.IndexOf(owner);
            if (index < 0)
                index = chain.Count;
            if (!groups.TryGetValue(index, out var group))
                groups[index] = group = (owner, []);
            group.Lines.Add("  " + Line(field, visible.Name, actual: true));
        }

        if (groups.Count == 0)
        {
            yield return "Fields: none";
            yield break;
        }

        bool first = true;
        foreach (var (_, (owner, lines)) in groups)
        {
            yield return first ? "Fields:" : $"Fields from {Name(catalog.Shown(owner))}:";
            first = false;
            foreach (var line in lines)
                yield return line;
        }
    }

    private static string Label(MemberReference member, bool actual)
    {
        if (Fields.GetReference(member) is { } reference)
        {
            return reference.Kind switch
            {
                ReferenceKind.Definition => $"ref {Name(ReferencedType(member)!)}",
                ReferenceKind.Handle => Fields.ValueType(member) is { IsGenericType: true, GenericTypeArguments: [var asset] } ? $"ref {Fields.TypeLabel(asset)}" : "ref asset",
                _ => "ref asset",
            };
        }

        if (IsCollection(member))
        {
            string element = ElementType(member) is { } e ? ElementLabel(e) : "?";
            if (member.MemberKind == MemberValueKind.Dictionary && Fields.DictionaryTypes(CollectionType(member)) is { } types)
                return $"dictionary of {ElementLabel(types.Key)} to {element}";
            return $"{Kind(member)} of {element}";
        }

        Type declared = member.DeclaredMemberType;
        string label = Fields.TypeLabel(Shown(declared));
        if (!actual)
            return label;
        Type current = member.CurrentMemberType;
        // Polymorphic fields show what they hold, as read does.
        return current != declared && Nullable.GetUnderlyingType(declared) != current ? $"{label}: {Fields.TypeLabel(Shown(current))}" : label;
    }

    private static string ElementLabel(Type type) =>
        typeof(Definition).IsAssignableFrom(Nullable.GetUnderlyingType(type) ?? type) ? $"ref {Name(Nullable.GetUnderlyingType(type) ?? type)}" : Fields.TypeLabel(Shown(type));

    private static Type Shown(Type type) => Nullable.GetUnderlyingType(type) is { } inner ? typeof(Nullable<>).MakeGenericType(TypeCatalog.Shown(inner)) : TypeCatalog.Shown(type);

    private static DocOverrides.Entry? OverrideOf(MemberReference member)
    {
        try
        {
            if (member.IsRoot || member.IsCollectionItem || member.MemberInfo.DeclaringType is not { } declaring)
                return null;
            return DocOverrides.For(declaring, TypeCatalog.Shown(declaring), member.Name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Type? ReferencedType(MemberReference member) =>
        Fields.GetReference(member) is { Kind: ReferenceKind.Definition, DefinitionTypes: [var first, ..] } ? first : null;

    private static string? UnitOf(MemberReference member)
    {
        if (OverrideOf(member)?.Unit is { } corrected)
            return corrected.Length == 0 ? null : corrected;
        try
        {
            return member.GetMemberAttribute<UnitAttribute>() is { } unit && Symbols.TryGetValue(unit.Unit, out string? symbol) ? symbol : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static readonly Dictionary<Unit, string> Symbols = new()
    {
        [Unit.Meters] = "m",
        [Unit.MetersPerSecond] = "m/s",
        [Unit.MetersPerSecondSquared] = "m/s^2",
        [Unit.CubicMeters] = "m^3",
        [Unit.Liters] = "L",
        [Unit.LitersPerSecond] = "L/s",
        [Unit.Radians] = "rad",
        [Unit.RadiansSecond] = "rad/s",
        [Unit.RadiansPerSecond] = "rad/s",
        [Unit.Degrees] = "degrees",
        [Unit.DegreesPerSecond] = "degrees/s",
        [Unit.DegreesPerSecondSquared] = "degrees/s^2",
        [Unit.Kilograms] = "kg",
        [Unit.Newtons] = "N",
        [Unit.NewtonsSecond] = "N*s",
        [Unit.NewtonsMeter] = "N*m",
        [Unit.Bytes] = "bytes",
        [Unit.Brightness] = "brightness",
        [Unit.RightAngles] = "right angles",
        [Unit.Hz] = "Hz",
        [Unit.GHz] = "GHz",
        [Unit.KilogramsPerCubicMeters] = "kg/m^3",
        [Unit.Seconds] = "s",
        [Unit.RevolutionsPerMinute] = "rpm",
        [Unit.Pixels] = "px",
        [Unit.Kilowatts] = "kW",
        [Unit.KilowattHours] = "kWh",
    };

    // A null object along a type walk is created on the throwaway instance, so its fields keep their place in it.
    private static MemberReference? Nested(MemberReference child)
    {
        if (!IsObject(child))
            return null;
        try
        {
            if (child.GetValue<object?>() is null)
            {
                Type type = Fields.ValueType(child);
                if (type.IsAbstract || type.IsInterface)
                    return null;
                child.SetNew(initializeNullableValueTypes: true);
            }
            return child.GetValue<object?>() is null ? null : child;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsObject(MemberReference member) =>
        member.MemberKind == MemberValueKind.Value && !Fields.IsScalar(Fields.ValueType(member)) && Fields.GetReference(member) == null;

    private static EditError NoFields(Type type, string path, ITypeCatalog catalog)
    {
        if (type.IsAbstract || type.IsInterface)
        {
            var choices = catalog.ConcreteTypes(type).Select(Name).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToList();
            return new EditError($"{Where(path)} holds a {Name(type)}, which is abstract; describe one of its types"
                + (choices.Count == 0 ? "." : $": {string.Join(", ", choices.Take(MaxDerived))}."));
        }
        return new EditError($"{Where(path)} is a {Name(type)} and has no fields.");
    }

    private static bool IsCollection(MemberReference member) =>
        member.MemberKind is MemberValueKind.List or MemberValueKind.Dictionary or MemberValueKind.Set;

    private static string Kind(MemberReference member) => member.MemberKind.ToString().ToLowerInvariant();

    private static Type CollectionType(MemberReference member)
    {
        try
        {
            return member.CurrentMemberType;
        }
        catch (Exception)
        {
            return member.DeclaredMemberType;
        }
    }

    private static Type? ElementType(MemberReference collection)
    {
        Type type = CollectionType(collection);
        if (collection.MemberKind == MemberValueKind.Dictionary)
            return Fields.DictionaryTypes(type)?.Value;
        if (Fields.ElementType(type) is { } element)
            return element;
        foreach (var candidate in type.GetInterfaces().Prepend(type))
        {
            if (candidate.IsGenericType && (candidate.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                || candidate.GetGenericTypeDefinition() == typeof(IReadOnlySet<>) || candidate.GetGenericTypeDefinition() == typeof(ISet<>)))
                return candidate.GenericTypeArguments[0];
        }
        return null;
    }

    private static string Name(Type type) => Fields.TypeLabel(type);

    private static string Where(string path) => path.Length == 0 ? "The type" : $"'{path}'";
}
