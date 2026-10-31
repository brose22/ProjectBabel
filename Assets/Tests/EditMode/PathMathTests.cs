using NUnit.Framework;
using UnityEngine;

public class PathMathTests
{
    // An L-shaped path: 10 tiles east, then 10 tiles north.
    static readonly Vector3[] Path = { new(0f, 0f), new(10f, 0f), new(10f, 10f) };

    static void AssertPoint(Vector3 expected, Vector3 actual) =>
        Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), $"expected {expected}, got {actual}");

    [Test]
    public void MoveBack_WithinOneSegment_StaysOnSegment()
    {
        Vector3 result = PathMath.MoveBack(Path, 2, new Vector3(10f, 4f), 3f, out int next);
        AssertPoint(new Vector3(10f, 1f), result);
        Assert.That(next, Is.EqualTo(2));
    }

    [Test]
    public void MoveBack_EndingExactlyOnAWaypoint_KeepsHeadingForTheNextOne()
    {
        Vector3 result = PathMath.MoveBack(Path, 2, new Vector3(10f, 4f), 4f, out int next);
        AssertPoint(new Vector3(10f, 0f), result);
        Assert.That(next, Is.EqualTo(2));
    }

    [Test]
    public void MoveBack_AcrossAWaypoint_RewindsNextPoint()
    {
        Vector3 result = PathMath.MoveBack(Path, 2, new Vector3(10f, 4f), 6f, out int next);
        AssertPoint(new Vector3(8f, 0f), result);
        Assert.That(next, Is.EqualTo(1));
    }

    [Test]
    public void MoveBack_AcrossSeveralWaypoints_RewindsNextPoint()
    {
        Vector3[] path = { new(0f, 0f), new(2f, 0f), new(2f, 2f), new(4f, 2f), new(4f, 6f) };
        // From (4, 3) heading for point 4: back 1 to (4, 2), 2 to (2, 2), 2 to (2, 0), then 0.5 more.
        Vector3 result = PathMath.MoveBack(path, 4, new Vector3(4f, 3f), 5.5f, out int next);
        AssertPoint(new Vector3(1.5f, 0f), result);
        Assert.That(next, Is.EqualTo(1));
    }

    [Test]
    public void MoveBack_StartingOnAWaypoint_ContinuesBackPastIt()
    {
        Vector3 result = PathMath.MoveBack(Path, 2, new Vector3(10f, 0f), 3f, out int next);
        AssertPoint(new Vector3(7f, 0f), result);
        Assert.That(next, Is.EqualTo(1));
    }

    [Test]
    public void MoveBack_PastTheStart_ClampsToFirstPoint()
    {
        Vector3 result = PathMath.MoveBack(Path, 2, new Vector3(10f, 4f), 100f, out int next);
        AssertPoint(Path[0], result);
        Assert.That(next, Is.EqualTo(1));
    }

    [TestCase(0f)]
    [TestCase(-2f)]
    [TestCase(float.NaN)]
    public void MoveBack_NonPositiveOrNaNDistance_DoesNothing(float distance)
    {
        var start = new Vector3(10f, 4f);
        Vector3 result = PathMath.MoveBack(Path, 2, start, distance, out int next);
        AssertPoint(start, result);
        Assert.That(next, Is.EqualTo(2));
    }

    [Test]
    public void MoveBack_NullPath_DoesNothing()
    {
        var start = new Vector3(3f, 3f);
        Vector3 result = PathMath.MoveBack(null, 1, start, 2f, out int next);
        AssertPoint(start, result);
        Assert.That(next, Is.EqualTo(1));
    }

    [Test]
    public void MoveBack_SinglePointPath_DoesNothing()
    {
        var start = new Vector3(3f, 3f);
        Vector3 result = PathMath.MoveBack(new[] { Vector3.zero }, 1, start, 2f, out int next);
        AssertPoint(start, result);
        Assert.That(next, Is.EqualTo(1));
    }

    [TestCase(0.5f)]
    [TestCase(4f)]
    [TestCase(6f)]
    [TestCase(13f)]
    public void MoveBack_ThenWalkingForwardTheSameDistance_ReturnsToTheStart(float distance)
    {
        var start = new Vector3(10f, 4f);
        Vector3 back = PathMath.MoveBack(Path, 2, start, distance, out int next);
        Vector3 forward = WalkForward(Path, ref next, back, distance);
        AssertPoint(start, forward);
        Assert.That(next, Is.EqualTo(2));
    }

    // Mirrors how Enemy walks: head for path[next] and advance next on arrival.
    static Vector3 WalkForward(Vector3[] path, ref int next, Vector3 position, float distance)
    {
        float remaining = distance;
        while (remaining > 1e-6f && next < path.Length)
        {
            float toNext = Vector3.Distance(position, path[next]);
            if (remaining < toNext)
                return Vector3.MoveTowards(position, path[next], remaining);
            position = path[next];
            remaining -= toNext;
            next++;
        }
        return position;
    }

    [Test]
    public void LeftNormal_PointsLeftOfTheTravelDirection()
    {
        Assert.That(PathMath.LeftNormal(new Vector3(0f, 0f), new Vector3(10f, 0f)), Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(PathMath.LeftNormal(new Vector3(10f, 0f), new Vector3(10f, 10f)), Is.EqualTo(new Vector2(-1f, 0f)));
    }

    [Test]
    public void LeftNormal_OfAZeroLengthSegmentIsZero() =>
        Assert.That(PathMath.LeftNormal(Vector3.one, Vector3.one), Is.EqualTo(Vector2.zero));

    [Test]
    public void EaseNormal_MovesTowardTheTargetStaysUnitLengthAndArrives()
    {
        Vector2 current = new(0f, 1f);
        Vector2 target = new(-1f, 0f);
        Vector2 half = PathMath.EaseNormal(current, target, 0.5f);
        Assert.That(half.magnitude, Is.EqualTo(1f).Within(1e-5f));
        Assert.That(half.x, Is.LessThan(0f));
        Assert.That(half.y, Is.GreaterThan(0f));
        Assert.That(PathMath.EaseNormal(current, target, 1f), Is.EqualTo(target));
    }

    [Test]
    public void EaseNormal_WithAZeroTargetKeepsTheCurrentDirection() =>
        Assert.That(PathMath.EaseNormal(new Vector2(0f, 1f), Vector2.zero, 0.5f), Is.EqualTo(new Vector2(0f, 1f)));
}
