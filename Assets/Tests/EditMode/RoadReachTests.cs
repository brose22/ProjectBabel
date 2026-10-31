using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RoadReachTests
{
    static List<Vector2> Road()
    {
        var road = new List<Vector2>();
        for (int x = 0; x < 10; x++) road.Add(new Vector2(x, 5));
        return road;
    }

    [Test]
    public void CountInRange_CountsOnlyRoadWithinRange()
    {
        Assert.That(RoadReach.CountInRange(new Vector2(5, 6), Road(), 1.5f), Is.EqualTo(3));
        Assert.That(RoadReach.CountInRange(new Vector2(5, 12), Road(), 1.5f), Is.EqualTo(0));
    }

    [Test]
    public void CountInRange_SpotAtTheRoadEndCoversLessThanOneBesideTheMiddle()
    {
        int edge = RoadReach.CountInRange(new Vector2(0, 7), Road(), 3f);
        int middle = RoadReach.CountInRange(new Vector2(5, 7), Road(), 3f);
        Assert.That(edge, Is.LessThan(middle));
    }

    [TestCase(0, 0, false)]
    [TestCase(1, 4, false)]
    [TestCase(4, 4, true)]
    [TestCase(6, 4, true)]
    public void KeepsCoverage_NeedsRoadAndAtLeastTheCurrentCount(int candidate, int current, bool expected) =>
        Assert.That(RoadReach.KeepsCoverage(candidate, current), Is.EqualTo(expected));
}
