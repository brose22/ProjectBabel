using System.Linq;
using NUnit.Framework;

public class ListFilterTests
{
    // Class (4), element (4), rarity (5), like a soldier list.
    static ListFilter Soldiers() => new(4, 4, 5);

    [Test]
    public void New_IsAllAndMatchesEverything()
    {
        var filter = Soldiers();
        Assert.That(filter.IsAll);
        Assert.That(filter.Matches(0, 3, 4));
        Assert.That(filter.Matches(2, ListFilter.All, 0), "a soldier with no element still shows under All");
    }

    [Test]
    public void Picks_CombineWithAnd()
    {
        var filter = Soldiers();
        filter.Set(0, 1);
        filter.Set(2, 3);
        Assert.That(filter.Matches(1, 0, 3));
        Assert.That(filter.Matches(1, 2, 3));
        Assert.That(filter.Matches(1, 0, 2), Is.False);
        Assert.That(filter.Matches(0, 0, 3), Is.False);
    }

    [Test]
    public void ElementPick_HidesRowsWithNoElement()
    {
        var filter = Soldiers();
        filter.Set(1, 2);
        Assert.That(filter.Matches(0, 2, 0));
        Assert.That(filter.Matches(0, ListFilter.All, 0), Is.False);
    }

    [Test]
    public void Cycle_StepsThroughValuesThenBackToAll()
    {
        var filter = new ListFilter(3);
        var seen = Enumerable.Range(0, 5).Select(_ => { filter.Cycle(0); return filter.Pick(0); }).ToArray();
        Assert.That(seen, Is.EqualTo(new[] { 0, 1, 2, ListFilter.All, 0 }));
        filter.Cycle(0, -1);
        Assert.That(filter.Pick(0), Is.EqualTo(ListFilter.All));
        filter.Cycle(0, -1);
        Assert.That(filter.Pick(0), Is.EqualTo(2));
    }

    [Test]
    public void Reset_And_OutOfRangeSet_GoBackToAll()
    {
        var filter = Soldiers();
        filter.Set(0, 2);
        filter.Set(2, 9);
        Assert.That(filter.Pick(2), Is.EqualTo(ListFilter.All));
        Assert.That(filter.IsAll, Is.False);
        filter.Reset();
        Assert.That(filter.IsAll);
    }

    [Test]
    public void Apply_KeepsOrderAndCanBeEmpty()
    {
        var filter = new ListFilter(4);
        int[] rows = { 3, 1, 3, 0 };
        filter.Set(0, 3);
        Assert.That(filter.Apply(rows, r => new[] { r }), Is.EqualTo(new[] { 3, 3 }));
        filter.Set(0, 2);
        Assert.That(filter.Apply(rows, r => new[] { r }), Is.Empty);
    }
}
