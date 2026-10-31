using NUnit.Framework;

public class LevelMathTests
{
    [TestCase(1, 0)]
    [TestCase(2, 50)]
    [TestCase(3, 150)]
    [TestCase(10, 2250)]
    public void XpForLevel_MatchesTheCurve(int level, int xp) =>
        Assert.That(LevelMath.XpForLevel(level), Is.EqualTo(xp));

    [TestCase(0, 1)]
    [TestCase(49, 1)]
    [TestCase(50, 2)]
    [TestCase(149, 2)]
    [TestCase(150, 3)]
    public void LevelForXp_ReturnsTheLevelReached(int xp, int level) =>
        Assert.That(LevelMath.LevelForXp(xp), Is.EqualTo(level));

    [Test]
    public void LevelForXp_HugeXp_CapsAtMaxLevel() =>
        Assert.That(LevelMath.LevelForXp(1000000), Is.EqualTo(LevelMath.MaxLevel));

    [Test]
    public void StatFactor_LevelOne_IsUnchanged() =>
        Assert.That(LevelMath.StatFactor(1), Is.EqualTo(1f).Within(1e-5f));

    [Test]
    public void StatFactor_LevelThree_AddsTwentyPercent() =>
        Assert.That(LevelMath.StatFactor(3), Is.EqualTo(1.2f).Within(1e-5f));

    [TestCase(0)]
    [TestCase(-3)]
    public void StatFactor_BelowLevelOne_TreatedAsLevelOne(int level) =>
        Assert.That(LevelMath.StatFactor(level), Is.EqualTo(1f).Within(1e-5f));

    [TestCase(1, 100)]
    [TestCase(2, 110)]
    [TestCase(10, 190)]
    public void StatPercent_IsTheFactorAsAWholePercentage(int level, int expected) =>
        Assert.That(LevelMath.StatPercent(level), Is.EqualTo(expected));
}
