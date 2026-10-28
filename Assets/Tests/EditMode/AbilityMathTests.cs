using NUnit.Framework;
using UnityEngine;

public class AbilityMathTests
{
    // A segment 10 tiles long, running east from the origin.
    static readonly Vector2 A = new(0f, 0f);
    static readonly Vector2 B = new(10f, 0f);

    static void AssertPoint(Vector2 expected, Vector2 actual) =>
        Assert.That(Vector2.Distance(expected, actual), Is.LessThan(1e-4f), $"expected {expected}, got {actual}");

    [Test]
    public void DistanceToSegment_PointOnSegment_IsZero()
    {
        Assert.That(AbilityMath.DistanceToSegment(new Vector2(4f, 0f), A, B), Is.EqualTo(0f).Within(1e-4f));
    }

    [Test]
    public void DistanceToSegment_BesideTheMiddle_IsPerpendicularDistance()
    {
        Assert.That(AbilityMath.DistanceToSegment(new Vector2(5f, 3f), A, B), Is.EqualTo(3f).Within(1e-4f));
    }

    [Test]
    public void DistanceToSegment_BeyondStart_IsDistanceToStart()
    {
        Assert.That(AbilityMath.DistanceToSegment(new Vector2(-3f, 4f), A, B), Is.EqualTo(5f).Within(1e-4f));
    }

    [Test]
    public void DistanceToSegment_BeyondEnd_IsDistanceToEnd()
    {
        Assert.That(AbilityMath.DistanceToSegment(new Vector2(13f, 4f), A, B), Is.EqualTo(5f).Within(1e-4f));
    }

    [Test]
    public void DistanceToSegment_DegenerateSegment_IsDistanceToThePoint()
    {
        var a = new Vector2(2f, 2f);
        Assert.That(AbilityMath.DistanceToSegment(new Vector2(5f, 6f), a, a), Is.EqualTo(5f).Within(1e-4f));
    }

    [Test]
    public void LineEnd_IsOriginPlusDirectionTimesLength()
    {
        // Direction (3, 4) normalizes to (0.6, 0.8), so 10 tiles along it is (6, 8) from the origin.
        AssertPoint(new Vector2(7f, 9f), AbilityMath.LineEnd(new Vector2(1f, 1f), new Vector2(4f, 5f), 10f));
    }

    [Test]
    public void LineEnd_TargetOnOrigin_ReturnsOrigin()
    {
        var origin = new Vector2(2f, 3f);
        AssertPoint(origin, AbilityMath.LineEnd(origin, origin, 5f));
    }

    [Test]
    public void LineEnd_LengthShorterThanTargetDistance_StopsShortOfTarget()
    {
        Vector2 end = AbilityMath.LineEnd(A, B, 4f);
        AssertPoint(new Vector2(4f, 0f), end);
        Assert.That(Vector2.Distance(A, end), Is.LessThan(Vector2.Distance(A, B)));
    }
}
