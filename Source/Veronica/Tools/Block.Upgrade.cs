using Keen.VRage.ContentPipeline.Definitions;
using Keen.VRage.ContentPipeline.Game.Prefabs;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Utils;
using Keen.VRage.Core.Game.Editor.PrefabManipulators;
using Keen.VRage.Core.Game.Editor.PrefabManipulators.BoundEntities;
using Keen.VRage.DCS.Internal;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.Utils;
using Keen.VRage.UI.Shared.ObjectInspector.Extensions;
using Veronica.Editing;

namespace Veronica.Tools;

// The component to upgrade and what the base brings, found before the new definition is created, so a refused
// upgrade leaves no file behind.
internal sealed record UpgradePlan(
    Guid Component,
    Type NewType,
    string Base,
    Guid? BaseDefinition,
    IReadOnlyDictionary<string, string> BaseFields,
    ExplorerFolder Folder,
    string DefinitionName);

internal sealed partial class Block
{
    private const string GuidField = "Guid";

    public UpgradePlan PlanUpgrade(string token, string baseQuery)
    {
        var (component, name) = Find(token);
        RequireWritable();
        var oldType = component.ComponentType;
        var (newType, baseShown, baseDefinition, baseFields) = ResolveBase(baseQuery, oldType, name);

        var newSides = SideOf(newType);
        foreach (var side in SidesWithScope().Where(s => s.Side.ComponentsMap.ContainsKey(component.Id)))
        {
            if (Root is BoundEntitiesNode && (newSides & side.Scope) == 0)
                throw new EditError($"A {newType.Name} cannot be on the {side.Scope}, where {ComponentNames.PathOf(name)} is.");
        }

        var needed = RuntimeComponentInfo.For(newType).Definition;
        if (needed != null && baseDefinition == null)
            throw new EditError($"{baseShown} gives no {needed.Name} to copy; pick a base whose {newType.Name} has a definition.");

        var folder = Item.Parent ?? throw new EditError($"{Content.Shown(Item)} is in no folder.");
        Content.RequireWritable(folder);
        return new UpgradePlan(component.Id, newType, baseShown, needed != null ? baseDefinition : null, baseFields, folder,
            needed != null ? NewDefinitionName(component, needed, folder) : "");
    }

