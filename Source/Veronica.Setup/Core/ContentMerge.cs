using Veronica.Agent;

namespace Veronica.Setup.Core;

public enum MergeKind
{
    Write,
    Delete,
    // The user changed the file: their version stays, the release's goes beside it as name.new.md.
    Conflict,
    // The user changed a file the release drops: an empty name.new.md, recorded as a drop in Veronica.installed.json.
    ConflictDropped,
    // The disk already holds what the release ships; a .new.md left from an earlier update is stale.
    ClearPending,
}

// Path is relative to the install root with '/' separators, e.g. "Skills/block-door.md".
public sealed record MergeAction(MergeKind Kind, string Path, byte[]? Content);

public sealed record MergeConflict(string Mine, string Incoming, bool Dropped);

// Skills and wiki are release content the user may edit (Docs/Tech/Distribution.md, Updates).
public static class ContentMerge
{
    public static IReadOnlyList<MergeAction> Plan(
        IReadOnlyDictionary<string, string> installed,
        IReadOnlyDictionary<string, string> disk,
        IReadOnlyDictionary<string, byte[]> release,
        IReadOnlySet<string> pending)
    {
        var actions = new List<MergeAction>();
        var paths = installed.Keys.Concat(release.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            string? shipped = installed.TryGetValue(path, out var s) ? s : null;
            string? onDisk = disk.TryGetValue(path, out var d) ? d : null;
            if (release.TryGetValue(path, out var content))
            {
                string incoming = Hashes.Sha256(content);
                if (onDisk == incoming)
                {
                    if (pending.Contains(path))
                        actions.Add(new MergeAction(MergeKind.ClearPending, path, null));
                    continue;
                }
                if (shipped == null)
                {
                    actions.Add(onDisk == null ? new MergeAction(MergeKind.Write, path, content) : new MergeAction(MergeKind.Conflict, path, content));
                    continue;
                }
                // A release that did not change the file leaves the user's edit (or deletion) alone.
                if (string.Equals(shipped, incoming, StringComparison.OrdinalIgnoreCase))
                    continue;
                actions.Add(onDisk == shipped ? new MergeAction(MergeKind.Write, path, content) : new MergeAction(MergeKind.Conflict, path, content));
            }
            else if (onDisk != null)
            {
                actions.Add(onDisk == shipped ? new MergeAction(MergeKind.Delete, path, null) : new MergeAction(MergeKind.ConflictDropped, path, []));
            }
            else if (pending.Contains(path))
            {
                actions.Add(new MergeAction(MergeKind.ClearPending, path, null));
            }
        }
        return actions;
    }

    // The drops still waiting after an update: earlier ones the user has not resolved, plus this plan's.
    public static HashSet<string> Dropped(IEnumerable<string> earlier, IReadOnlySet<string> pending, IEnumerable<MergeAction> plan)
    {
        var actions = plan.ToList();
        var touched = actions.Select(a => a.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dropped = earlier.Where(p => pending.Contains(p) && !touched.Contains(p)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        dropped.UnionWith(actions.Where(a => a.Kind == MergeKind.ConflictDropped).Select(a => a.Path));
        return dropped;
    }

    public static string IncomingPath(string path) =>
        path.EndsWith(Frontmatter.Extension, StringComparison.OrdinalIgnoreCase)
            ? path[..^Frontmatter.Extension.Length] + Frontmatter.PendingUpdateExtension
            : path + Frontmatter.PendingUpdateExtension;

    public static string MinePath(string incoming) => incoming[..^Frontmatter.PendingUpdateExtension.Length] + Frontmatter.Extension;

    // Files under the content folders as the plan sees them; pending .new.md files are not content.
    public static Dictionary<string, string> Snapshot(IFiles files, InstallLayout layout)
    {
        var disk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in InstallLayout.ContentFolders)
        {
            foreach (var file in files.Files(Path.Combine(layout.Root, folder), recursive: true))
            {
                if (Frontmatter.IsPendingUpdate(file))
                    continue;
                using var stream = files.OpenRead(file);
                disk[Relative(layout, file)] = Hashes.Sha256(stream);
            }
        }
        return disk;
    }

    // The content files that have a .new.md waiting beside them.
    public static HashSet<string> Pending(IFiles files, InstallLayout layout) =>
        InstallLayout.ContentFolders
            .SelectMany(folder => files.Files(Path.Combine(layout.Root, folder), recursive: true))
            .Where(Frontmatter.IsPendingUpdate)
            .Select(incoming => Relative(layout, MinePath(incoming)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static void Apply(IFiles files, InstallLayout layout, IEnumerable<MergeAction> actions)
    {
        foreach (var action in actions)
        {
            string full = Full(layout, action.Path);
            switch (action.Kind)
            {
                case MergeKind.Write:
                    files.WriteAllBytes(full, action.Content!);
                    files.Delete(IncomingPath(full));
                    break;
                case MergeKind.Delete:
                    files.Delete(full);
                    files.Delete(IncomingPath(full));
                    break;
                case MergeKind.Conflict:
                case MergeKind.ConflictDropped:
                    files.WriteAllBytes(IncomingPath(full), action.Content!);
                    break;
                case MergeKind.ClearPending:
                    files.Delete(IncomingPath(full));
                    break;
            }
        }
    }

    public static IReadOnlyList<MergeConflict> Conflicts(IFiles files, InstallLayout layout, InstalledRecord record) =>
        InstallLayout.ContentFolders
            .SelectMany(folder => files.Files(Path.Combine(layout.Root, folder), recursive: true))
            .Where(Frontmatter.IsPendingUpdate)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(incoming => new MergeConflict(MinePath(incoming), incoming, record.IsDropped(Relative(layout, MinePath(incoming)))))
            .ToList();

    public static void KeepMine(IFiles files, InstallLayout layout, MergeConflict conflict)
    {
        files.Delete(conflict.Incoming);
        Resolved(files, layout, conflict);
    }

    public static void UseNew(IFiles files, InstallLayout layout, MergeConflict conflict)
    {
        if (conflict.Dropped)
        {
            files.Delete(conflict.Mine);
            files.Delete(conflict.Incoming);
        }
        else
        {
            files.Move(conflict.Incoming, conflict.Mine, overwrite: true);
        }
        Resolved(files, layout, conflict);
    }

    private static void Resolved(IFiles files, InstallLayout layout, MergeConflict conflict)
    {
        var record = InstalledRecord.Load(files, layout);
        string path = Relative(layout, conflict.Mine);
        if (record.IsDropped(path))
            (record with { Dropped = record.Dropped.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)).ToList() }).Save(files, layout);
    }

    public static string Relative(InstallLayout layout, string full) =>
        Path.GetRelativePath(layout.Root, full).Replace('\\', '/');

    public static string Full(InstallLayout layout, string relative) =>
        Path.Combine(layout.Root, relative.Replace('/', Path.DirectorySeparatorChar));
}
