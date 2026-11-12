using System.Text.Json;
using Keen.VRage.ContentPipeline.Definitions.Utility;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.ContentPipeline.Project.Utils;
using Keen.VRage.Library.Definitions;
using Veronica.Editing;

namespace Veronica.Tools;

// The files create_prefab generated, named in the order its reply lists them; repairs append the files they create.
internal sealed record GeneratedPrefab(string Template, ExplorerFolder Folder, string Name, List<string> Files);

// Vanilla templates that fail the game's validation as they are, or make a block that cannot work, are put right
// after generation, the way vanilla's own blocks of the family have them. The reply lists the files a repair creates
// with the generated ones and never mentions the repair: the model could do nothing with it.
internal static class TemplateRepairs
{
    private static readonly Guid Reactor250Explosion = new("6a7db53a-1d2c-491f-a65f-4f84ba9fc695");
    private static readonly Guid HydrogenTank500Explosion = new("bc97dd7e-d81c-4642-ac68-79ea0088d7e0");
    private static readonly Guid Drill350Effects = new("b34b2c70-f868-4379-81f3-b6ed6f4b2da9");
    private static readonly Guid DoorClientBase = new("51b06007-072f-43da-82ae-ac7c1f0a4e03");
    private static readonly Guid Pivot = new("b80cb7d1-126f-406e-a1c7-6410109f3c1d");
    private static readonly Guid AtmosphericFlame = new("c3d8a15e-f0af-4c48-8718-7f09db3f6deb");
    private static readonly Guid AtmosphericThrusterKind = new("93e882df-b11a-4379-97ec-4176d195480f");

