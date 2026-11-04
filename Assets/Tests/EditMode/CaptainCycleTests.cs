using NUnit.Framework;

public class CaptainCycleTests
{
    static readonly string[] Party = { "a", "b", null, "d" };

    [Test]
    public void Step_WrapsAndSkipsEmptySlots()
    {
        Assert.That(CaptainCycle.Step(Party, 0, 1), Is.EqualTo(1));
        Assert.That(CaptainCycle.Step(Party, 1, 1), Is.EqualTo(3));
        Assert.That(CaptainCycle.Step(Party, 3, 1), Is.EqualTo(0));
        Assert.That(CaptainCycle.Step(Party, 0, -1), Is.EqualTo(3));
        Assert.That(CaptainCycle.Step(Party, 3, -1), Is.EqualTo(1));
        Assert.That(CaptainCycle.Step(Party, 0, 2), Is.EqualTo(3));
        Assert.That(CaptainCycle.Step(Party, 1, 0), Is.EqualTo(1));
    }

    [Test]
    public void Step_FromNowhere_StartsAtAnEnd()
    {
        Assert.That(CaptainCycle.Step(Party, 2, 0), Is.EqualTo(0));
        Assert.That(CaptainCycle.Step(Party, -1, 0), Is.EqualTo(0));
        Assert.That(CaptainCycle.Step(Party, -1, 1), Is.EqualTo(0));
        Assert.That(CaptainCycle.Step(Party, 9, -1), Is.EqualTo(3));
        Assert.That(CaptainCycle.Step(new string[0], 0, 1), Is.EqualTo(-1));
        Assert.That(CaptainCycle.Step(new string[] { null, null }, 0, 1), Is.EqualTo(-1));
    }

    [Test]
    public void Neighbours_OnlyShowsDistinctCaptains()
    {
        Assert.That(CaptainCycle.Neighbours(Party, 0), Is.EqualTo((3, 1)));
        Assert.That(CaptainCycle.Neighbours(new[] { "a", "b" }, 0), Is.EqualTo((-1, 1)));
        Assert.That(CaptainCycle.Neighbours(new[] { "a" }, 0), Is.EqualTo((-1, -1)));
    }
}
