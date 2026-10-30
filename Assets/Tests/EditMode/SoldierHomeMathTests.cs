using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SoldierHomeMathTests
{
    static readonly Vector2Int Tower = new(5, 5);
    // A road running along the row below the tower.
    static bool RoadBelow(Vector2Int cell) => cell.y == 4;
    static bool Anywhere(Vector2Int cell) => true;
    static bool Nothing(Vector2Int cell) => false;

    [Test]
    public void Pick_RoadLoverTakesTheRoad()
    {
        Vector2Int cell = SoldierHomeMath.Pick(Tower, HomeTile.Road, RoadBelow, Anywhere, Nothing);
        Assert.That(cell, Is.EqualTo(new Vector2Int(5, 4)));
    }

    [Test]
    public void Pick_RoadLoverReachesADiagonalRoad()
    {
        Vector2Int cell = SoldierHomeMath.Pick(Tower, HomeTile.Road, c => c == new Vector2Int(6, 6), Anywhere, Nothing);
        Assert.That(cell, Is.EqualTo(new Vector2Int(6, 6)));
    }

    [Test]
    public void Pick_OffRoadSoldierAvoidsTheRoad()
    {
        Vector2Int cell = SoldierHomeMath.Pick(Tower, HomeTile.OffRoad, c => c == new Vector2Int(5, 6), Anywhere, Nothing);
        Assert.That(cell, Is.EqualTo(new Vector2Int(6, 5)));
    }

    [Test]
    public void Pick_SkipsATakenRoadTileForTheNextOne()
    {
        var taken = new HashSet<Vector2Int> { new(5, 4) };
        Vector2Int cell = SoldierHomeMath.Pick(Tower, HomeTile.Road, RoadBelow, Anywhere, taken.Contains);
        Assert.That(cell, Is.EqualTo(new Vector2Int(6, 4)));
    }

    [Test]
    public void Pick_SettlesForAnotherTileWhenNoLikedTileIsFree()
    {
        Vector2Int cell = SoldierHomeMath.Pick(Tower, HomeTile.Road, Nothing, Anywhere, Nothing);
        Assert.That(cell, Is.EqualTo(new Vector2Int(5, 6)));
    }

    [Test]
    public void Pick_NeverStandsOnABlockedTile()
    {
        Vector2Int cell = SoldierHomeMath.Pick(Tower, HomeTile.Road, RoadBelow, c => !RoadBelow(c), Nothing);
        Assert.That(RoadBelow(cell), Is.False);
    }

    [Test]
    public void Pick_SharesATileWhenEveryTileIsTaken()
    {
        Vector2Int cell = SoldierHomeMath.Pick(Tower, HomeTile.Any, Nothing, c => c == new Vector2Int(4, 6), Anywhere);
        Assert.That(cell, Is.EqualTo(new Vector2Int(4, 6)));
    }
}
