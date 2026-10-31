using System.Linq;
using NUnit.Framework;

public class ShopStockTests
{
    [Test]
    public void Pick_ReturnsTheRequestedNumberOfDistinctIndices()
    {
        var pick = ShopStock.Pick(10, 6, 42);
        Assert.That(pick.Count, Is.EqualTo(6));
        Assert.That(pick.Distinct().Count(), Is.EqualTo(6));
        Assert.That(pick.All(i => i >= 0 && i < 10));
    }

    [Test]
    public void Pick_IsTheSameForTheSameSeed()
    {
        Assert.That(ShopStock.Pick(10, 6, 7), Is.EqualTo(ShopStock.Pick(10, 6, 7)));
    }

    [Test]
    public void Pick_VariesWithTheSeed()
    {
        Assert.That(Enumerable.Range(0, 20).Select(s => string.Join(",", ShopStock.Pick(10, 6, s))).Distinct().Count(), Is.GreaterThan(1));
    }

    [Test]
    public void Pick_TakesEverythingWhenThereIsNotMuchToChooseFrom()
    {
        Assert.That(ShopStock.Pick(4, 6, 1), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        Assert.That(ShopStock.Pick(0, 6, 1), Is.Empty);
    }
}
