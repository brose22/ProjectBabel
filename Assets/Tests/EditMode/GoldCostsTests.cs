using NUnit.Framework;

public class GoldCostsTests
{
    [TestCase(0, 1f)]
    [TestCase(1, 1f)]
    [TestCase(2, 1.5f)]
    [TestCase(3, 2f)]
    public void LevelFactor_IsLinearAndTreatsLevelsBelowOneAsOne(int level, float expected)
    {
        Assert.That(GoldCosts.LevelFactor(level), Is.EqualTo(expected).Within(1e-5f));
    }

    [TestCase(4, 1, 4)]
    [TestCase(4, 2, 6)]
    [TestCase(4, 3, 8)]
    [TestCase(12, 3, 24)]
    public void Scaled_MultipliesBaseByLevelFactor(int baseCost, int level, int expected)
    {
        Assert.That(GoldCosts.Scaled(baseCost, level), Is.EqualTo(expected));
    }

    // Odd bases at level 2 land on .5; Mathf.RoundToInt rounds those to the even neighbour.
    [TestCase(1, 2, 2)]
    [TestCase(3, 2, 4)]
    [TestCase(5, 2, 8)]
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
