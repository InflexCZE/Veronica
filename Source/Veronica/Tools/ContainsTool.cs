using System.Text.Json;
using Keen.VRage.ContentPipeline.Project.Structure;
using Veronica.Agent;
using Veronica.Agent.Tools;
using Veronica.Editing;

namespace Veronica.Tools;

public sealed class ContainsTool : ITool
{
    // A short name can match many files; past this many it names nothing in particular.
    private const int MaxNamedItems = 10;

    public string Name => "contains";

    public string Description =>
        "Checks whether a definition holds a value, without reading long lists, sets or dictionaries whole. Lists each match as read prints it: " +
        "a list or set element, a dictionary key or value, or a field of an element. A value naming a block matches its prefab binding and both of its prefabs. " +
        "Text matches whole values, ignoring case; with no whole match, the values that contain it are listed.";

    public string Parameters => """
        {"type":"object","properties":{"target":{"type":"string","description":"Project path of the definition, or its file name if unique"},"path":{"type":"string","description":"Field path as read prints it, e.g. Blocks; empty searches the whole definition"},"value":{"type":"string","description":"What to look for: an item's name or path, or a value as read prints it"}},"required":["target","value"]}
        """;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        string target = Args.Text(arguments, "target");
        string path = Args.OptionalText(arguments, "path") ?? "";
        string value = Args.Text(arguments, "value");
        return new ToolResult(await EditorProject.RunAsync(project =>
        {
            var resolved = Content.Resolve(project, target);
            var items = NamedItems(project, value, out string? tooMany);
            string scope = path.Trim().Length == 0 ? "the whole definition" : path.Trim();
            string note = tooMany == null ? "" : "\n" + tooMany;
            if (Block.TryOpen(project, resolved) is { } block)
                return $"{Content.Describe(block.Item)}:\n{FieldSearch.Search(block.SearchEntries(path), value, items, scope)}{note}";
            var (asset, fields) = Content.Definition(resolved);
            var entries = FieldReader.Entries(fields.Root, path, new ProjectLinks(project, asset));
            return $"{Content.Describe(asset)}:\n{FieldSearch.Search(entries, value, items, scope)}{note}";
        }, cancellationToken));
    }

    // The items the value names, a block with all of its files, so a reference to any of them is a match.
    private static HashSet<Guid> NamedItems(LoadedProject project, string value, out string? tooMany)
    {
        tooMany = null;
        var ids = new HashSet<Guid>();
        string name = value.Trim().Trim('"');
        if (name.Length == 0)
            return ids;
        var named = Content.Pick(project.GetAvailableFiles().OfType<ExplorerAssetFile>().ToList(), name);
        if (named.Count > MaxNamedItems)
        {
            tooMany = $"\"{name}\" names {named.Count} items, more than {MaxNamedItems}, so references to them were not matched, only text. " +
                "To match references to one item, give its project path or its full file name.";
            return ids;
        }
        foreach (var asset in named)
        {
            ids.Add(asset.AssetID);
            try
            {
                if (Block.TryOpen(project, asset) is { } block)
                    ids.UnionWith(block.FileIds());
            }
            // A broken block still matches by its own file.
            catch (EditError)
            {
            }
        }
        return ids;
    }
}
