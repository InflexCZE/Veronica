using System.Text.Json;
using Keen.VRage.ContentPipeline.Definitions.AssetInterfaces;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Library.Definitions;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class FindReferencesTool : ITool
{
    private const int MaxResults = 50;

    public string Name => "find_references";

    public string Description =>
        "Lists every content item that points at an item: definitions, blocks (by the component field that holds the reference), copies and overrides of it, in the mod and the projects it depends on, such as Vanilla. " +
        "One step only: to follow a chain, call it again on a referrer. The count it gives is complete, so a short list means nothing else uses the item.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the item, or its file name if unique"}},"required":["target"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        return new ToolResult(await EditorProject.RunAsync(project => Find(project, target), cancellationToken));
    }

    private static string Find(LoadedProject project, string target)
    {
        var asset = Content.Resolve(project, target);
        var index = project.ReferencesManager;
        if (!index.Enabled)
            throw new EditError("This project keeps no reference index, so what points at an item cannot be listed.");

        var lines = new List<string>();
        var subject = asset;
        var targets = new HashSet<Guid> { asset.AssetID };
        // An override takes the original's place: what points at the original is what the override changes.
        if (asset.AssetInterface is PartialDefinitionAssetInterface { PartialDefinitionKind: PartialDefinitionKind.Override } partial)
        {
            lines.Add($"{Content.Shown(asset)} is an override; it changes the original wherever the original is used.");
            targets = [partial.BaseDefinition];
            if (project.GetItemByGuid(partial.BaseDefinition) is ExplorerAssetFile original)
                subject = original;
        }
        // A block is referenced through any of its files.
        else if (Block.TryOpen(project, asset) is { } block)
        {
            subject = block.Item;
            targets = block.FileIds().ToHashSet();
        }

        var own = new HashSet<Guid>(targets) { asset.AssetID };
        var referrers = targets.SelectMany(t => index.GetDirectDependenciesTo(t).Select(d => d.guid)).ToHashSet();
        // A partial's link to its base is not always in the index.
        if (subject.AssetInterface is IDefinitionAssetInterface definition)
            referrers.UnionWith(definition.PartialDefinitions.Select(p => p.AssetID));
        referrers.ExceptWith(own);

        var order = Content.ProjectOrder(project);
        var found = referrers.Select(g => project.GetItemByGuid(g)).ToList();
        int unseen = found.Count(i => i == null);
        var items = found.OfType<ExplorerItem>()
            .OrderBy(i => order.GetValueOrDefault(i.Project, int.MaxValue))
            .ThenBy(i => i.ProjectRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // One entry per block, keyed without loading it, so the count is of rows and a block is opened once.
        var entries = new List<ExplorerItem>();
        var blocks = new HashSet<Guid>();
        foreach (var item in items)
        {
            if (item is ExplorerAssetFile file && Block.KeyOf(project, file) is { } key && !blocks.Add(key))
                continue;
            entries.Add(item);
        }

        string name = Content.Shown(subject);
        if (entries.Count == 0 && unseen == 0)
        {
            lines.Add($"Nothing references {name}.");
            return string.Join("\n", lines);
        }

        var rows = entries.Take(MaxResults).Select(item => Row(project, item, targets)).ToList();
        lines.Add(entries.Count == 1 ? $"1 item references {name}:" : $"{entries.Count} items reference {name}:");
        lines.AddRange(Content.ByProject(project, rows));
        if (entries.Count > MaxResults)
            lines.Add($"... {entries.Count - MaxResults} more not shown.");
        if (unseen > 0)
            lines.Add(unseen == 1 ? "1 more reference comes from an item the project does not list." : $"{unseen} more references come from items the project does not list.");
        return string.Join("\n", lines);
    }

    private static (ExplorerItem, string) Row(LoadedProject project, ExplorerItem item, IReadOnlySet<Guid> targets)
    {
        try
        {
            if (item is not ExplorerAssetFile asset)
                return (item, $"{Content.Line(item)}: refers to it");

            if (Block.TryOpen(project, asset) is { } block)
            {
                var files = block.FileIds().Select(project.GetItemByGuid).OfType<ExplorerAssetFile>();
                var blockFound = files.Select(f => Derived(f, targets)).OfType<string>().Distinct().ToList();
                blockFound.AddRange(block.FindLinks(targets));
                return (block.Item, $"{Content.Shown(block.Item)} (block): {Where(blockFound)}");
            }

            var found = new List<string>();
            if (Derived(asset, targets) is { } derived)
                found.Add(derived);
            if (asset.AssetInterface is IDefinitionAssetInterface)
            {
                var (_, fields) = Content.Definition(asset);
                found.AddRange(FieldReader.FindLinks(fields.Root, targets, new ProjectLinks(project, asset)));
            }
            return (asset, $"{Content.Line(asset)}: {Where(found)}");
        }
        // One broken referrer must not cost the whole list; it still counts and shows.
        catch (Exception e) when (e is not ItemBusy)
        {
            return (item, $"{Content.Line(item)}: refers to it");
        }
    }

    private static string? Derived(ExplorerAssetFile asset, IReadOnlySet<Guid> targets) =>
        asset.AssetInterface is PartialDefinitionAssetInterface { PartialDefinitionKind: not PartialDefinitionKind.None } partial && targets.Contains(partial.BaseDefinition)
            ? partial.PartialDefinitionKind == PartialDefinitionKind.Override ? "override of it" : "copy of it"
            : null;

    private static string Where(List<string> found) => found.Count == 0 ? "refers to it" : string.Join(", ", found);
}
