using System.Collections.Generic;
using NUnit.Framework;

public class PartyRulesTests
{
    class C { public string id; public C(string id) => this.id = id; }

    [Test]
    public void Leave_RemovesFromPartyAndRoster()
    {
        C a = new("a"), b = new("b"), c = new("c");
        var party = new List<C> { a, b, c };
        var roster = new List<C> { a, b, c };
        Assert.That(PartyRules.Leave(party, roster, b), Is.True);
        Assert.That(party, Is.EqualTo(new[] { a, c }));
        Assert.That(roster, Is.EqualTo(new[] { a, c }));
        Assert.That(party.Count, Is.EqualTo(2));
    }

    [Test]
    public void Leave_CaptainNotInParty_LeavesPartyAlone()
    {
        C a = new("a"), b = new("b");
        var party = new List<C> { a };
        Assert.That(PartyRules.Leave(party, null, b), Is.False);
        Assert.That(PartyRules.Leave(party, null, null), Is.False);
        Assert.That(party, Is.EqualTo(new[] { a }));
    }

    [Test]
    public void Leave_ThenLostRun_DoesNotCountTheWalkoutTwice()
    {
        C a = new("a"), b = new("b");
        var party = new List<C> { a, b };
        var roster = new List<C> { a, b };
        PartyRules.Leave(party, roster, b);
        Assert.That(RunEndRules.CaptainsLost(party, roster), Is.EqualTo(new[] { a }));
    }
}
