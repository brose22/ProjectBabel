using NUnit.Framework;

public class BossRulesTests
{
    [Test]
    public void IsWaveOver_IgnoresTheBoss()
    {
        Assert.That(BossRules.IsWaveOver(false, 1, bossAlive: true), Is.True);
        Assert.That(BossRules.IsWaveOver(false, 2, bossAlive: true), Is.False);
        Assert.That(BossRules.IsWaveOver(true, 1, bossAlive: true), Is.False);
        Assert.That(BossRules.IsWaveOver(false, 0, bossAlive: false), Is.True);
        Assert.That(BossRules.IsWaveOver(false, 1, bossAlive: false), Is.False);
    }

    [Test]
    public void IsMatchWon_NeedsEveryWaveAndADeadBoss()
    {
        Assert.That(BossRules.IsMatchWon(20, 20, bossAlive: false), Is.True);
        Assert.That(BossRules.IsMatchWon(20, 20, bossAlive: true), Is.False);
        Assert.That(BossRules.IsMatchWon(19, 20, bossAlive: false), Is.False);
    }
}
