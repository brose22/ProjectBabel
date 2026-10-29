using NUnit.Framework;

public class TimeScaleRulesTests
{
    [Test]
    public void Resolve_RunsAtGameSpeedWhenNothingHoldsIt()
    {
        Assert.That(TimeScaleRules.Resolve(false, false, false, 1f), Is.EqualTo(1f));
        Assert.That(TimeScaleRules.Resolve(false, false, false, 2f), Is.EqualTo(2f));
    }

    [Test]
    public void Resolve_AnyFreezeHoldsTheMatch()
    {
        Assert.That(TimeScaleRules.Resolve(true, false, false, 2f), Is.EqualTo(0f));
        Assert.That(TimeScaleRules.Resolve(false, true, false, 2f), Is.EqualTo(0f));
        Assert.That(TimeScaleRules.Resolve(false, false, true, 2f), Is.EqualTo(0f));
    }

    [Test]
    public void Resolve_HitStopEndingDuringARequestStaysPaused()
    {
        Assert.That(TimeScaleRules.Resolve(false, true, true, 1f), Is.EqualTo(0f));
        Assert.That(TimeScaleRules.Resolve(false, false, true, 1f), Is.EqualTo(0f));
    }
}
