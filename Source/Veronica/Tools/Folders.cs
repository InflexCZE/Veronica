using Keen.VRage.ContentPipeline.Project.Structure;
using Veronica.Editing;

namespace Veronica.Tools;

// Folders of the loaded mod, created like mkdir -p.
internal static class Folders
{
    // assetsOnly: a folder for definitions, refused under AssetSources before anything is created there.
    public static (ExplorerFolder Folder, bool Created) Ensure(LoadedProject project, string folderPath, bool assetsOnly = false)
    {
        string[] segments = ContentNames.Segments(folderPath);
        if (segments.Length == 0)
            throw new EditError("Give a folder path inside the mod, e.g. Assets/MyThruster.");

        ExplorerFolder folder = project.GetRootFolder();
        bool created = false;
        foreach (string segment in segments)
        {
            if (assetsOnly && folder is ExplorerAssetSourcesFolder)
                throw new EditError($"{Content.Shown(folder)} holds source files; create definitions under Assets.");
            switch (folder.GetItemByName(segment))
            {
                case ExplorerFolder existing:
                    folder = existing;
                    continue;
                case { } file:
                    throw new EditError($"'{file.ProjectRelativePath}' is a file, not a folder.");
            }

            Content.RequireWritable(folder);
            if (folder.CreateSubfolder(segment) == null || folder.GetItemByName(segment) is not ExplorerFolder made)
                throw new EditError($"The Editor could not create '{segment}' in '{Content.Shown(folder)}'.");
            folder = made;
            created = true;
        }
        if (assetsOnly && folder is ExplorerAssetSourcesFolder)
            throw new EditError($"{Content.Shown(folder)} holds source files; create definitions under Assets.");
        return (folder, created);
    }

    // Folders that already exist, named the way targets are: an exact path always wins, then a leading project
    // name narrows the search, then a shortened path finds its one folder, and only then is a bare project name
    // that project's root, so a project named like a folder never hides the folder. A folder that exists in several
    // projects comes once per project. A folder to create is never looked up this way: Ensure takes its path as given.
    public static List<(LoadedProject Owner, ExplorerFolder Folder)> Existing(LoadedProject project, string folderPath)
    {
        var owners = project.EnumerateProjectDAG().ToList();
        string[] segments = ContentNames.Segments(folderPath);

        var exact = Exact(owners, segments);
        if (exact.Count > 0 || segments.Length == 0)
            return exact;

        if (segments.Length > 1 && owners.FirstOrDefault(o => Same(o.Name, segments[0])) is { } named)
        {
            var inProject = Exact([named], segments[1..]);
            if (inProject.Count == 0)
                inProject = Ending([named], segments[1..], folderPath);
            if (inProject.Count > 0)
                return inProject;
        }
        var ending = Ending(owners, segments, folderPath);
        if (ending.Count > 0)
            return ending;
        if (ContentNames.ProjectRoot(owners.Select(o => o.Name), folderPath) is { } root)
            return owners.Where(o => Same(o.Name, root)).Select(o => (o, (ExplorerFolder)o.GetRootFolder())).ToList();
        return ending;
    }

    public static string NotFound(LoadedProject project, string folderPath, string next)
    {
        var folders = project.EnumerateProjectDAG().SelectMany(o => All(o.GetRootFolder()));
        var nearest = ContentNames.Nearest(folders, f => f.ProjectRelativePath, f => f.Project == project, folderPath);
        string hint = nearest.Count > 0 ? $"; nearest: {string.Join(", ", nearest.Select(f => $"{f.ProjectRelativePath} in {f.Project.Name}"))}" : "";
        return $"No folder '{folderPath}' in the mod or its dependencies{hint}; {next}.";
    }

    private static List<(LoadedProject, ExplorerFolder)> Exact(List<LoadedProject> owners, string[] segments)
    {
        var found = new List<(LoadedProject, ExplorerFolder)>();
        foreach (var owner in owners)
            if (Walk(owner, segments) is { } folder)
                found.Add((owner, folder));
        return found;
    }

    private static List<(LoadedProject, ExplorerFolder)> Ending(List<LoadedProject> owners, string[] segments, string folderPath)
    {
        if (segments.Length == 0)
            return [];
        var ending = owners.SelectMany(o => All(o.GetRootFolder()).Where(f => EndsWith(f, segments)).Select(f => (o, f))).ToList();
        var paths = ending.Select(e => e.f.ProjectRelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (paths.Count > 1)
            throw new EditError($"'{folderPath}' matches several folders; use a full path: {string.Join(", ", paths.Take(Content.MaxCandidates))}{(paths.Count > Content.MaxCandidates ? ", ..." : "")}.");
        return ending;
    }

    private static ExplorerFolder? Walk(LoadedProject owner, string[] segments)
    {
        ExplorerFolder? folder = owner.GetRootFolder();
        foreach (string segment in segments)
            folder = folder?.GetItemByName(segment) as ExplorerFolder;
        return folder;
    }

    private static IEnumerable<ExplorerFolder> All(ExplorerFolder folder)
    {
        foreach (var child in folder.ChildItems.OfType<ExplorerFolder>())
        {
            yield return child;
            foreach (var below in All(child))
                yield return below;
        }
    }

    private static bool EndsWith(ExplorerFolder folder, string[] wanted)
    {
        string[] path = ContentNames.Segments(folder.ProjectRelativePath);
        return path.Length >= wanted.Length && path[^wanted.Length..].Zip(wanted).All(p => Same(p.First, p.Second));
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
