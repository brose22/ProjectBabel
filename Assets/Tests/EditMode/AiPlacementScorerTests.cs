using System.Collections.Generic;
using NUnit.Framework;

public class AiPlacementScorerTests
{
    // A straight road along y = 5 from x = 0 to 9.
    static List<(int x, int y)> Road()
    {
        var road = new List<(int x, int y)>();
        for (int x = 0; x < 10; x++) road.Add((x, 5));
        return road;
    }

    [Test]
    public void Coverage_CountsRoadCellsInRange()
    {
        var road = Road();
        float near = AiPlacementScorer.Coverage(5, 6, 2f, road);
        float far = AiPlacementScorer.Coverage(5, 10, 2f, road);
        Assert.That(near, Is.GreaterThan(2f));
        Assert.That(far, Is.EqualTo(0f));
    }

    [Test]
    public void Coverage_ValuesCellsNearTheCastleMore()
    {
        var road = Road();
        Assert.That(AiPlacementScorer.Coverage(8, 6, 1.5f, road), Is.GreaterThan(AiPlacementScorer.Coverage(1, 6, 1.5f, road)));
    }

    [Test]
    public void PickCell_PrefersTheMiddleOfTheRoad()
    {
        var road = Road();
        var candidates = new List<(int x, int y)> { (0, 8), (5, 7), (9, 9) };
        Assert.That(AiPlacementScorer.PickCell(candidates, 3f, road, false, null, out _), Is.EqualTo(1));
    }

    [Test]
    public void PickCell_SpreadsAwayFromWhatIsAlreadyCovered()
    {
        var road = Road();
        var covered = new bool[road.Count];
        AiPlacementScorer.MarkCovered(2, 6, 2.5f, road, covered);
        var candidates = new List<(int x, int y)> { (2, 7), (7, 7) };
        Assert.That(AiPlacementScorer.PickCell(candidates, 2.5f, road, false, covered, out _), Is.EqualTo(1));
    }

    [Test]
    public void Support_AvoidsCellsBesideTheRoad()
    {
        var road = Road();
        var candidates = new List<(int x, int y)> { (5, 6), (5, 9) };
        Assert.That(AiPlacementScorer.PickCell(candidates, 4f, road, true, null, out _), Is.EqualTo(1));
    }

    [Test]
    public void Support_StillStaysWithinRangeOfTheRoad()
    {
        var road = Road();
        var candidates = new List<(int x, int y)> { (5, 12), (5, 7) };
        Assert.That(AiPlacementScorer.PickCell(candidates, 3f, road, true, null, out _), Is.EqualTo(1));
    }

    [Test]
    public void PickCell_NothingInRange_TakesTheNearestToTheRoad()
    {
        var road = Road();
        var candidates = new List<(int x, int y)> { (5, 14), (5, 9) };
        Assert.That(AiPlacementScorer.PickCell(candidates, 2f, road, false, null, out _), Is.EqualTo(1));
    }

    [Test]
    public void PickCell_NoCandidates_ReturnsMinusOne()
    {
        Assert.That(AiPlacementScorer.PickCell(new List<(int x, int y)>(), 3f, Road(), false, null, out _), Is.EqualTo(-1));
    }

    [Test]
    public void Move_OnlyForPoorSpotsWithGoldToSpare()
    {
        Assert.That(AiPlacementScorer.ShouldMove(2f, 20f, 200, 25), Is.True);
        Assert.That(AiPlacementScorer.ShouldMove(15f, 20f, 200, 25), Is.False);
        Assert.That(AiPlacementScorer.ShouldMove(2f, 20f, 40, 25), Is.False);
    }

    [Test]
    public void Restore_KeepsACushion()
    {
        Assert.That(AiPlacementScorer.ShouldRestore(15, 100, 3), Is.True);
        Assert.That(AiPlacementScorer.ShouldRestore(15, 30, 3), Is.False);
    }

    [Test]
    public void DrawRate_FollowsGoldPerTower()
    {
        Assert.That(AiPlacementScorer.DrawRateFor(600, 3), Is.EqualTo(2));
        Assert.That(AiPlacementScorer.DrawRateFor(150, 3), Is.EqualTo(1));
        Assert.That(AiPlacementScorer.DrawRateFor(30, 3), Is.EqualTo(0));
    }
}
