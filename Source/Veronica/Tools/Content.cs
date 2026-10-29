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
        var matches = Pick(project.GetAvailableFiles().OfType<ExplorerAssetFile>().ToList(), target);
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new EditError(Pick(project.GetAvailableFiles().ToList(), target).Count > 0
                ? $"'{target}' is a source file, not a definition or asset."
                : NotFound(project, project.GetAvailableFiles().OfType<ExplorerAssetFile>(), target)),
            _ => throw new EditError(Ambiguous(target, matches)),
        };
    }

    // Hints come from the items the caller can take, so a target never gets a source file it would refuse.
    public static string NotFound(LoadedProject project, IEnumerable<ExplorerItem> candidates, string target)
    {
        var nearest = ContentNames.Nearest(candidates, i => i.ProjectRelativePath, i => i.Project == project, target);
        string hint = nearest.Count > 0 ? $"; nearest: {string.Join(", ", nearest.Select(i => $"{i.ProjectRelativePath} in {i.Project.Name}"))}" : "";
        return $"No content item named '{target}'{hint}; look it up with find_content.";
    }

    // Several projects can hold one path; only the project name in front tells them apart.
    public static string Ambiguous(string target, IReadOnlyCollection<ExplorerItem> items)
    {
        var list = items.OrderBy(i => i.ProjectRelativePath, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Project.Name).ToList();
        string shown = string.Join(", ", list.Take(MaxCandidates).Select(i => $"{i.ProjectRelativePath} in {i.Project.Name}"));
        if (list.Count > MaxCandidates)
            shown += $", ... ({list.Count - MaxCandidates} more)";
        string message = $"'{target}' matches several items; use a full path: {shown}.";
        var shared = list.GroupBy(i => i.ProjectRelativePath, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (shared != null)
            message += $" To pick one of the same path, lead it with its project name, e.g. {shared.First().Project.Name}/{shared.Key.Replace('\\', '/')}.";
        return message;
    }

    public static List<T> Pick<T>(IReadOnlyCollection<T> items, string query) where T : ExplorerItem =>
        ContentNames.Pick(items, i => i.ProjectRelativePath, i => i.Project.Name, query);

    public static (ExplorerAssetFile Asset, ObjectManipulator Fields) Definition(LoadedProject project, string target) =>
        Definition(Resolve(project, target));

    public static (ExplorerAssetFile Asset, ObjectManipulator Fields) Definition(ExplorerAssetFile asset)
    {
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

    public static string? DefinitionTypeName(ExplorerItem item) => DefinitionType(item)?.Name;

    // The definition type first, then its base types: a query for a base type finds every derived definition.
    public static List<string> DefinitionTypeNames(ExplorerItem item)
    {
        var names = new List<string>();
        for (var type = DefinitionType(item); type != null && type != typeof(object); type = type.BaseType)
            names.Add(type.Name);
        return names;
    }

    private static Type? DefinitionType(ExplorerItem item)
    {
        try
        {
            return ((item as ExplorerAssetFile)?.AssetInterface as IDefinitionAssetInterface)?.DefinitionType;
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
}
