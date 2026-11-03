using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class StartingCaptainsTests
{
    static readonly CaptainRole[] AllRoles =
        { CaptainRole.Knight, CaptainRole.Mage, CaptainRole.Rogue, CaptainRole.Archer, CaptainRole.Healer };

    [Test]
    public void Draw_OffersOneKnightMageArcherAndHealer()
    {
        var offer = StartingCaptains.Draw(AllRoles, 42);
        Assert.That(offer.Select(o => AllRoles[o.template]),
            Is.EqualTo(new[] { CaptainRole.Knight, CaptainRole.Mage, CaptainRole.Archer, CaptainRole.Healer }));
    }

    [Test]
    public void Draw_SameSeedGivesTheSameOffer() =>
        Assert.That(StartingCaptains.Draw(AllRoles, 7), Is.EqualTo(StartingCaptains.Draw(AllRoles, 7)));

    [Test]
    public void Draw_RollSeedsDiffer() =>
        Assert.That(StartingCaptains.Draw(AllRoles, 7).Select(o => o.seed), Is.Unique);

    [Test]
    public void Draw_PicksAmongTemplatesOfTheSameRole()
    {
        var roles = new[] { CaptainRole.Knight, CaptainRole.Knight, CaptainRole.Mage, CaptainRole.Archer, CaptainRole.Healer };
        var knights = new HashSet<int>();
        for (int seed = 0; seed < 50; seed++) knights.Add(StartingCaptains.Draw(roles, seed)[0].template);
        Assert.That(knights, Is.EquivalentTo(new[] { 0, 1 }));
    }

    [Test]
    public void Draw_LeavesOutARoleWithNoTemplate()
    {
        var offer = StartingCaptains.Draw(new[] { CaptainRole.Rogue, CaptainRole.Mage, CaptainRole.Healer }, 3);
        Assert.That(offer.Select(o => o.template), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void Draw_NoTemplatesGivesNothing() =>
        Assert.That(StartingCaptains.Draw(new CaptainRole[0], 1), Is.Empty);

    [TestCase(4, 3)]
    [TestCase(2, 2)]
    [TestCase(0, 0)]
    public void PickCount_IsThreeOrTheWholeOffer(int offer, int expected) =>
        Assert.That(StartingCaptains.PickCount(offer), Is.EqualTo(expected));

    [Test]
    public void Toggle_PicksUpToThree()
    {
        var picked = new List<int>();
        Assert.That(StartingCaptains.Toggle(picked, 0, 4), Is.True);
        Assert.That(StartingCaptains.Toggle(picked, 2, 4), Is.True);
        Assert.That(StartingCaptains.IsComplete(picked, 4), Is.False);
        Assert.That(StartingCaptains.Toggle(picked, 3, 4), Is.True);
        Assert.That(StartingCaptains.IsComplete(picked, 4), Is.True);
        Assert.That(StartingCaptains.Toggle(picked, 1, 4), Is.False);
        Assert.That(picked, Is.EqualTo(new[] { 0, 2, 3 }));
    }

    [Test]
    public void Toggle_UnpicksAPickedCaptain()
    {
        var picked = new List<int> { 0, 1, 2 };
        Assert.That(StartingCaptains.Toggle(picked, 1, 4), Is.True);
        Assert.That(picked, Is.EqualTo(new[] { 0, 2 }));
        Assert.That(StartingCaptains.Toggle(picked, 3, 4), Is.True);
        Assert.That(StartingCaptains.IsComplete(picked, 4), Is.True);
    }

    [Test]
    public void Toggle_IgnoresIndicesOutsideTheOffer()
    {
        var picked = new List<int>();
        Assert.That(StartingCaptains.Toggle(picked, 4, 4), Is.False);
        Assert.That(StartingCaptains.Toggle(picked, -1, 4), Is.False);
        Assert.That(picked, Is.Empty);
    }

    [Test]
    public void IsComplete_NeedsAnOffer() =>
        Assert.That(StartingCaptains.IsComplete(new List<int>(), 0), Is.False);

    [Test]
    public void GiveSoldiers_ThreeCaptainsGetSixEquippedSoldiers()
    {
        var inventory = new SoldierInventory();
        var ids = new[] { "a", "b", "c" };
        var given = StartingCaptains.GiveSoldiers(inventory, ids, 11);
        Assert.That(given.Count, Is.EqualTo(6));
        Assert.That(inventory.Items.Count, Is.EqualTo(6));
        Assert.That(inventory.Items, Has.All.Matches<SoldierItem>(s => s.IsEquipped));
        foreach (string id in ids)
            Assert.That(inventory.EquippedTo(id).Select(s => s.slot), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(inventory.Items.Select(s => s.id), Is.Unique);
    }

    [Test]
    public void GiveSoldiers_FillsEmptySlotsAroundEquippedOnes()
    {
        var inventory = new SoldierInventory();
        SoldierItem own = SoldierRules.Roll(5);
        inventory.Add(own);
        inventory.Equip(own.id, "a", 0);
        StartingCaptains.GiveSoldiers(inventory, new[] { "a" }, 3);
        Assert.That(inventory.EquippedTo("a").Select(s => s.slot), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void GiveSoldiers_SameSeedGivesTheSameSoldiers()
    {
        var a = StartingCaptains.GiveSoldiers(new SoldierInventory(), new[] { "x", "y" }, 9);
        var b = StartingCaptains.GiveSoldiers(new SoldierInventory(), new[] { "x", "y" }, 9);
        Assert.That(a.Select(s => (s.id, s.soldierClass, s.rarity)), Is.EqualTo(b.Select(s => (s.id, s.soldierClass, s.rarity))));
    }
}
