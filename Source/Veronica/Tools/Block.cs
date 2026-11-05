using System.Text.Json;
using Keen.VRage.ContentPipeline.Definitions;
using Keen.VRage.ContentPipeline.Definitions.AssetInterfaces;
using Keen.VRage.ContentPipeline.Game.Prefabs;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Utils;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Definitions;
using Keen.VRage.Core.Game.Editor.PrefabManipulators;
using Keen.VRage.Core.Game.Editor.PrefabManipulators.BoundEntities;
using Keen.VRage.Core.Game.Editor.PrefabManipulators.BoundEntities.Components;
using Keen.VRage.Core.Game.Editor.PrefabManipulators.EntityPrefab;
using Keen.VRage.DCS.Components;
using Keen.VRage.DCS.Definitions;
using Keen.VRage.DCS.Internal;
using Keen.VRage.Editor.Windows.PropertyInspector.PropertyElements.Custom.Definitions.Prefabs.Core;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Serialization.Validation;
using Keen.VRage.Library.Utils;
using Keen.VRage.UI.Shared.ObjectInspector;
using Keen.VRage.UI.Shared.ObjectInspector.Extensions;
using Veronica.Editing;

namespace Veronica.Tools;

// A block, or a prefab with no binding, edited as a whole through the Editor's own prefab layer, the one behind the
// inspector's prefab view: client and server pairing, dependency filling and the shared components' mirroring are its.
// Any file of the block opens it, so a model naming one of its prefabs edits the block the same way.
internal sealed class Block
{
    private const string DefinitionField = "Definition";
    private const string TagsField = "Tags";

    // The Editor sides a few types wrongly (an Editor bug); vanilla blocks show where they belong.
    private static readonly Dictionary<string, ComponentScope> SideOverrides = new()
    {
        ["Keen.Game2.Simulation.WorldObjects.Items.InventoryComponent"] = ComponentScope.Server,
        ["Keen.Game2.Simulation.WorldObjects.Items.ItemRequesterComponent"] = ComponentScope.Server,
        [PermissionsType] = ComponentScope.Server,
    };

    private const string PermissionsType = "Keen.Game2.Simulation.WorldObjects.CubeBlocks.BlockPermissionsComponent";

    // Required on a block with the type by game validation only, so dependency filling never adds them. Without
    // permissions the server also refuses every inventory or seat action players take on the block.
    private static readonly Dictionary<string, string> Companions = new()
    {
        ["Keen.Game2.Simulation.WorldObjects.Items.InventoryComponent"] = PermissionsType,
        ["Keen.Game2.Simulation.WorldObjects.CubeBlocks.Pilotable.SeatComponent"] = PermissionsType,
    };

    private readonly LoadedProject _project;
    private readonly string? _soleSide;

    // The binding, or the prefab when there is none; replies name it.
    public ExplorerAssetFile Item { get; }
    private IEntityNode Root { get; }

    private Block(LoadedProject project, ExplorerAssetFile item, IEntityNode root, string? soleSide)
    {
        _project = project;
        Item = item;
        Root = root;
        _soleSide = soleSide;
    }

    public static Block Open(LoadedProject project, string target)
    {
        var asset = Content.Resolve(project, target);
        return TryOpen(project, asset)
            ?? throw new EditError($"{Content.Describe(asset)} is not a block; give its prefab binding or one of its prefabs.");
    }

    // Null when the asset is not part of a prefab.
    public static Block? TryOpen(LoadedProject project, ExplorerAssetFile asset)
    {
        Type? type = DefinitionType(asset);
        bool binding = asset.IsPrefabBindingDefinition();
        if (!binding && type != typeof(PrefabDefinition) && type != typeof(EntityCompositeDefinition))
            return null;

        var bindingAsset = binding ? asset : project.TryGetPrefabBindingInProject(asset.AssetID);
        if (bindingAsset == null)
        {
            var prefab = type == typeof(PrefabDefinition) ? asset
                : project.TryGetPrefabGuidOfCompositeDefinition(asset.AssetID) is { } prefabId ? project.GetAssetFileByGuid(prefabId) : null;
            return prefab == null ? null : new Block(project, prefab, Manipulator(project).LoadPrefab(prefab.AssetID), null);
        }

        var pairing = bindingAsset.TryGetPairingAllowIncomplete();
        var client = pairing.clientPrefab is { } c ? project.GetAssetFileByGuid(c.Guid) : null;
        var server = pairing.serverPrefab is { } s ? project.GetAssetFileByGuid(s.Guid) : null;
        if (client != null && server != null)
            return new Block(project, bindingAsset, Manipulator(project).LoadBinding(client.AssetID, server.AssetID), null);
        if (client != null || server != null)
            return new Block(project, bindingAsset, Manipulator(project).LoadPrefab((client ?? server)!.AssetID), client != null ? "Client" : "Server");
        throw new EditError($"{Content.Shown(bindingAsset)} is a prefab binding with no client or server prefab set.");
    }

