using NUnit.Framework;

public class RunHealingTests
{
    [Test]
    public void Heal_NeverPassesMax()
    {
        Assert.That(RunHealing.Heal(10, 20, 4), Is.EqualTo(14));
        Assert.That(RunHealing.Heal(19, 20, 4), Is.EqualTo(20));
        Assert.That(RunHealing.Heal(10, 20, -3), Is.EqualTo(10));
    }

    [Test]
    public void RestAmount_IsAShareOfMaxRoundedUp()
    {
        Assert.That(RunHealing.RestAmount(20, 0.3f), Is.EqualTo(6));
        Assert.That(RunHealing.RestAmount(25, 0.3f), Is.EqualTo(8));
        Assert.That(RunHealing.RestAmount(2, 0.1f), Is.EqualTo(1));
        Assert.That(RunHealing.RestAmount(20, 0f), Is.EqualTo(0));
    }
}