    public string Upgrade(UpgradePlan plan, string? definitionFile)
    {
        var component = Root.Components.FirstOrDefault(c => c.Id == plan.Component)
            ?? throw new EditError("The component changed while the upgrade ran; read the block again.");
        RequireWritable();
        var contexts = SerializationContexts();
        string oldLabel = Label(component);
        var oldType = component.ComponentType;
        Guid? oldDefinition = DefinitionId(component);
        var files = Files().Select(f => f.AssetID).ToHashSet();
        var oldFields = RuntimeComponentInfo.For(oldType).ObjectBuilder != null ? Snapshot(component.ObjectBuilder, contexts) : new();

        var lines = new List<string>();
        ExplorerAssetFile? definition = null;
        if (definitionFile != null)
        {
            definition = plan.Folder.GetItemByName(definitionFile) as ExplorerAssetFile
                ?? throw new EditError($"The new definition {definitionFile} is not in {Content.Shown(plan.Folder)}.");
            Content.RequireWritable(definition);
            var root = Content.Definition(definition).Fields.Root;
            var oldAsset = oldDefinition is { } id ? _project.GetAssetFileByGuid(id) as ExplorerAssetFile : null;
            var oldNames = oldAsset != null ? Content.Definition(oldAsset).Fields.Root.Members.Select(m => m.Name).ToHashSet() : null;
            if (oldAsset != null)
                Apply(Snapshot(Content.Definition(oldAsset).Fields.Root, contexts), root, contexts);
            Content.Save(definition);

            string from = oldAsset != null ? $", with the values of {Content.Shown(oldAsset)}" : "";
            lines.Add($"Its definition is now {Content.Shown(definition)}, copied from {plan.Base}{from}.");
            if (oldNames != null)
            {
                var brought = root.Members.Where(m => !oldNames.Contains(m.Name) && Fields.IsVisible(m)).ToList();
                if (brought.Count > 0)
                    lines.Add($"From {plan.Base}, fields a {oldType.Name}'s definition has not; change them with write on {Content.Shown(definition)}:\n"
                        + string.Join("\n", brought.Select(m => FieldReader.Read(root, m.Name, Links))));
            }
        }

        var before = Capture();
        var tags = component.Tags.ToList();
        foreach (var tag in RuntimeComponentInfo.For(plan.NewType).DefaultTags ?? [])
        {
            if (!tags.Contains(tag) && !SidesOf(component).SelectMany(s => s.ComponentsMap.Values).Any(c => c.Id != component.Id && c.Tags.Contains(tag)))
                tags.Add(tag);
        }
        // The id is kept so nothing that names the component loses it. A bound block keeps its client and server
        // wrapper for an id it already has, so it must drop the id before the sides add it again, or the wrapper
        // keeps the removed side nodes.
        var sides = SidesOf(component).ToList();
        var bound = Root as IBoundEntitiesNodeInternal;
        foreach (var side in sides)
            side.RemoveComponent(component.Id);
        bound?.UpdateComponents();
        foreach (var side in sides)
            side.AddComponent(plan.NewType, component.Id, null, null, tags.ToArray());
        bound?.UpdateComponents();

        var upgraded = Root.Components.First(c => c.Id == plan.Component);
        if (RuntimeComponentInfo.For(plan.NewType).ObjectBuilder != null)
        {
            Apply(plan.BaseFields, upgraded.ObjectBuilder, contexts);
            Apply(oldFields, upgraded.ObjectBuilder, contexts);
        }
        if (definition != null)
            upgraded.Definition.SetValue<object?>(Links.CreateDefinitionReference(definition.AssetID));
        upgraded.ApplyChanges();
        var unmet = FillDependencies(AddableTypes(), before);
        Save();

        lines.Insert(0, $"{Content.Shown(Item)}: {oldLabel} upgraded to {Label(upgraded)}, keeping its tags and values.");
        lines.AddRange(AddedNotes(plan.Component, before, unmet));
        string unused = UnusedNote(oldDefinition, files);
        if (unused.Length > 0)
            lines.Add(unused.TrimStart());
        return string.Join("\n", lines);
    }

    private CustomSerializationContext[] SerializationContexts() => [new ProjectDefinitionSerializationContext(_project)];

    // The base's component of a type derived from the old one, with the definition to copy and its fields.
    private (Type Type, string Shown, Guid? Definition, IReadOnlyDictionary<string, string> Fields) ResolveBase(string query, Type oldType, string name)
    {
        var templates = TemplateFiles();
        var matches = Templates.Resolve(templates.Keys, query);
        ExplorerAssetFile asset;
        string shown;
        if (matches.Count == 1)
        {
            asset = templates[matches[0]];
            shown = $"template {matches[0]}";
        }
        else
        {
            try
            {
                asset = Content.Resolve(_project, query);
            }
            catch (EditError) when (matches.Count > 1)
            {
                throw new EditError($"'{query}' matches several templates: {string.Join(", ", matches.Take(Templates.MaxListed))}.");
            }
            shown = Content.Shown(asset);
        }

        string path = ComponentNames.PathOf(name);
        if (TryOpen(_project, asset) is { } block)
        {
            var offered = block.Root.Components.Select(c => c.ComponentType).Distinct().ToList();
            var candidates = block.Root.Components.Where(c => c.ComponentType.IsSubclassOf(oldType)).ToList();
            if (candidates.Count == 1)
            {
                var chosen = candidates[0];
                var fields = RuntimeComponentInfo.For(chosen.ComponentType).ObjectBuilder != null ? Snapshot(chosen.ObjectBuilder, SerializationContexts()) : new();
                return (chosen.ComponentType, shown, DefinitionId(chosen), fields);
            }
            throw NoUpgrade(shown, path, oldType, candidates.Count > 1 ? candidates.Select(c => c.ComponentType).ToList() : offered, candidates.Count > 1);
        }

        var definitionType = DefinitionType(asset)
            ?? throw new EditError($"{shown} is not a template, a block or a definition; give one whose component is a kind of {oldType.Name}.");
        var types = AddableTypes().Where(t => t.IsSubclassOf(oldType) && RuntimeComponentInfo.For(t).Definition is { } d && d.IsAssignableFrom(definitionType)).ToList();
        var exact = types.Where(t => RuntimeComponentInfo.For(t).Definition == definitionType).ToList();
        if (exact.Count > 0)
            types = exact;
        if (types.Count == 1)
            return (types[0], shown, asset.AssetID, new Dictionary<string, string>());
        if (types.Count > 1)
            throw NoUpgrade(shown, path, oldType, types, true);
        throw new EditError($"{shown} is a {definitionType.Name}, which no kind of {oldType.Name} takes; give a definition of the type the new component takes, or a template or block that has one.");
    }

