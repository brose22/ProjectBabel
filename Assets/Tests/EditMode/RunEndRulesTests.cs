using System.Collections.Generic;
using NUnit.Framework;

public class RunEndRulesTests
{
    [TestCase(1000, 200)]
    [TestCase(99, 19)]
    [TestCase(4, 0)]
    [TestCase(0, 0)]
    [TestCase(-50, 0)]
    public void GoldLost_IsAFifthRoundedDown(int gold, int lost) =>
        Assert.That(RunEndRules.GoldLost(gold), Is.EqualTo(lost));

    [TestCase(1000, 250)]
    [TestCase(99, 25)]
    [TestCase(10, 3)]
    [TestCase(2, 1)]
    [TestCase(1, 0)]
    [TestCase(0, 0)]
    [TestCase(-50, 0)]
    public void AbandonGoldLost_IsAQuarterRounded(int gold, int lost) =>
        Assert.That(RunEndRules.AbandonGoldLost(gold), Is.EqualTo(lost));

    [Test]
    public void Remove_TakesOneSoldierEvenWhenEquipped()
    {
        var soldiers = new SoldierInventory();
        foreach (string id in new[] { "s1", "s2" }) soldiers.Add(new SoldierItem { id = id });
        soldiers.Equip("s1", "cap", 0);
        Assert.That(soldiers.Remove("s1"), Is.True);
        Assert.That(soldiers.Find("s1"), Is.Null);
        Assert.That(soldiers.InSlot("cap", 0), Is.Null);
        Assert.That(soldiers.Find("s2"), Is.Not.Null);
        Assert.That(soldiers.Remove("nope"), Is.False);
    }

    [Test]
    public void CaptainsLost_AreThePartyMembersStillInTheRoster()
    {
        var roster = new List<string> { "a", "b", "c", "d" };
        // "e" walked out during the run, so it has already left the roster.
        var party = new List<string> { "b", "e", "d", "b" };
        Assert.That(RunEndRules.CaptainsLost(party, roster), Is.EqualTo(new[] { "b", "d" }));
    }

    [Test]
    public void CaptainsLost_AreOnlyTheDeployedCaptains()
    {
        var roster = new List<string> { "a", "b", "c", "d" };
        // The party was a, b, c; only a and c were placed on the field, so b goes home safely.
        var deployed = new List<string> { "a", "c" };
        Assert.That(RunEndRules.CaptainsLost(deployed, roster), Is.EqualTo(new[] { "a", "c" }));
        Assert.That(RunEndRules.CaptainsLost(new List<string>(), roster), Is.Empty);
    }

    [Test]
    public void LosingTheParty_TakesTheirSoldiersButNotTheOthers()
    {
        var soldiers = new SoldierInventory();
        foreach (string id in new[] { "s1", "s2", "s3", "s4" }) soldiers.Add(new SoldierItem { id = id });
        soldiers.Equip("s1", "lost", 0);
        soldiers.Equip("s2", "lost", 1);
        soldiers.Equip("s3", "kept", 0);

        Assert.That(soldiers.RemoveEquippedTo("lost"), Is.EqualTo(2));
        Assert.That(soldiers.Find("s1"), Is.Null);
        Assert.That(soldiers.Find("s2"), Is.Null);
        Assert.That(soldiers.Find("s3").equippedTo, Is.EqualTo("kept"));
        Assert.That(soldiers.Find("s4"), Is.Not.Null);
        Assert.That(soldiers.RemoveEquippedTo(""), Is.EqualTo(0));
    }

    [Test]
    public void Join_WithRoom_AddsTheNewcomer()
    {
        var roster = new List<string> { "a", "b" };
        Assert.That(RosterRules.Join(roster, "new", null, 3), Is.True);
        Assert.That(roster, Is.EqualTo(new[] { "a", "b", "new" }));
    }

    [Test]
    public void Join_WithRoom_ReleasesNobody()
    {
        var roster = new List<string> { "a", "b" };
        Assert.That(RosterRules.Join(roster, "new", "a", 3), Is.True);
        Assert.That(roster, Is.EqualTo(new[] { "a", "b", "new" }));
    }

    [Test]
    public void Join_FullRoster_NeedsARelease()
    {
        var roster = new List<string> { "a", "b", "c" };
        Assert.That(RosterRules.NeedsRelease(roster.Count, 3), Is.True);
        Assert.That(RosterRules.NeedsRelease(2, 3), Is.False);
        Assert.That(RosterRules.Join(roster, "new", null, 3), Is.False);
        Assert.That(roster, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void Join_FullRoster_ReleasingTheNewcomerKeepsTheRoster()
    {
        var roster = new List<string> { "a", "b", "c" };
        Assert.That(RosterRules.Join(roster, "new", "new", 3), Is.False);
        Assert.That(roster, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void Join_FullRoster_ReleasingAnExistingCaptainSwapsThem()
    {
        var roster = new List<string> { "a", "b", "c" };
        Assert.That(RosterRules.Join(roster, "new", "b", 3), Is.True);
        Assert.That(roster, Is.EqualTo(new[] { "a", "c", "new" }));
    }

    [Test]
    public void Join_FullRoster_ReleasingAStrangerChangesNothing()
    {
        var roster = new List<string> { "a", "b", "c" };
        Assert.That(RosterRules.Join(roster, "new", "x", 3), Is.False);
        Assert.That(roster, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void Join_SomeoneAlreadyThere_DoesNothing()
    {
        var roster = new List<string> { "a", "b" };
        Assert.That(RosterRules.Join(roster, "a", null, 3), Is.False);
        Assert.That(roster, Is.EqualTo(new[] { "a", "b" }));
    }

    // A won run's unlock is covered by LocationRulesTests (UnlockedAfterWin).
}
