using Veronica.Editing;
using Xunit;

namespace Veronica.Tests.Editing;

public sealed class OverrideLinkTests
{
    private sealed record Item(Guid Id, string Path, Item? Original = null);

    private static readonly Item Original = new(Guid.NewGuid(), @"Vanilla\Effects\Flame.def");
    private static readonly Item Override = new(Guid.NewGuid(), @"Mods\Red\Flame.partialdef", Original);
    private static readonly Item Copy = new(Guid.NewGuid(), @"Mods\Red\RedFlame.partialdef");

    private static List<Item> Resolve(params Item[] named) => OverrideLinks.InPlaceOfOverrides(named, i => i.Original, i => i.Id);

    [Fact]
    public void An_override_stands_for_its_original() => Assert.Equal([Original], Resolve(Override));

    [Fact]
    public void A_name_shared_by_original_and_override_is_the_original_once() => Assert.Equal([Original], Resolve(Original, Override));

    [Fact]
    public void A_copy_stays_itself() => Assert.Equal([Copy], Resolve(Copy));
}
