using NUnit.Framework;

public class AiPartyPickerTests
{
    static AiCaptainInfo Captain(string name, AiRole role, int element, float strength) =>
        new() { name = name, role = role, element = element, strength = strength, range = 3f };

    [Test]
    public void Party_HasASupport_WhenOneIsAvailable()
    {
        var roster = new[]
        {
            Captain("A", AiRole.Attacker, 0, 30), Captain("B", AiRole.Attacker, 1, 28), Captain("C", AiRole.Attacker, 2, 26),
            Captain("D", AiRole.Attacker, 3, 24), Captain("Healer", AiRole.Healer, 0, 5),
        };
        var party = AiPartyPicker.PickParty(roster, 3);
        Assert.That(party, Has.Count.EqualTo(3));
        Assert.That(party, Does.Contain(4));
    }

    [Test]
    public void Party_HasNoDuplicates()
    {
        var roster = new[] { Captain("A", AiRole.Attacker, 0, 30), Captain("B", AiRole.Attacker, 1, 28), Captain("C", AiRole.Attacker, 2, 26) };
        Assert.That(AiPartyPicker.PickParty(roster, 3), Is.Unique);
    }

    [Test]
    public void Party_NeverExceedsRoster()
    {
        var roster = new[] { Captain("A", AiRole.Attacker, 0, 30) };
        Assert.That(AiPartyPicker.PickParty(roster, 3), Has.Count.EqualTo(1));
    }

    [Test]
    public void Recruit_PassesWhenNothingHelps()
    {
        var party = new[] { Captain("A", AiRole.Attacker, 0, 30) };
        Assert.That(AiPartyPicker.PickRecruit(new AiCaptainInfo[0], party), Is.EqualTo(-1));
    }

    [Test]
    public void Recruit_TakesTheFirstSupportWhenPartyHasNone()
    {
        var party = new[] { Captain("A", AiRole.Attacker, 0, 30) };
        var offer = new[] { Captain("B", AiRole.Attacker, 1, 15), Captain("Buffer", AiRole.Buffer, 2, 8) };
        Assert.That(AiPartyPicker.PickRecruit(offer, party), Is.EqualTo(1));
    }

    [Test]
    public void FieldOrder_PutsAttackerThenSupportFirst()
    {
        var party = new[]
        {
            Captain("Weak", AiRole.Attacker, 0, 10), Captain("Healer", AiRole.Healer, 1, 20), Captain("Strong", AiRole.Attacker, 2, 30),
        };
        var order = AiPartyPicker.FieldOrder(party);
        Assert.That(order[0], Is.EqualTo(2));
        Assert.That(order[1], Is.EqualTo(1));
        Assert.That(order, Has.Count.EqualTo(3));
    }
}
