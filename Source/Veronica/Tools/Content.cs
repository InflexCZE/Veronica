using Keen.VRage.ContentPipeline.Definitions.AssetInterfaces;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Library.Serialization;
using Veronica.Editing;

namespace Veronica.Tools;

// Content items as the tools name them: project-relative paths, or bare file names when unique.
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
        if (state.IsReadOnly)
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

    public static string Describe(ExplorerItem item)
    {
        var parts = new List<string>();
        if (DefinitionTypeName(item) is { } type)
            parts.Add(type);
        if (item.CurrentReadOnly is { IsReadOnly: true } state)
            parts.Add($"read-only: {state.Reason ?? "locked"}");
        return parts.Count == 0 ? Shown(item) : $"{Shown(item)} ({string.Join(", ", parts)})";
    }

    public static string Shown(ExplorerItem item) => item.ProjectRelativePath.Length == 0 ? "(project root)" : item.ProjectRelativePath;

    public static string Paths(IEnumerable<ExplorerItem> items)
    {
        var list = items.Select(i => i.ProjectRelativePath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        string shown = string.Join(", ", list.Take(MaxCandidates));
        return list.Count > MaxCandidates ? $"{shown}, ... ({list.Count - MaxCandidates} more)" : shown;
    }
}
