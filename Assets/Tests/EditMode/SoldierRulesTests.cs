using System.Collections.Generic;
using NUnit.Framework;

public class SoldierRulesTests
{
    [TestCase(SoldierRarity.Common, 1f)]
    [TestCase(SoldierRarity.Uncommon, 1.25f)]
    [TestCase(SoldierRarity.Rare, 1.5f)]
    [TestCase(SoldierRarity.Epic, 1.75f)]
    [TestCase(SoldierRarity.Legendary, 2f)]
    public void RarityMultiplier_AddsAQuarterPerTier(SoldierRarity rarity, float expected) =>
        Assert.That(SoldierRules.RarityMultiplier(rarity), Is.EqualTo(expected).Within(1e-5f));

    [TestCase(SoldierClass.Knight, CaptainStat.Defense)]
    [TestCase(SoldierClass.Archer, CaptainStat.Range)]
    [TestCase(SoldierClass.Mage, CaptainStat.Power)]
    [TestCase(SoldierClass.Rogue, CaptainStat.Speed)]
    public void StatOf_FollowsTheClass(SoldierClass soldierClass, CaptainStat expected) =>
        Assert.That(SoldierRules.StatOf(soldierClass), Is.EqualTo(expected));

    [Test]
    public void Bonus_IsTheBaseTimesTheRarity() =>
        Assert.That(SoldierRules.Bonus(0.5f, SoldierRarity.Epic), Is.EqualTo(0.875f).Within(1e-5f));

    [Test]
    public void StatTotal_AddsOnlySoldiersOfThatStat()
    {
        var soldiers = new List<SoldierItem>
        {
            new() { soldierClass = SoldierClass.Knight, rarity = SoldierRarity.Common },
            new() { soldierClass = SoldierClass.Knight, rarity = SoldierRarity.Legendary },
            new() { soldierClass = SoldierClass.Mage, rarity = SoldierRarity.Rare },
        };
        float Base(SoldierClass c) => c == SoldierClass.Knight ? 0.5f : 1f;
        Assert.That(SoldierRules.StatTotal(soldiers, CaptainStat.Defense, Base), Is.EqualTo(1.5f).Within(1e-5f));
        Assert.That(SoldierRules.StatTotal(soldiers, CaptainStat.Power, Base), Is.EqualTo(1.5f).Within(1e-5f));
        Assert.That(SoldierRules.StatTotal(soldiers, CaptainStat.Speed, Base), Is.EqualTo(0f));
    }

    [Test]
    public void Roll_IsTheSameForTheSameSeed()
    {
        SoldierItem a = SoldierRules.Roll(42), b = SoldierRules.Roll(42);
        Assert.That(a.id, Is.EqualTo(b.id));
        Assert.That(a.soldierClass, Is.EqualTo(b.soldierClass));
        Assert.That(a.rarity, Is.EqualTo(b.rarity));
        Assert.That(a.IsEquipped, Is.False);
    }

    [Test]
    public void Roll_GivesEveryClassAndRarity()
    {
        var classes = new HashSet<SoldierClass>();
        var rarities = new HashSet<SoldierRarity>();
        for (int seed = 0; seed < 500; seed++)
        {
            SoldierItem item = SoldierRules.Roll(seed);
            classes.Add(item.soldierClass);
            rarities.Add(item.rarity);
        }
        Assert.That(classes.Count, Is.EqualTo(4));
        Assert.That(rarities.Count, Is.EqualTo(5));
    }

    static SoldierInventory Inventory(params string[] ids)
    {
        var inventory = new SoldierInventory();
        foreach (string id in ids) inventory.Add(new SoldierItem { id = id });
        return inventory;
    }

    [Test]
    public void Equip_PutsTheSoldierInTheSlot()
    {
        SoldierInventory inventory = Inventory("s1");
        Assert.That(inventory.Equip("s1", "c1", 2), Is.True);
        Assert.That(inventory.InSlot("c1", 2).id, Is.EqualTo("s1"));
        Assert.That(inventory.Unequipped, Is.Empty);
    }

    [Test]
    public void Equip_MovesTheSoldierFromItsOldCaptain()
    {
        SoldierInventory inventory = Inventory("s1");
        inventory.Equip("s1", "c1", 0);
        inventory.Equip("s1", "c2", 1);
        Assert.That(inventory.EquippedTo("c1"), Is.Empty);
        Assert.That(inventory.InSlot("c2", 1).id, Is.EqualTo("s1"));
    }