    // Run before the abstract rule, so an abstract reference with a better vanilla value keeps that value.
    private static readonly Dictionary<string, Func<Repair, Task>> ByTemplate = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vanilla sensors remove it: it needs an input adapter no block has.
        ["Blocks/Breakable/Sensor"] = r => r.RemoveComponent("BlockToolbarControllerComponent"),
        ["Blocks/Breakable/Reactors"] = Reactor,
        ["Blocks/Breakable/Tanks"] = r => r.Write(r.Generated("BlockDestructionExplosionDefinition"), "Explosion", r.PathOf(HydrogenTank500Explosion)),
        ["Blocks/Breakable/Drills"] = r => r.PointComponent("DrillToolEffectsComponent", r.PathOf(Drill350Effects)),
        ["Blocks/Breakable/Doors"] = Door,
        ["Blocks/Breakable/Production(Manual)"] = Production,
    };

    // What every vanilla block of the synthetic template's family sets over the template it is generated from.
    private static readonly Dictionary<string, Func<Repair, Task>> Synthetic = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Blocks/Breakable/Atmospheric Thrusters"] = AtmosphericThruster,
    };

    public static async Task ApplyAsync(GeneratedPrefab prefab, CancellationToken cancellationToken)
    {
        var repair = new Repair(prefab, cancellationToken);
        if (ByTemplate.TryGetValue(Templates.BaseOf(prefab.Template), out var specific))
            await specific(repair);
        if (Synthetic.TryGetValue(prefab.Template, out var recipe))
            await recipe(repair);
        else if (Templates.Synthetic.ContainsKey(prefab.Template))
            throw new InvalidOperationException($"The synthetic template {prefab.Template} has no recipe.");
        await CopyAbstractDefinitions(repair);
        await AddMissingPivot(repair);
    }

    // The mechanical templates (connectors, hinges, pistons, rotors and their heads) bring no dummies. The game's
    // check for a mechanical block's Pivot raises an assert dialog instead of an error, which holds validation until
    // someone answers it.
    private static Task AddMissingPivot(Repair r) => r.Run(project =>
    {
        if (r.OpenBlock(project)?.HasComponent("MechanicalBlockComponent") != true
            || r.Generated("BlockModelComponentDefinition")(project) is not { } model
            || project.GetItemByGuid(Pivot)?.ProjectRelativePath is not { } pivot)
            return 0;
        var asset = Content.Resolve(project, model);
        Content.RequireWritable(asset);
        var root = Content.Definition(asset).Fields.Root;
        var links = new ProjectLinks(project, asset);
        var (dummies, _) = FieldWalker.Walk(root, "Dummies", links: links);
        int count = dummies.GetValue<object?>() is null ? 0 : Fields.CollectionCount(Fields.Unwrap(dummies));
        for (int i = 0; i < count; i++)
        {
            if (FieldWalker.Walk(root, $"Dummies[{i}].Type", links: links).Member.GetValue<Definition?>()?.Guid == Pivot)
                return 0;
        }
        Elements.Add(root, "Dummies", null, null, links);
        FieldWriter.Write(root, $"Dummies[{count}].Type", JsonSerializer.SerializeToElement(pivot), links);
        Content.Save(asset);
        return 0;
    });

    // Vanilla atmospheric thrusters are the ion template with these class values; thrust, flame length and sound
    // differ per size and stay as the template has them.
    private static async Task AtmosphericThruster(Repair r)
    {
        await r.Write(r.Generated("ThrusterDefinition"), "ThrustClass", "Atmospheric");
        await r.Write(r.Generated("ThrusterEffectsComponentDefinition"), "OnThrustPoweredParticleEffect", r.PathOf(AtmosphericFlame));
        await r.Write(r.Generated("PowerableBlockDefinition"), "UIData.Name", "ThrusterAtmo");
        await r.Write(r.Generated("PowerableBlockDefinition"), "BlockKind", r.PathOf(AtmosphericThrusterKind));
    }

    // The template draws power idle and none while crafting, and queues nothing, so its block can never craft; every
    // vanilla production block draws nothing idle. Assembler500's crafting values stand in until the user picks theirs.
    private static async Task Production(Repair r)
    {
        await r.Write(r.Generated("PowerableBlockDefinition"), "ConsumedResource.Amount", 0);
        await r.Write(r.Generated("ItemConverterDefinition"), "ConsumedResource", 1600);
        await r.Write(r.Generated("ItemConverterDefinition"), "MaxQueueSize", 20);
    }

    // SlidingDoorNarrow250's values.
    private static async Task Door(Repair r)
    {
        await r.Write(r.Generated("DoorServerDefinition"), "OpeningDuration", "00:00:00.5");
        await r.Write(r.Generated("BoxShapeEntityDetectorDefinition"), "QueryHalfExtents", new { X = 0.6, Y = 1.0, Z = 0.125 });

        var client = Repair.Item(await r.Copy(DoorClientBase, $"{r.Prefab.Name}_DoorClientDefinition"));
        await r.Write(client, "ServerDefinition", r.Generated("DoorServerDefinition"));
        await r.PointComponent("DoorClientComponent", client);

        var provider = Repair.Item(await r.Create("ConditionalSubPartsDoorPartsProviderDefinition", $"{r.Prefab.Name}_ConditionalSubPartsDoorPartsProviderDefinition"));
        await r.Write(provider, "ConditionalSubPartsDefinition", r.Generated("ConditionalSubPartsDefinition"));
        await r.Write(r.Generated("DoorBlockColliderDefinition"), "SubPartsProvider", provider);
        await r.Write(r.Generated("DoorBlockColliderDefinition"), "ModelDefinition", r.Generated("BlockModelComponentDefinition"));
    }

    // The template's reactor explosion component points at a definition shared by every block made from it;
    // vanilla reactors have their own copy, with the reactor's blast.
    private static async Task Reactor(Repair r)
    {
        await r.Write(r.Generated("BlockDestructionExplosionDefinition"), "Explosion", r.PathOf(Reactor250Explosion));
        foreach (var copy in await CopyDefinitions(r, (_, component, _) => component == "FunctionalBlockDestructionExplosionComponent"))
            await r.Write(copy, "Explosion", r.PathOf(Reactor250Explosion));
    }

    // Vanilla blocks of these templates use a copy of the abstract base; the template points at the base itself.
    private static Task CopyAbstractDefinitions(Repair r) =>
        CopyDefinitions(r, (project, _, id) => project.GetAssetFileByGuid(id) is ExplorerAssetFile asset
            && ProjectDefinitionUtility.GetDefinitionFlags(asset).HasFlag(DefinitionFlags.Abstract));

    // Copies each definition the chosen components point at into the prefab's folder and points them at the copy.
    private static async Task<List<ItemOf>> CopyDefinitions(Repair r, Func<LoadedProject, string, Guid, bool> chosen)
    {
        var bases = await r.Run(project => r.OpenBlock(project)?.ComponentDefinitions()
            .Where(d => chosen(project, d.Name, d.Definition) && project.GetAssetFileByGuid(d.Definition) != null)
            .GroupBy(d => d.Definition)
            .Select(g => (Id: g.Key, Stem: Path.GetFileNameWithoutExtension(project.GetAssetFileByGuid(g.Key)!.Name), Components: g.Select(d => d.Name).ToList()))
            .ToList() ?? []);
        var copies = new List<ItemOf>();
        foreach (var (id, stem, components) in bases)
        {
            var copy = Repair.Item(await r.Copy(id, $"{r.Prefab.Name}_{stem}"));
            foreach (string component in components)
                await r.PointComponent(component, copy);
            copies.Add(copy);
        }
        return copies;
    }

    // A content item named when the step runs, so a step can name a file an earlier step made; null skips the step.
    private delegate string? ItemOf(LoadedProject project);

    // Each step is one call on the UI thread, so a file the Editor still holds after generation is waited out
    // and only that step runs again. A step whose item is missing is skipped: vanilla moved on.
    private sealed class Repair(GeneratedPrefab prefab, CancellationToken cancellationToken)
    {
        public GeneratedPrefab Prefab => prefab;

        public static ItemOf Item(string? path) => _ => path;

        public ItemOf PathOf(Guid id) => project => project.GetItemByGuid(id)?.ProjectRelativePath;

        // A generated definition by its type or a base type.
        public ItemOf Generated(string typeName) =>
            _ => Files().FirstOrDefault(f => Content.DefinitionTypeNames(f).Contains(typeName))?.ProjectRelativePath;

        public Task<T> Run<T>(Func<LoadedProject, T> work) => EditorProject.RunAsync(work, cancellationToken);

        private Task Run(Action<LoadedProject> work) => Run(project =>
        {
            work(project);
            return 0;
        });

        private IEnumerable<ExplorerAssetFile> Files() => prefab.Files.Select(prefab.Folder.GetItemByName).OfType<ExplorerAssetFile>();

        public Block? OpenBlock(LoadedProject project) =>
            Files().Where(f => Block.KeyOf(project, f) != null).Select(f => Block.TryOpen(project, f)).FirstOrDefault(b => b != null);

        public Task RemoveComponent(string component) => Run(project => OpenBlock(project)?.Remove(component));

        public Task PointComponent(string component, ItemOf definition) => Run(project =>
        {
            if (definition(project) is { } path)
                OpenBlock(project)?.Write(FieldPath.Member(ComponentNames.PathOf(component), "Definition"), JsonSerializer.SerializeToElement(path));
        });

        public Task Write(ItemOf target, string path, ItemOf reference) =>
            Write(target, path, project => reference(project) is { } item ? JsonSerializer.SerializeToElement(item) : null);

        public Task Write(ItemOf target, string path, object value) =>
            Write(target, path, _ => JsonSerializer.SerializeToElement(value));

        private Task Write(ItemOf target, string path, Func<LoadedProject, JsonElement?> value) => Run(project =>
        {
            if (target(project) is not { } file || value(project) is not { } json)
                return;
            var asset = Content.Resolve(project, file);
            Content.RequireWritable(asset);
            FieldWriter.Write(Content.Definition(asset).Fields.Root, path, json, new ProjectLinks(project, asset));
            Content.Save(asset);
        });

        // The new file's path; null when the source is missing.
        public async Task<string?> Copy(Guid source, string name) =>
            Added(await EditorProject.RunAsync(async project => project.GetAssetFileByGuid(source) is ExplorerAssetFile asset
                ? await CreatePartialTool.CreateCopyAsync(project, asset, prefab.Folder, name)
                : null, cancellationToken));

        public async Task<string?> Create(string type, string name) =>
            Added(await Run(_ => CreateDefinitionTool.CreateFile(type, prefab.Folder, name)));

        private string? Added(string? fileName)
        {
            if (fileName == null)
                return null;
            prefab.Files.Add(fileName);
            return Path.Combine(prefab.Folder.ProjectRelativePath, fileName);
        }
    }
}
