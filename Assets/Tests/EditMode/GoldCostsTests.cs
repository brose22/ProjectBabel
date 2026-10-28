using NUnit.Framework;

public class GoldCostsTests
{
    [TestCase(0, 1f)]
    [TestCase(1, 1f)]
    [TestCase(2, 1.1f)]
    [TestCase(3, 1.2f)]
    [TestCase(6, 1.5f)]
    public void LevelFactor_IsLinearAndTreatsLevelsBelowOneAsOne(int level, float expected)
    {
        Assert.That(GoldCosts.LevelFactor(level), Is.EqualTo(expected).Within(1e-5f));
    }

    [TestCase(4, 1, 4)]
    [TestCase(10, 2, 11)]
    [TestCase(10, 3, 12)]
    [TestCase(20, 6, 30)]
    public void Scaled_MultipliesBaseByLevelFactor(int baseCost, int level, int expected)
    {
        Assert.That(GoldCosts.Scaled(baseCost, level), Is.EqualTo(expected));
    }

    // Bases ending in 5 at level 2 land on .5; Mathf.RoundToInt rounds those to the even neighbour.
    [TestCase(5, 2, 6)]
    [TestCase(15, 2, 16)]
    [TestCase(25, 2, 28)]
    public void Scaled_RoundsHalvesToEven(int baseCost, int level, int expected)
    {
        Assert.That(GoldCosts.Scaled(baseCost, level), Is.EqualTo(expected));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    public void Scaled_ZeroBaseStaysFree(int level)
    {
        Assert.That(GoldCosts.Scaled(0, level), Is.EqualTo(0));
    }

    [TestCase(-5, 1)]
    [TestCase(-1, 3)]
    public void Scaled_IsNeverNegative(int baseCost, int level)
    {
        Assert.That(GoldCosts.Scaled(baseCost, level), Is.EqualTo(0));
    }
}
