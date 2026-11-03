using System.Linq;
using NUnit.Framework;

public class CastleShopTests
{
    static readonly int[] Prices = { 60, 100, 160, 240, 400 };

    [Test]
    public void SoldierStock_IsTheSameForTheSameSeed()
    {
        var first = CastleShop.SoldierStock(7, 6);
        var second = CastleShop.SoldierStock(7, 6);
        Assert.That(first.Select(s => s.id), Is.EqualTo(second.Select(s => s.id)));
        Assert.That(first.Select(s => s.rarity), Is.EqualTo(second.Select(s => s.rarity)));
    }

    [Test]
    public void SoldierStock_HasTheRequestedCountOfDistinctSoldiers()
    {
        var stock = CastleShop.SoldierStock(3, 6);
        Assert.That(stock.Count, Is.EqualTo(6));
        Assert.That(stock.Select(s => s.id).Distinct().Count(), Is.EqualTo(6));
        Assert.That(stock.All(s => !s.IsEquipped));
    }

    [Test]
    public void SoldierStock_ChangesWithTheSeed()
    {
        Assert.That(CastleShop.SoldierStock(1, 6)[0].id, Is.Not.EqualTo(CastleShop.SoldierStock(2, 6)[0].id));
    }

    [Test]
    public void SoldierStock_MixesClassesAndRaritiesOverManySeeds()
    {
        var soldiers = Enumerable.Range(0, 50).SelectMany(seed => CastleShop.SoldierStock(seed, 6)).ToList();
        Assert.That(soldiers.Select(s => s.soldierClass).Distinct().Count(), Is.EqualTo(4));
        Assert.That(soldiers.Select(s => s.rarity).Distinct().Count(), Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void SlotSeed_DiffersByShelfAndSlot()
    {
        Assert.That(CastleShop.SlotSeed(5, CastleShop.SoldierShelf, 0), Is.Not.EqualTo(CastleShop.SlotSeed(5, CastleShop.CaptainShelf, 0)));
        Assert.That(CastleShop.SlotSeed(5, CastleShop.SoldierShelf, 0), Is.Not.EqualTo(CastleShop.SlotSeed(5, CastleShop.SoldierShelf, 1)));
        Assert.That(CastleShop.SlotSeed(5, CastleShop.SoldierShelf, 0), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void SoldierPrice_RisesWithRarity()
    {
        Assert.That(CastleShop.SoldierPrice(SoldierRarity.Common, Prices), Is.EqualTo(60));
        Assert.That(CastleShop.SoldierPrice(SoldierRarity.Legendary, Prices), Is.EqualTo(400));
    }

    [Test]
    public void SoldierPrice_UsesTheLastPriceWhenTheListIsShort()
    {
        Assert.That(CastleShop.SoldierPrice(SoldierRarity.Epic, new[] { 50, 90 }), Is.EqualTo(90));
        Assert.That(CastleShop.SoldierPrice(SoldierRarity.Epic, new int[0]), Is.EqualTo(0));
    }

    [Test]
    public void CanBuySoldier_NeedsGoldAndAnUnsoldShelf()
    {
        Assert.That(CastleShop.CanBuySoldier(false, 100, 100), Is.True);
        Assert.That(CastleShop.CanBuySoldier(false, 99, 100), Is.False);
        Assert.That(CastleShop.CanBuySoldier(true, 500, 100), Is.False);
    }

    [Test]
    public void CaptainBlock_RefusesAtAFullRoster()
    {
        Assert.That(CastleShop.CaptainBlock(false, 11, 12, 500, 250), Is.EqualTo(RecruitBlock.None));
        Assert.That(CastleShop.CaptainBlock(false, 12, 12, 500, 250), Is.EqualTo(RecruitBlock.RosterFull));
    }

    [Test]
    public void CaptainBlock_GivesAFullRosterBeforeGold_AndSoldOutFirst()
    {
        Assert.That(CastleShop.CaptainBlock(false, 12, 12, 0, 250), Is.EqualTo(RecruitBlock.RosterFull));
        Assert.That(CastleShop.CaptainBlock(false, 3, 12, 249, 250), Is.EqualTo(RecruitBlock.NotEnoughGold));
        Assert.That(CastleShop.CaptainBlock(true, 12, 12, 0, 250), Is.EqualTo(RecruitBlock.SoldOut));
    }
}
