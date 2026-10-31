using System.Collections.Generic;
using NUnit.Framework;

public class RouteMathTests
{
    static IReadOnlyList<(int x, int y)> Line(params (int x, int y)[] cells) => cells;

    [TestCase(0, 3, 7, 0)]
    [TestCase(2, 3, 0, 2)]
    [TestCase(5, 3, 0, 2)]
    [TestCase(-1, 3, 0, 0)]
    [TestCase(-1, 3, 4, 1)]
    [TestCase(-1, 1, 9, 0)]
    [TestCase(1, 0, 3, 0)]
    public void Pick_UsesTheRequestedRouteOrCyclesForAny(int requested, int count, int sequence, int expected) =>
        Assert.That(RouteMath.Pick(requested, count, sequence), Is.EqualTo(expected));

    [Test]
    public void OrderedRoadCells_WithOneRouteKeepsItsOrder()
    {
        var route = Line((0, 0), (1, 0), (2, 0), (2, 1));
        Assert.That(RouteMath.OrderedRoadCells(new[] { route }), Is.EqualTo(route));
    }

    [Test]
    public void OrderedRoadCells_ListsSharedCellsOnceAndTheCastleLast()
    {
        // Two routes that merge at (5, 5) and share the walk to the castle at (7, 5).
        var a = Line((0, 0), (1, 0), (5, 5), (6, 5), (7, 5));
        var b = Line((0, 9), (1, 9), (2, 9), (5, 5), (6, 5), (7, 5));
        List<(int x, int y)> cells = RouteMath.OrderedRoadCells(new[] { a, b });
        Assert.That(cells, Is.Unique);
        Assert.That(cells.Count, Is.EqualTo(8));
        Assert.That(cells[^1], Is.EqualTo((7, 5)));
        Assert.That(cells.IndexOf((0, 9)), Is.LessThan(cells.IndexOf((5, 5))));
        Assert.That(cells.IndexOf((0, 0)), Is.LessThan(cells.IndexOf((5, 5))));
    }

    [Test]
    public void OrderedRoadCells_LongerRouteStartsFurtherFromTheCastle()
    {
        var a = Line((0, 0), (1, 0), (9, 9));
        var b = Line((0, 5), (1, 5), (2, 5), (3, 5), (9, 9));
        List<(int x, int y)> cells = RouteMath.OrderedRoadCells(new[] { a, b });
        Assert.That(cells[0], Is.EqualTo((0, 5)));
    }

    [Test]
    public void EndAtTheSameCell_DetectsRoutesThatMissTheCastle()
    {
        var a = Line((0, 0), (5, 5));
        var b = Line((0, 9), (5, 5));
        var c = Line((0, 9), (6, 5));
        Assert.That(RouteMath.EndAtTheSameCell(new[] { a, b }), Is.True);
        Assert.That(RouteMath.EndAtTheSameCell(new[] { a, c }), Is.False);
    }
}
