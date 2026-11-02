using NUnit.Framework;
using UnityEngine;

public class PadMathTests
{
    [Test]
    public void ToStep_IgnoresTheDeadZone()
    {
        Assert.That(PadMath.ToStep(new Vector2(0.3f, 0.3f)), Is.EqualTo(Vector2Int.zero));
    }

    [Test]
    public void ToStep_FollowsTheStrongerAxis()
    {
        Assert.That(PadMath.ToStep(new Vector2(0.8f, 0.3f)), Is.EqualTo(Vector2Int.right));
        Assert.That(PadMath.ToStep(new Vector2(0.3f, -0.8f)), Is.EqualTo(Vector2Int.down));
    }

    [Test]
    public void StepCost_RejectsWidgetsBehindOrLevel()
    {
        Assert.That(PadMath.StepCost(Vector2.zero, new Vector2(-50f, 0f), Vector2Int.right), Is.EqualTo(float.PositiveInfinity));
        Assert.That(PadMath.StepCost(Vector2.zero, new Vector2(0f, 50f), Vector2Int.right), Is.EqualTo(float.PositiveInfinity));
    }

    [Test]
    public void StepCost_PrefersAWidgetInLineOverACloserOneToTheSide()
    {
        float inLine = PadMath.StepCost(Vector2.zero, new Vector2(100f, 0f), Vector2Int.right);
        float aside = PadMath.StepCost(Vector2.zero, new Vector2(40f, 60f), Vector2Int.right);
        Assert.That(inLine, Is.LessThan(aside));
    }

    [Test]
    public void StepRepeat_StepsOnceThenWaitsThenRepeats()
    {
        var repeat = new StepRepeat(0.3f, 0.1f);
        Assert.That(repeat.Update(Vector2Int.up, 0f), Is.EqualTo(Vector2Int.up));
        Assert.That(repeat.Update(Vector2Int.up, 0.2f), Is.EqualTo(Vector2Int.zero));
        Assert.That(repeat.Update(Vector2Int.up, 0.3f), Is.EqualTo(Vector2Int.up));
        Assert.That(repeat.Update(Vector2Int.up, 0.35f), Is.EqualTo(Vector2Int.zero));
        Assert.That(repeat.Update(Vector2Int.up, 0.4f), Is.EqualTo(Vector2Int.up));
    }

    [Test]
    public void StepRepeat_AChangedOrReleasedDirectionStepsAtOnce()
    {
        var repeat = new StepRepeat(0.3f, 0.1f);
        repeat.Update(Vector2Int.up, 0f);
        Assert.That(repeat.Update(Vector2Int.left, 0.01f), Is.EqualTo(Vector2Int.left));
        repeat.Update(Vector2Int.zero, 0.02f);
        Assert.That(repeat.Update(Vector2Int.left, 0.03f), Is.EqualTo(Vector2Int.left));
    }
}