    [Test]
    public void Equip_SendsTheSlotsSoldierBackToTheInventory()
    {
        SoldierInventory inventory = Inventory("s1", "s2");
        inventory.Equip("s1", "c1", 0);
        inventory.Equip("s2", "c1", 0);
        Assert.That(inventory.InSlot("c1", 0).id, Is.EqualTo("s2"));
        Assert.That(inventory.Find("s1").IsEquipped, Is.False);
    }

    [TestCase(-1)]
    [TestCase(SoldierInventory.SlotsPerCaptain)]
    public void Equip_RefusesASlotPastTheFour(int slot)
    {
        SoldierInventory inventory = Inventory("s1");
        Assert.That(inventory.Equip("s1", "c1", slot), Is.False);
        Assert.That(inventory.Find("s1").IsEquipped, Is.False);
    }

    [Test]
    public void Equip_RefusesAnUnknownSoldier() =>
        Assert.That(Inventory().Equip("missing", "c1", 0), Is.False);

    [Test]
    public void Unequip_ReturnsTheSoldierToTheInventory()
    {
        SoldierInventory inventory = Inventory("s1");
        inventory.Equip("s1", "c1", 0);
        Assert.That(inventory.Unequip("s1"), Is.True);
        Assert.That(inventory.Unequipped.Count, Is.EqualTo(1));
        Assert.That(inventory.InSlot("c1", 0), Is.Null);
        Assert.That(inventory.Unequip("s1"), Is.False);
    }

    [Test]
    public void EquippedTo_IsInSlotOrder()
    {
        SoldierInventory inventory = Inventory("a", "b", "c");
        inventory.Equip("a", "c1", 3);
        inventory.Equip("b", "c1", 0);
        inventory.Equip("c", "c2", 1);
        Assert.That(inventory.EquippedTo("c1").ConvertAll(i => i.id), Is.EqualTo(new[] { "b", "a" }));
    }

    [Test]
    public void UnequipAll_EmptiesTheCaptain()
    {
        SoldierInventory inventory = Inventory("a", "b");
        inventory.Equip("a", "c1", 0);
        inventory.Equip("b", "c1", 1);
        inventory.UnequipAll("c1");
        Assert.That(inventory.EquippedTo("c1"), Is.Empty);
        Assert.That(inventory.Unequipped.Count, Is.EqualTo(2));
    }

    [Test]
    public void FirstEmptySlot_SkipsTakenSlotsAndStopsAtTheOpenOnes()
    {
        SoldierInventory inventory = Inventory("a");
        inventory.Equip("a", "c1", 0);
        Assert.That(inventory.FirstEmptySlot("c1", 2), Is.EqualTo(1));
        Assert.That(inventory.FirstEmptySlot("c1", 1), Is.EqualTo(-1));
    }

    [Test]
    public void Add_IgnoresADuplicateId()
    {
        SoldierInventory inventory = Inventory("a");
        inventory.Add(new SoldierItem { id = "a" });
        Assert.That(inventory.Items.Count, Is.EqualTo(1));
    }

    [Test]
    public void RollOffer_GivesCountDistinctSoldiers_AndTheSameForTheSameSeed()
    {
        List<SoldierItem> a = SoldierRules.RollOffer(42, 3, 1), b = SoldierRules.RollOffer(42, 3, 1);
        Assert.That(a.Count, Is.EqualTo(3));
        Assert.That(new HashSet<string>(a.ConvertAll(s => s.id)).Count, Is.EqualTo(3));
        Assert.That(a.ConvertAll(s => s.id), Is.EqualTo(b.ConvertAll(s => s.id)));
    }

    [Test]
    public void Roll_OneRarityRollMatchesTheCastleRoll()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            SoldierItem castle = SoldierRules.Roll(seed), one = SoldierRules.Roll(seed, 1);
            Assert.That((one.id, one.soldierClass, one.rarity), Is.EqualTo((castle.id, castle.soldierClass, castle.rarity)));
        }
    }

    [Test]
    public void Roll_MoreRarityRolls_NeverWorseAndRareOnAverage()
    {
        int better = 0;
        for (int seed = 0; seed < 2000; seed++)
        {
            SoldierRarity once = SoldierRules.Roll(seed, 1).rarity, twice = SoldierRules.Roll(seed, 2).rarity;
            Assert.That(twice, Is.GreaterThanOrEqualTo(once));
            if (twice > once) better++;
        }
        Assert.That(better, Is.GreaterThan(500));
    }
}
