using Keen.VRage.ContentPipeline.Definitions.AssetInterfaces;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Library.Serialization;
using Veronica.Editing;

namespace Veronica.Tools;

// Content items as the tools name them: project-relative paths, or bare file names when unique.
// The Editor holds the item for a moment; trying again shortly is expected to work.
internal sealed class ItemBusy(string message) : Exception(message);

internal static class Content
{
    public const int MaxCandidates = 10;

    public static ExplorerAssetFile Resolve(LoadedProject project, string target)
    {
        var matches = project.GetAvailableFiles().OfType<ExplorerAssetFile>()
            .Where(f => ContentNames.Matches(f.ProjectRelativePath, target))
            .ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new EditError(project.GetAvailableFiles().Any(f => ContentNames.Matches(f.ProjectRelativePath, target))
                ? $"'{target}' is a source file, not a definition or asset."
                : $"No content item named '{target}'; look it up with find_content."),
            _ => throw new EditError($"'{target}' matches several items; use a full path: {Paths(matches)}."),
        };
    }

    public static (ExplorerAssetFile Asset, ObjectManipulator Fields) Definition(LoadedProject project, string target)
    {
        var asset = Resolve(project, target);
        if (asset.AssetInterface is not IDefinitionAssetInterface definition)
            throw new EditError($"'{asset.ProjectRelativePath}' is not a definition; only definitions have fields to read and write.");
        return (asset, definition.GetDefinitionManipulator());
    }

    public static void RequireWritable(ExplorerItem item)
    {
        var state = item.CurrentReadOnly;
        if (!state.IsReadOnly)
            return;
        // A file of a writable project is locked only while the Editor works on it, e.g. right after it was created.
        if (item is ExplorerFile && !item.Project.IsReadOnly)
            throw new ItemBusy($"'{Shown(item)}' stayed locked by the Editor: {state.Reason ?? "in use"}.");
        throw new EditError($"'{Shown(item)}' is read-only: {state.Reason ?? "the Editor locks it"}.");
    }

    // The same path an inspector edit ends in: mark modified, auto-save, and rebuild an open inspector.
    public static void Save(ExplorerAssetFile asset) => asset.AssetInterface.OnInterfaceDataModified(forceValuesReload: true);

    public static string? DefinitionTypeName(ExplorerItem item)
    {
        try
        {
            return ((item as ExplorerAssetFile)?.AssetInterface as IDefinitionAssetInterface)?.DefinitionType.Name;
        }
        catch (InvalidOperationException)
        {
            // A partial definition whose base is gone has no type; one such file must not break every listing.
            return null;
        }
    }

    // One item on its own line, e.g. a read header: a dependency's item names its project instead of a reason.
    public static string Describe(ExplorerItem item)
    {
        string line = Line(item);
        return item.Project.IsReadOnly ? $"{line} in {item.Project.Name} (read-only)" : line;
    }

    // An item inside a per-project listing, whose header already says when the whole project is read-only.
    // A file's own lock is not shown: it passes, and changes wait it out.
    public static string Line(ExplorerItem item) =>
        DefinitionTypeName(item) is { } type ? $"{Shown(item)} ({type})" : Shown(item);

    // Items grouped under one header per project: the mod first, then its dependencies in load order.
    public static List<string> ByProject(LoadedProject root, IEnumerable<ExplorerItem> items)
    {
        var order = ProjectOrder(root);
        var lines = new List<string>();
        foreach (var group in items.GroupBy(i => i.Project).OrderBy(g => order.GetValueOrDefault(g.Key, int.MaxValue)))
        {
            lines.Add($"{group.Key.Name}{(group.Key.IsReadOnly ? " (read-only)" : "")}:");
            lines.AddRange(group.Select(i => "  " + Line(i)));
        }
        return lines;
    }

    // The mod first, then its dependencies in load order.
    public static Dictionary<LoadedProject, int> ProjectOrder(LoadedProject root) =>
        root.EnumerateProjectDAG().Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);

    public static string Shown(ExplorerItem item) => item.ProjectRelativePath.Length == 0 ? "(project root)" : item.ProjectRelativePath;

    public static string Paths(IEnumerable<ExplorerItem> items)
    {
        var list = items.Select(i => i.ProjectRelativePath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        string shown = string.Join(", ", list.Take(MaxCandidates));
        return list.Count > MaxCandidates ? $"{shown}, ... ({list.Count - MaxCandidates} more)" : shown;
    }
}
