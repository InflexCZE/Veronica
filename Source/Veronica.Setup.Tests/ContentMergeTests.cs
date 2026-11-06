using System.Text;
using Veronica.Setup.Core;
using Xunit;

namespace Veronica.Setup.Tests;

public class ContentMergeTests
{
    private static readonly InstallLayout Layout = new(@"C:\V");
    private static readonly HashSet<string> NonePending = [];

    private static byte[] B(string text) => Encoding.UTF8.GetBytes(text);

    private static string H(string text) => Hashes.Sha256(B(text));

    private static Dictionary<string, string> Map(params (string Path, string Text)[] files) =>
        files.ToDictionary(f => f.Path, f => H(f.Text), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, byte[]> Release(params (string Path, string Text)[] files) =>
        files.ToDictionary(f => f.Path, f => B(f.Text), StringComparer.OrdinalIgnoreCase);

    private static (MergeKind Kind, string Path, string? Content) One(IReadOnlyList<MergeAction> plan)
    {
        var action = Assert.Single(plan);
        return (action.Kind, action.Path, action.Content == null ? null : Encoding.UTF8.GetString(action.Content));
    }

    [Fact]
    public void FreshInstall_WritesEveryFile()
    {
        var plan = ContentMerge.Plan(Map(), Map(), Release(("Skills/a.md", "a1"), ("Wiki/p.md", "p1")), NonePending);

        Assert.Equal([MergeKind.Write, MergeKind.Write], plan.Select(a => a.Kind));
    }

    [Fact]
    public void UnchangedFile_TakesTheNewVersion()
    {
        var plan = ContentMerge.Plan(Map(("Skills/a.md", "a1")), Map(("Skills/a.md", "a1")), Release(("Skills/a.md", "a2")), NonePending);

        Assert.Equal((MergeKind.Write, "Skills/a.md", "a2"), One(plan));
    }

    [Fact]
    public void ChangedFile_ChangedByTheRelease_IsAConflictCarryingTheNewVersion()
    {
        var plan = ContentMerge.Plan(Map(("Skills/a.md", "a1")), Map(("Skills/a.md", "mine")), Release(("Skills/a.md", "a2")), NonePending);

        Assert.Equal((MergeKind.Conflict, "Skills/a.md", "a2"), One(plan));
    }

    [Fact]
    public void ChangedFile_TheReleaseLeftAlone_StaysWithoutAConflict()
    {
        var plan = ContentMerge.Plan(Map(("Skills/a.md", "a1")), Map(("Skills/a.md", "mine")), Release(("Skills/a.md", "a1")), NonePending);

        Assert.Empty(plan);
    }

    [Fact]
    public void DroppedFile_Unchanged_IsDeleted()
    {
        var plan = ContentMerge.Plan(Map(("Wiki/p.md", "p1")), Map(("Wiki/p.md", "p1")), Release(), NonePending);

        Assert.Equal((MergeKind.Delete, "Wiki/p.md", null), One(plan));
    }

    [Fact]
    public void DroppedFile_Changed_IsADroppedConflictWithAnEmptyNewVersion()
    {
        var plan = ContentMerge.Plan(Map(("Wiki/p.md", "p1")), Map(("Wiki/p.md", "mine")), Release(), NonePending);

        Assert.Equal((MergeKind.ConflictDropped, "Wiki/p.md", ""), One(plan));
    }

    [Fact]
    public void UserAddedFile_IsLeftAlone()
    {
        var plan = ContentMerge.Plan(Map(), Map(("Skills/own.md", "mine")), Release(), NonePending);

        Assert.Empty(plan);
    }

    [Fact]
    public void DeletedShippedFile_ChangedByTheRelease_IsAConflict()
    {
        var plan = ContentMerge.Plan(Map(("Skills/a.md", "a1")), Map(), Release(("Skills/a.md", "a2")), NonePending);

        Assert.Equal((MergeKind.Conflict, "Skills/a.md", "a2"), One(plan));
    }

    [Fact]
    public void NewShippedFile_ClashingWithAUserFile_IsAConflict()
    {
        var plan = ContentMerge.Plan(Map(), Map(("Skills/a.md", "mine")), Release(("Skills/a.md", "a1")), NonePending);

        Assert.Equal((MergeKind.Conflict, "Skills/a.md", "a1"), One(plan));
    }

    [Fact]
    public void DiskAlreadyHoldingTheNewVersion_NeedsNothing_ButClearsAStalePendingFile()
    {
        var installed = Map(("Skills/a.md", "a1"));
        var disk = Map(("Skills/a.md", "a2"));
        var release = Release(("Skills/a.md", "a2"));

        Assert.Empty(ContentMerge.Plan(installed, disk, release, NonePending));
        Assert.Equal((MergeKind.ClearPending, "Skills/a.md", null), One(ContentMerge.Plan(installed, disk, release, new HashSet<string>(["Skills/a.md"], StringComparer.OrdinalIgnoreCase))));
    }

    [Fact]
    public void DroppedList_KeepsUnresolvedDrops_AndAddsNewOnes()
    {
        var plan = new[] { new MergeAction(MergeKind.ConflictDropped, "Wiki/q.md", []), new MergeAction(MergeKind.Write, "Wiki/r.md", B("r")) };

        var dropped = ContentMerge.Dropped(["Wiki/p.md", "Wiki/r.md", "Wiki/resolved.md"], new HashSet<string>(["Wiki/p.md", "Wiki/r.md"], StringComparer.OrdinalIgnoreCase), plan);

        Assert.Equal(["Wiki/p.md", "Wiki/q.md"], dropped.Order());
    }

    [Fact]
    public void Apply_WritesConflictsBesideTheUsersFile_AndListsThem()
    {
        var files = new MemoryFiles();
        files.WriteAllText(@"C:\V\Skills\a.md", "mine");
        files.WriteAllText(@"C:\V\Wiki\p.md", "mine too");

        ContentMerge.Apply(files, Layout,
        [
            new MergeAction(MergeKind.Conflict, "Skills/a.md", B("a2")),
            new MergeAction(MergeKind.ConflictDropped, "Wiki/p.md", []),
            new MergeAction(MergeKind.Write, "Skills/b.md", B("b1")),
        ]);
        var record = InstalledRecord.Empty with { Dropped = ["Wiki/p.md"] };

        Assert.Equal("mine", files.ReadAllText(@"C:\V\Skills\a.md"));
        Assert.Equal("a2", files.ReadAllText(@"C:\V\Skills\a.new.md"));
        Assert.Equal("b1", files.ReadAllText(@"C:\V\Skills\b.md"));
        var conflicts = ContentMerge.Conflicts(files, Layout, record);
        Assert.Equal([@"C:\V\Skills\a.md", @"C:\V\Wiki\p.md"], conflicts.Select(c => c.Mine));
        Assert.Equal([false, true], conflicts.Select(c => c.Dropped));
    }

    [Fact]
    public void EmptyNewVersion_IsNotADrop_UnlessRecordedAsOne()
    {
        var files = new MemoryFiles();
        files.WriteAllText(@"C:\V\Skills\a.md", "mine");
        files.WriteAllBytes(@"C:\V\Skills\a.new.md", []);
        var conflict = Assert.Single(ContentMerge.Conflicts(files, Layout, InstalledRecord.Empty));

        ContentMerge.UseNew(files, Layout, conflict);

        Assert.Equal("", files.ReadAllText(@"C:\V\Skills\a.md"));
    }

    [Fact]
    public void Snapshot_IgnoresPendingNewFiles()
    {
        var files = new MemoryFiles();
        files.WriteAllText(@"C:\V\Skills\a.md", "mine");
        files.WriteAllText(@"C:\V\Skills\a.new.md", "a2");

        Assert.Equal(["Skills/a.md"], ContentMerge.Snapshot(files, Layout).Keys);
        Assert.Equal(["Skills/a.md"], ContentMerge.Pending(files, Layout));
    }

    [Fact]
    public void Resolve_KeepMine_UseNew_AndUseNewOfADroppedFile()
    {
        var files = new MemoryFiles();
        files.WriteAllText(@"C:\V\Skills\a.md", "mine");
        files.WriteAllText(@"C:\V\Skills\a.new.md", "a2");
        files.WriteAllText(@"C:\V\Skills\b.md", "mine");
        files.WriteAllText(@"C:\V\Skills\b.new.md", "b2");
        files.WriteAllText(@"C:\V\Wiki\p.md", "mine");
        files.WriteAllBytes(@"C:\V\Wiki\p.new.md", []);
        (InstalledRecord.Empty with { Dropped = ["Wiki/p.md"] }).Save(files, Layout);
        var conflicts = ContentMerge.Conflicts(files, Layout, InstalledRecord.Load(files, Layout));

        ContentMerge.KeepMine(files, Layout, conflicts.Single(c => c.Mine.EndsWith("a.md")));
        ContentMerge.UseNew(files, Layout, conflicts.Single(c => c.Mine.EndsWith("b.md")));
        ContentMerge.UseNew(files, Layout, conflicts.Single(c => c.Mine.EndsWith("p.md")));

        Assert.Equal("mine", files.ReadAllText(@"C:\V\Skills\a.md"));
        Assert.Equal("b2", files.ReadAllText(@"C:\V\Skills\b.md"));
        Assert.False(files.FileExists(@"C:\V\Wiki\p.md"));
        Assert.Empty(ContentMerge.Conflicts(files, Layout, InstalledRecord.Load(files, Layout)));
        Assert.Empty(InstalledRecord.Load(files, Layout).Dropped);
    }

    [Fact]
    public void UnreadableRecord_CountsAsNone_WithAWarning()
    {
        var files = new MemoryFiles();
        files.WriteAllText(Layout.InstalledFile, "{ not json");

        var record = InstalledRecord.Load(files, Layout, out var warning);

        Assert.Same(InstalledRecord.Empty, record);
        Assert.Contains("could not be read", warning);
    }
}