    private static EditError NoUpgrade(string shown, string path, Type oldType, List<Type> types, bool several)
    {
        string list = string.Join(", ", types.Select(t => t.Name).Order(StringComparer.Ordinal));
        return several
            ? new EditError($"{shown} offers several kinds of {oldType.Name}: {list}. Give a definition of the one you want instead.")
            : new EditError($"{shown} has no kind of {oldType.Name} to upgrade {path} to; its components: {list}.");
    }

    // The Create Prefab menu's templates by the path find_template lists, each with its template block.
    private Dictionary<string, ExplorerAssetFile> TemplateFiles()
    {
        var found = new Dictionary<string, ExplorerAssetFile>(StringComparer.OrdinalIgnoreCase);
        if (_project.ProjectFeatures.OfType<PrefabGeneratorFeature>().FirstOrDefault() is not { } feature)
            return found;
        foreach (var (file, template) in feature.Templates)
            found.TryAdd(template.TemplatePath.Replace('\\', '/'), file);
        Templates.AddSynthetic(found);
        return found;
    }

    // The old definition's name with its type swapped for the new one, so the file still reads as the block's.
    private string NewDefinitionName(IComponentNode component, Type needed, ExplorerFolder folder)
    {
        var old = DefinitionId(component) is { } id ? _project.GetAssetFileByGuid(id) : null;
        string stem = Path.GetFileNameWithoutExtension((old ?? Item).Name);
        string? oldType = DefinitionTypeOf(component)?.Name;
        int at = oldType == null ? -1 : stem.LastIndexOf(oldType, StringComparison.Ordinal);
        string name = at >= 0 ? stem[..at] + needed.Name + stem[(at + oldType!.Length)..] : $"{stem}_{needed.Name}";
        string free = name;
        for (int n = 2; FileStem.Taken(folder, free) != null; n++)
            free = $"{name}{n}";
        return free;
    }

    // Each field as the inspector's copy puts it on the clipboard; the identity of a definition is not a value.
    private static Dictionary<string, string> Snapshot(MemberReference root, CustomSerializationContext[] contexts)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var member in root.Members)
        {
            if (member.Name == GuidField || member.IsReadOnly)
                continue;
            if (member.SerializeToJson(contexts) is { } json)
                fields[member.Name] = json;
        }
        return fields;
    }

    // Written only where it differs, so a partial records no override of a value it already has.
    private static void Apply(IReadOnlyDictionary<string, string> fields, MemberReference root, CustomSerializationContext[] contexts)
    {
        foreach (var member in root.Members)
        {
            if (member.IsReadOnly || !fields.TryGetValue(member.Name, out var json) || member.SerializeToJson(contexts) == json)
                continue;
            member.SetValueFromJson(json, contexts);
        }
    }
}