    // The block an asset belongs to, named by the item TryOpen would make its Item, without loading the block.
    public static Guid? KeyOf(LoadedProject project, ExplorerAssetFile asset)
    {
        Type? type = DefinitionType(asset);
        bool binding = asset.IsPrefabBindingDefinition();
        if (!binding && type != typeof(PrefabDefinition) && type != typeof(EntityCompositeDefinition))
            return null;
        if (binding)
            return asset.AssetID;
        if (project.TryGetPrefabBindingInProject(asset.AssetID) is { } bindingAsset)
            return bindingAsset.AssetID;
        return type == typeof(PrefabDefinition) ? asset.AssetID : project.TryGetPrefabGuidOfCompositeDefinition(asset.AssetID);
    }

    // The binding and every prefab and composition of the block.
    public IEnumerable<Guid> FileIds() => Files().Select(f => f.AssetID).Append(Item.AssetID).Distinct();

    private static Type? DefinitionType(ExplorerAssetFile asset)
    {
        try
        {
            return (asset.AssetInterface as IDefinitionAssetInterface)?.DefinitionType;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    // As the inspector builds it; the serializer copies a shared component's data to its other side.
    private static PrefabManipulator Manipulator(LoadedProject project)
    {
        CustomSerializationContext[] contexts = [new ProjectDefinitionSerializationContext(project)];
        return new PrefabManipulator(new PrefabDefinitionsProvider(project),
            (custom, member) => member.SerializeToJson(custom ?? contexts),
            (member, json) => member.DeserializeFromJson<object>(json, contexts));
    }

    private ProjectLinks Links => new(_project, Item);

    private IEnumerable<EntityPrefabNode> Sides() => Root switch
    {
        BoundEntitiesNode bound => new[] { bound.ClientEntity, bound.ServerEntity }.OfType<EntityPrefabNode>(),
        EntityPrefabNode prefab => [prefab],
        _ => [],
    };

    // The files edits land in: each side's prefab and composition, overrides included.
    private List<ExplorerAssetFile> Files() =>
        Sides().SelectMany(s => new[] { s.DefinitionsIds.PrefabId, s.DefinitionsIds.CompositeId })
            .Select(_project.GetAssetFileByGuid).OfType<ExplorerAssetFile>().Distinct().ToList();

    private void RequireWritable()
    {
        foreach (var file in Files())
            Content.RequireWritable(file);
    }

    private void Save()
    {
        foreach (var file in Files())
            Content.Save(file);
    }

    private List<ComponentEntry> Entries() =>
        Root.Components.Select(c => new ComponentEntry(c.Id, c.ComponentType.Name, c.Tags.Select(t => t.ToString()).ToList())).ToList();

    private (IComponentNode Component, string Name) Find(string token)
    {
        var entries = Entries();
        var entry = ComponentNames.Find(entries, token);
        return (Root.Components.First(c => c.Id == entry.Id), ComponentNames.Assign(entries)[entry.Id]);
    }

    private string SideLabel(IComponentNode component) => component switch
    {
        SharedComponent => " (Client and Server)",
        SingleSideComponent single => single.IsServer ? " (Server)" : " (Client)",
        _ => _soleSide != null ? $" ({_soleSide})" : "",
    };

    private static Type? DefinitionTypeOf(IComponentNode component) => RuntimeComponentInfo.For(component.ComponentType).Definition;

    private static Guid? DefinitionId(IComponentNode component) =>
        component.Definition.GetValue<Definition?>() is { } definition && definition.Guid != Guid.Empty ? definition.Guid : null;

    private string ComponentList()
    {
        var names = ComponentNames.Assign(Entries());
        var lines = Root.Components.OrderBy(c => names[c.Id], StringComparer.OrdinalIgnoreCase).Select(c =>
        {
            string name = names[c.Id];
            string line = name == c.ComponentType.Name ? name : $"{name} = {c.ComponentType.Name}";
            line += SideLabel(c);
            if (c.Tags.Count > 0)
                line += $" tags: {string.Join(", ", TagTexts(c))}";
            if (DefinitionId(c) is { } id)
                line += $"  definition: {Links.PathOf(id) ?? "(missing item)"}";
            else if (DefinitionTypeOf(c) is { } needed)
                line += $"  needs {needed.Name}";
            return line;
        }).ToList();
        return lines.Count == 0 ? "(no components)" : string.Join("\n", lines);
    }

    private EditError NotAComponentPath() => new(
        $"{Content.Shown(Item)} is a block; its fields are its components'. Use a path like {ComponentNames.PathOf("<name>")}.<field>. Components:\n{ComponentList()}");

    private (IComponentNode Component, string Name, string Inner) Locate(string path)
    {
        if (ComponentNames.Split(path) is not var (token, inner))
            throw NotAComponentPath();
        var (component, name) = Find(token);
        return (component, name, inner);
    }

    private static bool IsDefinition(string inner, out bool deeper)
    {
        var segments = FieldPath.Parse(inner);
        deeper = segments.Count > 1;
        return segments.Count > 0 && !segments[0].Bracketed && string.Equals(segments[0].Text, DefinitionField, StringComparison.OrdinalIgnoreCase);
    }

    // Whether the inner path is under Tags; tag is the one it names, or null for the list itself.
    private static bool IsTags(string inner, out string? tag)
    {
        var segments = FieldPath.Parse(inner);
        tag = null;
        if (segments.Count == 0 || segments[0].Bracketed || !string.Equals(segments[0].Text, TagsField, StringComparison.OrdinalIgnoreCase))
            return false;
        if (segments.Count > 2 || (segments.Count == 2 && !segments[1].Bracketed))
            throw new EditError($"A tag has no fields; name one like {TagsField}[<tag>].");
        tag = segments.Count == 2 ? segments[1].Text : null;
        return true;
    }

    private static IEnumerable<string> TagTexts(IComponentNode component) =>
        component.Tags.Select(t => t.ToString()).Order(StringComparer.Ordinal);

    private static string TagList(IComponentNode component) => $"[{string.Join(", ", TagTexts(component))}]";

    private string DefinitionLine(IComponentNode component, string name)
    {
        string path = FieldPath.Member(ComponentNames.PathOf(name), DefinitionField);
        string value = DefinitionId(component) is { } id ? Links.PathOf(id) ?? "(missing item)" : "none";
        string type = DefinitionTypeOf(component)?.Name ?? "takes no definition";
        return $"{path} = {value} ({type})";
    }

    // A component type without an object builder has no fields; its member would read as an empty object.
    private static MemberReference ObjectBuilder(IComponentNode component, string name) =>
        RuntimeComponentInfo.For(component.ComponentType).ObjectBuilder != null
            ? component.ObjectBuilder
            : throw new EditError($"{ComponentNames.PathOf(name)} has no fields; only its {DefinitionField} can be set.");

    public string Read(string path)
    {
        string header = $"{Content.Describe(Item)}:";
        if (path.Trim().Length == 0 || ComponentNames.IsCollection(path))
            return $"{header}\n{ComponentList()}\nRead a component's fields with path {ComponentNames.PathOf("<name>")}.";

        var (component, name, inner) = Locate(path);
        string basePath = ComponentNames.PathOf(name);
        if (inner.Length > 0 && IsDefinition(inner, out bool deeper))
        {
            if (deeper)
                throw new EditError($"'{FieldPath.Member(basePath, DefinitionField)}' is a reference to another item; read that item for its fields.");
            return $"{header}\n{DefinitionLine(component, name)}";
        }

        if (inner.Length > 0 && IsTags(inner, out string? tag))
        {
            if (tag != null)
                throw new EditError($"Read the tags as {FieldPath.Member(basePath, TagsField)}; a tag has no fields.");
            return $"{header}\n{FieldPath.Member(basePath, TagsField)} = {TagList(component)}";
        }

        if (inner.Length > 0)
            return $"{header}\n{FieldReader.Read(ObjectBuilder(component, name), inner, Links, basePath: basePath)}";

        string fields = RuntimeComponentInfo.For(component.ComponentType).ObjectBuilder == null
            ? $"{basePath}: (no fields)"
            : FieldReader.Read(component.ObjectBuilder, "", Links, maxLines: FieldReader.MaxLines - 2, basePath: basePath);
        return $"{header}\n{DefinitionLine(component, name)}\n{FieldPath.Member(basePath, TagsField)} = {TagList(component)}\n{fields}";
    }

    public string Describe(string path, ITypeCatalog catalog)
    {
        string header = $"{Content.Describe(Item)} (block):";
        if (path.Trim().Length == 0 || ComponentNames.IsCollection(path))
        {
            var names = ComponentNames.Assign(Entries());
            var lines = new List<string> { header };
            foreach (var c in Root.Components.OrderBy(c => names[c.Id], StringComparer.OrdinalIgnoreCase))
            {
                string line = $"{ComponentNames.PathOf(names[c.Id])}: {c.ComponentType.Name}";
                if (DefinitionTypeOf(c) is { } needed)
                    line += $", takes {needed.Name}";
                lines.Add(line);
                if (catalog.Summary(c.ComponentType) is { } summary)
                    lines.Add("  " + summary.Split('\n')[0]);
            }
            lines.Add($"Describe a component with member {ComponentNames.PathOf("<name>")}.");
            return string.Join("\n", lines);
        }

        var (component, name, inner) = Locate(path);
        string basePath = ComponentNames.PathOf(name);
        if (inner.Length > 0 && IsDefinition(inner, out bool deeper))
        {
            var type = DefinitionTypeOf(component) ?? throw new EditError($"A {component.ComponentType.Name} takes no definition.");
            if (deeper)
                throw new EditError($"'{FieldPath.Member(basePath, DefinitionField)}' is a reference to another item; describe {type.Name} for its fields.");
            return $"{header}\n{FieldPath.Member(basePath, DefinitionField)}: ref {type.Name}\n\n{Describer.Listing(type, catalog.Fields(type), catalog)}";
        }

        if (inner.Length > 0 && IsTags(inner, out _))
            return $"{header}\n{FieldPath.Member(basePath, TagsField)}: list of tags; systems find the component by them, and a side holds one component per tag.";

        if (RuntimeComponentInfo.For(component.ComponentType).ObjectBuilder == null)
        {
            if (inner.Length > 0)
                throw new EditError($"{basePath} has no fields; only its {DefinitionField} can be set.");
            return $"{header}\n{basePath}: {Describer.Listing(component.ComponentType, null, catalog)}";
        }

        return $"{header}\n{(inner.Length == 0 ? $"{basePath}: " : "")}{Describer.OfValue(component.ObjectBuilder, inner, catalog, Links, basePath)}";
    }

    public List<string> FindLinks(IReadOnlySet<Guid> targets)
    {
        var names = ComponentNames.Assign(Entries());
        var found = new List<string>();
        foreach (var component in Root.Components.OrderBy(c => names[c.Id], StringComparer.OrdinalIgnoreCase))
        {
            string basePath = ComponentNames.PathOf(names[component.Id]);
            if (DefinitionId(component) is { } id && targets.Contains(id))
                found.Add(FieldPath.Member(basePath, DefinitionField));
            if (RuntimeComponentInfo.For(component.ComponentType).ObjectBuilder != null)
                found.AddRange(FieldReader.FindLinks(component.ObjectBuilder, targets, Links, basePath));
        }
        return found;
    }

    public List<FieldEntry> SearchEntries(string path)
    {
        var names = ComponentNames.Assign(Entries());
        if (path.Trim().Length == 0 || ComponentNames.IsCollection(path))
            return Root.Components.OrderBy(c => names[c.Id], StringComparer.OrdinalIgnoreCase)
                .SelectMany(c => ComponentEntries(c, names[c.Id], "")).ToList();

        var (component, name, inner) = Locate(path);
        if (inner.Length > 0 && IsDefinition(inner, out _))
            return DefinitionEntries(component, name).ToList();
        if (inner.Length > 0 && IsTags(inner, out _))
            return [];
        return ComponentEntries(component, name, inner).ToList();
    }

    private IEnumerable<FieldEntry> ComponentEntries(IComponentNode component, string name, string inner)
    {
        var entries = inner.Length == 0 ? DefinitionEntries(component, name).ToList() : [];
        if (RuntimeComponentInfo.For(component.ComponentType).ObjectBuilder != null)
            entries.AddRange(FieldReader.Entries(component.ObjectBuilder, inner, Links, ComponentNames.PathOf(name)));
        return entries;
    }

    private IEnumerable<FieldEntry> DefinitionEntries(IComponentNode component, string name)
    {
        if (DefinitionId(component) is not { } id)
            yield break;
        string path = FieldPath.Member(ComponentNames.PathOf(name), DefinitionField);
        string value = Links.PathOf(id) ?? "(missing item)";
        yield return new FieldEntry(path, value, $"{path} = {value}", id);
    }

    public string Write(string path, JsonElement value)
    {
        var (component, name, inner) = Locate(path);
        string basePath = ComponentNames.PathOf(name);
        RequireWritable();

        string reply;
        if (inner.Length > 0 && IsDefinition(inner, out bool deeper))
        {
            if (deeper)
                throw new EditError($"'{FieldPath.Member(basePath, DefinitionField)}' is a reference; write the item's own fields on that item.");
            reply = WriteDefinition(component, name, value);
        }
        else if (inner.Length > 0 && IsTags(inner, out _))
        {
            throw new EditError($"Tags are added with element_add on {FieldPath.Member(basePath, TagsField)} (key: the tag) and removed with element_remove on {FieldPath.Member(basePath, TagsField)}[<tag>].");
        }
        else
        {
            reply = FieldWriter.Write(ObjectBuilder(component, name), inner, value, Links, basePath, new ComponentDefinition(_project, component));
        }
        return Applied(component, reply);
    }

    public string ElementAdd(string path, string? key, string? type, string? value = null)
    {
        var (component, name, inner) = Locate(path);
        if (inner.Length > 0 && IsDefinition(inner, out _))
            throw new EditError($"'{FieldPath.Member(ComponentNames.PathOf(name), DefinitionField)}' is a reference, not a list.");
        if (inner.Length == 0)
            throw new EditError($"To add a component use component_add; element_add takes a list inside one, e.g. {ComponentNames.PathOf(name)}.<list>.");
        RequireWritable();
        if (IsTags(inner, out string? tagged))
        {
            string? tag = string.IsNullOrWhiteSpace(key) ? value : key;
            if (tagged != null || string.IsNullOrWhiteSpace(tag))
                throw new EditError($"Add a tag with element_add on {FieldPath.Member(ComponentNames.PathOf(name), TagsField)} and the tag as key.");
            return Applied(component, AddTag(component, name, tag.Trim()));
        }
        return Applied(component, Elements.Add(ObjectBuilder(component, name), inner, key, type, Links, ComponentNames.PathOf(name), value));
    }

    public string ElementRemove(string path)
    {
        var (component, name, inner) = Locate(path);
        if (inner.Length == 0)
            throw new EditError($"To remove {ComponentNames.PathOf(name)} use component_remove.");
        RequireWritable();
        if (IsTags(inner, out string? tagged))
        {
            if (tagged == null)
                throw new EditError(component.Tags.Count == 0
                    ? $"{ComponentNames.PathOf(name)} has no tags."
                    : $"Name the tag to remove, e.g. {FieldPath.Member(ComponentNames.PathOf(name), TagsField)}[{TagTexts(component).First()}].");
            return Applied(component, RemoveTag(component, name, tagged));
        }
        return Applied(component, Elements.Remove(ObjectBuilder(component, name), inner, Links, ComponentNames.PathOf(name)));
    }

    // A shared component's edit lands on its client side; the prefab layer copies it to the server side.
    private string Applied(IComponentNode component, string reply)
    {
        component.ApplyChanges();
        Save();
        return $"{Content.Shown(Item)}: {reply}";
    }

    // Bounds that game validation takes from a component's definition, read from the definition's file as read shows it,
    // so a value just written there counts at once.
    private sealed class ComponentDefinition(LoadedProject project, IComponentNode component) : IDefinitionLocatorStrategy
    {
        private MemberReference? _root;

        public MemberReference GetDefinitionMember(MemberReference source) => _root ??=
            DefinitionId(component) is { } id && project.GetAssetFileByGuid(id) is ExplorerAssetFile asset
                ? Content.Definition(asset).Fields.Root
                : throw new EditError("The component has no definition.");
    }

    private string WriteDefinition(IComponentNode component, string name, JsonElement value)
    {
        string path = FieldPath.Member(ComponentNames.PathOf(name), DefinitionField);
        string before = DefinitionId(component) is { } old ? Links.PathOf(old) ?? "(missing item)" : "none";
        if (value.ValueKind == JsonValueKind.Null)
            component.Definition.SetDefault();
        else if (value.ValueKind != JsonValueKind.String || value.GetString()!.Trim().Length == 0)
            throw new EditError($"'{path}' is a reference; give the name or path of the definition to point at, or null to clear it.");
        else
            component.Definition.SetValue<object?>(DefinitionReference(component, value.GetString()!.Trim()));
        string after = DefinitionId(component) is { } id ? Links.PathOf(id) ?? "(missing item)" : "none";
        return $"{path}: {before} -> {after}";
    }

    // Resolved among items of the definition type the component's type takes, not any definition the field could hold.
    private Definition DefinitionReference(IComponentNode component, string itemName)
    {
        var type = DefinitionTypeOf(component)
            ?? throw new EditError($"A {component.ComponentType.Name} takes no definition.");
        var link = Links.Find(itemName, new ReferenceSpec(component.Definition, ReferenceKind.Definition, [type], []));
        return Links.CreateDefinitionReference(link.Id);
    }

    // A side keeps one component per tag, so a tag held elsewhere would be taken away from its holder.
    private string AddTag(IComponentNode component, string name, string text)
    {
        string path = FieldPath.Member(ComponentNames.PathOf(name), TagsField);
        // Removal names a tag inside a path's brackets, so a tag must survive being written there.
        if (text.Any(c => char.IsWhiteSpace(c) || c is '[' or ']' or '.'))
            throw new EditError($"'{text}' cannot be a tag; use letters, digits and underscores.");
        var tag = StringId.Get(text);
        if (component.Tags.Any(t => string.Equals(t.ToString(), text, StringComparison.OrdinalIgnoreCase)))
            throw new EditError($"{path} already has {text}.");
        var names = ComponentNames.Assign(Entries());
        var holders = SidesOf(component).SelectMany(side => side.ComponentsMap.Values)
            .Where(other => other.Id != component.Id && other.Tags.Any(t => string.Equals(t.ToString(), text, StringComparison.OrdinalIgnoreCase)))
            .Select(other => names[other.Id]).Distinct().ToList();
        if (holders.Count > 0)
            throw new EditError($"{text} is the tag of {string.Join(", ", holders.Select(ComponentNames.PathOf))}; a tag belongs to one component on a side.");
        string before = TagList(component);
        component.AddTag(tag);
        return $"{path}: {before} -> {TagList(component)}";
    }

    private string RemoveTag(IComponentNode component, string name, string text)
    {
        string path = FieldPath.Member(ComponentNames.PathOf(name), TagsField);
        var match = component.Tags.Where(t => string.Equals(t.ToString(), text, StringComparison.OrdinalIgnoreCase)).ToList();
        if (match.Count == 0)
            throw new EditError($"{path} has no tag {text}; its tags are {TagList(component)}.");
        var tag = match[0];
        var dependents = Dependents(component, tag);
        if (dependents.Count > 0)
            throw new EditError($"{tag} cannot be removed: {string.Join(", ", dependents.Select(ComponentNames.PathOf))} need{(dependents.Count == 1 ? "s" : "")} it.");
        string before = TagList(component);
        component.RemoveTag(tag);
        return $"{path}: {before} -> {TagList(component)}";
    }

    private IEnumerable<EntityPrefabNode> SidesOf(IComponentNode component) => Sides().Where(side => side.ComponentsMap.ContainsKey(component.Id));

    // A type allowing several per entity has no default tag, and systems such as the conveyor graph find some
    // components only by tag.
    private string AutoTag(IComponentNode component)
    {
        const string suffix = "Component";
        string type = component.ComponentType.Name;
        string stem = type.EndsWith(suffix, StringComparison.Ordinal) && type.Length > suffix.Length ? type[..^suffix.Length] : type;
        var taken = SidesOf(component).SelectMany(side => side.ComponentsMap.Values).SelectMany(c => c.Tags).Select(t => t.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int n = 1;
        while (taken.Contains($"{stem}{n}"))
            n++;
        return $"{stem}{n}";
    }

    private static ComponentScope SideOf(Type type) =>
        SideOverrides.TryGetValue(type.FullName ?? "", out var side) ? side : ComponentScopeUtility.GetComponentScope(type);

    public string Add(string typeQuery, string? definition)
    {
        var choices = AddableTypes();
        var type = DefinitionTypes.Pick(choices, typeQuery, "Component", "component type", "ThrusterComponent");
        if (!RuntimeComponentInfo.For(type).AllowMultiple && Root.Components.FirstOrDefault(c => c.ComponentType == type) is { } present)
            throw new EditError($"{Content.Shown(Item)} already has {ComponentNames.PathOf(ComponentNames.Assign(Entries())[present.Id])}; a {type.Name} is there once per entity.");

        RefuseTakenDefaultTags(type);

        Definition? reference = null;
        if (definition != null)
        {
            var needed = RuntimeComponentInfo.For(type).Definition ?? throw new EditError($"A {type.Name} takes no definition; leave definition out.");
            var link = Links.Find(definition, new ReferenceSpec(default, ReferenceKind.Definition, [needed], []));
            reference = Links.CreateDefinitionReference(link.Id);
        }
        RequireWritable();

        var before = Root.Components.Select(c => c.Id).ToHashSet();
        var tagsBefore = Root.Components.ToDictionary(c => c.Id, c => TagTexts(c).ToHashSet(StringComparer.Ordinal));
        var sharedBefore = Root.Components.Where(c => c is SharedComponent).Select(c => c.Id).ToHashSet();
        var id = Root is BoundEntitiesNode bound && SideOverrides.ContainsKey(type.FullName ?? "")
            ? bound.AddComponent(type, SideOf(type))
            : Root.AddComponent(type);
        foreach (var component in Root.Components.ToList())
            component.PopulateMissingDependencies();
        KeepOverriddenSides(sharedBefore);
        var unmet = FillInterfaceDependencies();
        AddCompanions(choices);

        var added = Root.Components.First(c => c.Id == id);
        string? autoTag = null;
        if (added.Tags.Count == 0)
        {
            autoTag = AutoTag(added);
            added.AddTag(StringId.Get(autoTag));
        }
        if (reference != null)
        {
            added.Definition.SetValue<object?>(reference);
            added.ApplyChanges();
        }
        Save();

        var names = ComponentNames.Assign(Entries());
        string Label(IComponentNode c) => ComponentNames.PathOf(names[c.Id]) + SideLabel(c);
        var lines = new List<string> { $"{Content.Shown(Item)}: added {Label(added)}{(autoTag != null ? $" with tag {autoTag}" : "")}." };

        var dependencies = Root.Components.Where(c => c.Id != id && !before.Contains(c.Id)).ToList();
        if (dependencies.Count > 0)
            lines.Add($"Added with it for its dependencies: {string.Join(", ", dependencies.Select(Label))}.");

        foreach (var c in Root.Components.Where(c => c.Id != id && tagsBefore.ContainsKey(c.Id)))
        {
            var newTags = TagTexts(c).Where(t => !tagsBefore[c.Id].Contains(t)).ToList();
            if (newTags.Count > 0)
                lines.Add($"{Label(c)} got tag {string.Join(", ", newTags)}, which the new component's dependencies look up.");
        }

        foreach (var c in dependencies.Prepend(added).Where(c => DefinitionId(c) == null && DefinitionTypeOf(c) != null))
        {
            string path = ComponentNames.PathOf(names[c.Id]);
            lines.Add($"{path} needs a {DefinitionTypeOf(c)!.Name}; the block fails validation without it. Point it at one with write to {path}.Definition, or create one with create_definition first.");
        }
        lines.AddRange(unmet);
        return string.Join("\n", lines);
    }

    private const string RenderParentTag = "IRenderParent";
    private const string RenderParentAdapter = "ParentRenderAdapterComponent";

    // The Editor's filler adds only dependencies declared as a component type; one declared as an interface has
    // several providers and is left unfilled. A block is always a child entity, so its render parent is the adapter.
    private List<string> FillInterfaceDependencies()
    {
        var unmet = new List<string>();
        var addable = AddableTypes();
        bool block = Root.Components.Any(c => IsOrDerives(c.ComponentType, "CubeBlockComponent"));
        bool changed = false;
        foreach (var (side, scope) in SidesWithScope())
        {
            foreach (var (tag, needers, candidates) in UnfilledInterfaceDependencies(side, addable))
            {
                var adapter = candidates.FirstOrDefault(t => t.Name == RenderParentAdapter);
                if (block && tag == RenderParentTag && adapter != null)
                {
                    if (Root is BoundEntitiesNode bound)
                        bound.AddComponent(adapter, scope);
                    else
                        Root.AddComponent(adapter);
                    changed = true;
                    continue;
                }
                string sideText = Root is BoundEntitiesNode ? $" on the {scope}" : "";
                string offer = candidates.Count == 0 ? "no component offered here provides it"
                    : $"add one of: {string.Join(", ", candidates.Select(t => t.Name))}";
                unmet.Add($"{string.Join(", ", needers)} need{(needers.Count == 1 ? "s" : "")} a component tagged {tag}{sideText}, which was not added: {offer}.");
            }
        }
        if (changed)
            foreach (var component in Root.Components.ToList())
                component.PopulateMissingDependencies();
        return unmet;
    }

    private IEnumerable<(EntityPrefabNode Side, ComponentScope Scope)> SidesWithScope()
    {
        if (Root is BoundEntitiesNode bound)
        {
            if (bound.ClientEntity is EntityPrefabNode client)
                yield return (client, ComponentScope.Client);
            if (bound.ServerEntity is EntityPrefabNode server)
                yield return (server, ComponentScope.Server);
        }
        else if (Root is EntityPrefabNode prefab)
        {
            yield return (prefab, _soleSide == "Server" ? ComponentScope.Server : ComponentScope.Client);
        }
    }

    private static List<(string Tag, List<string> Needers, List<Type> Candidates)> UnfilledInterfaceDependencies(EntityPrefabNode side, IReadOnlyCollection<Type> addable)
    {
        var tags = side.ComponentsMap.Values.SelectMany(c => c.Tags).ToHashSet();
        var found = new Dictionary<string, (List<string> Needers, List<Type> Candidates)>(StringComparer.Ordinal);
        foreach (var component in side.ComponentsMap.Values)
        {
            foreach (var dependency in RuntimeComponentInfo.For(component.ComponentType).BuildDependencies)
            {
                var type = dependency.Field.FieldType;
                if (dependency.Optional || RuntimeComponentInfo.IsComponent(type) || !type.IsInterface || tags.Contains(dependency.Tag))
                    continue;
                string tag = dependency.Tag.ToString();
                if (!found.TryGetValue(tag, out var entry))
                    found[tag] = entry = ([], addable.Where(type.IsAssignableFrom).OrderBy(t => t.Name, StringComparer.Ordinal).ToList());
                if (!entry.Needers.Contains(component.ComponentType.Name))
                    entry.Needers.Add(component.ComponentType.Name);
            }
        }
        return found.Select(kv => (kv.Key, kv.Value.Needers, kv.Value.Candidates)).ToList();
    }

    private static bool IsOrDerives(Type? type, string name)
    {
        for (; type != null; type = type.BaseType)
            if (type.Name == name)
                return true;
        return false;
    }

    private void AddCompanions(IReadOnlyCollection<Type> addable)
    {
        if (!Root.Components.Any(c => IsOrDerives(c.ComponentType, "CubeBlockComponent")) || _soleSide == "Client")
            return;
        foreach (var (owner, companion) in Companions)
        {
            if (!Root.Components.Any(c => DerivesFrom(c.ComponentType, owner)) || Root.Components.Any(c => DerivesFrom(c.ComponentType, companion)))
                continue;
            if (addable.FirstOrDefault(t => t.FullName == companion) is not { } type)
                continue;
            if (Root is BoundEntitiesNode bound)
                bound.AddComponent(type, SideOf(type));
            else
                Root.AddComponent(type);
        }
    }

    private static bool DerivesFrom(Type? type, string fullName)
    {
        for (; type != null; type = type.BaseType)
            if (type.FullName == fullName)
                return true;
        return false;
    }

    public bool HasComponent(string typeName) => Root.Components.Any(c =>
    {
        for (var type = c.ComponentType; type != null; type = type.BaseType)
            if (type.Name == typeName)
                return true;
        return false;
    });

    // Each component's name as the tools take it, with the definition it points at.
    public List<(string Name, Guid Definition)> ComponentDefinitions()
    {
        var names = ComponentNames.Assign(Entries());
        return Root.Components.Where(c => DefinitionId(c) != null).Select(c => (names[c.Id], DefinitionId(c)!.Value)).ToList();
    }

    // The Editor would move a default tag held by another component to the new one, leaving two components in one
    // role: a PowerableBlockComponent next to a FunctionalBlockComponent took GridBlockComponent, and two block
    // definitions then crashed the Editor's breakable post-processing.
    private void RefuseTakenDefaultTags(Type type)
    {
        var defaults = RuntimeComponentInfo.For(type).DefaultTags;
        if (defaults == null || defaults.Length == 0)
            return;
        var names = ComponentNames.Assign(Entries());
        var holders = SidesFor(type)
            .SelectMany(s => s.ComponentsMap.Values)
            .Where(c => c.Tags.Any(defaults.Contains))
            .Select(c => (Name: names[c.Id], Tags: c.Tags.Where(defaults.Contains).Select(t => t.ToString())))
            .DistinctBy(h => h.Name)
            .ToList();
        if (holders.Count == 0)
            return;
        string held = string.Join("; ", holders.Select(h => $"{ComponentNames.PathOf(h.Name)} holds {string.Join(", ", h.Tags)}"));
        throw new EditError($"A {type.Name} would take over a role this block's component already fills ({held}); a block keeps one component per role. Replacing a component is not a tool operation.");
    }

    private IEnumerable<EntityPrefabNode> SidesFor(Type type)
    {
        if (Root is not BoundEntitiesNode bound)
            return Sides();
        var side = SideOf(type);
        var sides = new List<object?>();
        if ((side & ComponentScope.Client) != 0)
            sides.Add(bound.ClientEntity);
        if ((side & ComponentScope.Server) != 0)
            sides.Add(bound.ServerEntity);
        return sides.OfType<EntityPrefabNode>();
    }

    // Dependency filling sides a dependency by the Editor's own scope, so it puts an overridden type on both sides,
    // making an existing one shared or adding a new one shared; the other side's half is dropped again.
    private void KeepOverriddenSides(HashSet<Guid> sharedBefore)
    {
        foreach (var component in Root.Components.OfType<SharedComponent>().ToList())
        {
            if (sharedBefore.Contains(component.Id) || !SideOverrides.TryGetValue(component.ComponentType.FullName ?? "", out var side))
                continue;
            component.Remove(side == ComponentScope.Server ? ComponentScope.Client : ComponentScope.Server);
        }
    }

    public string Remove(string token)
    {
        var (component, name) = Find(token);
        string path = ComponentNames.PathOf(name);
        RequireWritable();

        var dependents = Dependents(component);
        if (dependents.Count > 0)
            throw new EditError($"{path} cannot be removed: {string.Join(", ", dependents.Select(ComponentNames.PathOf))} depend{(dependents.Count == 1 ? "s" : "")} on it. Remove them first.");

        string sides = SideLabel(component);
        Guid? definition = DefinitionId(component);
        var files = Files().Select(f => f.AssetID).ToHashSet();
        Root.RemoveComponent(component.Id);
        Save();

        string reply = $"{Content.Shown(Item)}: removed {path}{sides}.";
        if (definition is { } id && _project.GetAssetFileByGuid(id) is { Project.IsReadOnly: false } asset && Unused(id, files) == true)
            reply += $" Its definition {Content.Shown(asset)} is no longer used by anything.";
        return reply;
    }

    // The engine's dependency rule, run backwards: a required dependency is a tag the component fills on its side.
    private List<string> Dependents(IComponentNode component, StringId? only = null)
    {
        var names = ComponentNames.Assign(Entries());
        var found = new HashSet<Guid>();
        foreach (var side in Sides())
        {
            if (!side.ComponentsMap.TryGetValue(component.Id, out var own))
                continue;
            foreach (var (otherId, other) in side.ComponentsMap)
            {
                if (otherId != component.Id && RuntimeComponentInfo.For(other.ComponentType).BuildDependencies.Any(d => !d.Optional && (only is { } tag ? d.Tag == tag : own.Tags.Contains(d.Tag))))
                    found.Add(otherId);
            }
        }
        return found.Select(id => names[id]).Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Null when the project keeps no reference graph. The block's own files are left out, since the graph may not
    // have caught up with the removal yet; what the block still uses is read from the block itself.
    private bool? Unused(Guid definition, HashSet<Guid> blockFiles)
    {
        if (Root.Components.Any(c => DefinitionId(c) == definition))
            return false;
        if (!_project.ReferencesManager.Enabled)
            return null;
        return _project.ReferencesManager.GetDirectDependenciesTo(definition).All(d => blockFiles.Contains(d.guid));
    }

    // The inspector's Add Component list, for this block's sides.
    private IReadOnlyCollection<Type> AddableTypes()
    {
        var scope = Root is BoundEntitiesNode bound ? bound.Scope : ComponentScope.Shared;
        return TypeOptionManager.FindOptions(typeof(Component),
                (_, candidate) => Addable(candidate, scope),
                null,
                $"Veronica_Component_{scope}")
            .ToList();
    }

    private static bool Addable(Type candidate, ComponentScope scope)
    {
        if (candidate.IsAbstract || candidate.ContainsGenericParameters || !RuntimeComponentInfo.IsComponent(candidate))
            return false;
        // The inspector offers HierarchyComponent in developer mode only, yet vanilla subparts, door leaves and
        // blocks with subparts carry it, so it stays addable by name.
        if (candidate != typeof(HierarchyComponent) && !CreateDefinitionTool.GameVisible(candidate))
            return false;
        var own = SideOf(candidate);
        if (scope == ComponentScope.Client && (own & ComponentScope.Client) == 0)
            return false;
        if (scope == ComponentScope.Server && (own & ComponentScope.Server) == 0)
            return false;
        return Serializable(candidate, []);
    }

    // Its object builder and those of its required dependencies serialize, or the prefab could not be saved.
    private static bool Serializable(Type component, HashSet<Type> seen)
    {
        if (!seen.Add(component))
            return true;
        var info = RuntimeComponentInfo.For(component);
        if (info.ObjectBuilder != null && !SerializationHelper.TryGetSerializer(info.ObjectBuilder, out _))
            return false;
        foreach (var dependency in info.BuildDependencies)
        {
            var type = dependency.Field.FieldType;
            if (!dependency.Optional && RuntimeComponentInfo.IsComponent(type) && !Serializable(type, seen))
                return false;
        }
        return true;
    }
}
