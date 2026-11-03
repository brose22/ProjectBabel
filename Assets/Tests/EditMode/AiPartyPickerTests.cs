using NUnit.Framework;

public class AiPartyPickerTests
{
    static AiCaptainInfo Captain(string name, bool support, int element, float strength) =>
        new() { name = name, role = support ? CaptainRole.Healer : CaptainRole.Knight, support = support, element = element, strength = strength, range = 3f };

    [Test]
    public void Party_HasASupport_WhenOneIsAvailable()
    {
        var roster = new[]
        {
            Captain("A", false, 0, 30), Captain("B", false, 1, 28), Captain("C", false, 2, 26),
            Captain("D", false, 3, 24), Captain("Healer", true, 0, 5),
        };
        var party = AiPartyPicker.PickParty(roster, 3);
        Assert.That(party, Has.Count.EqualTo(3));
        Assert.That(party, Does.Contain(4));
    }

    [Test]
    public void Party_HasNoDuplicates()
    {
        var roster = new[] { Captain("A", false, 0, 30), Captain("B", false, 1, 28), Captain("C", false, 2, 26) };
        Assert.That(AiPartyPicker.PickParty(roster, 3), Is.Unique);
    }

    [Test]
    public void Party_NeverExceedsRoster()
    {
        var roster = new[] { Captain("A", false, 0, 30) };
        Assert.That(AiPartyPicker.PickParty(roster, 3), Has.Count.EqualTo(1));
    }

    [Test]
    public void Recruit_PassesWhenNothingHelps()
    {
        var party = new[] { Captain("A", false, 0, 30) };
        Assert.That(AiPartyPicker.PickRecruit(new AiCaptainInfo[0], party), Is.EqualTo(-1));
    }

    [Test]
    public void Recruit_TakesTheFirstSupportWhenPartyHasNone()
    {
        var party = new[] { Captain("A", false, 0, 30) };
        var offer = new[] { Captain("B", false, 1, 15), Captain("Buffer", true, 2, 8) };
        Assert.That(AiPartyPicker.PickRecruit(offer, party), Is.EqualTo(1));
    }

    [Test]
    public void FieldOrder_PutsAttackerThenSupportFirst()
    {
        var party = new[]
        {
            Captain("Weak", false, 0, 10), Captain("Healer", true, 1, 20), Captain("Strong", false, 2, 30),
        };
        var order = AiPartyPicker.FieldOrder(party);
        Assert.That(order[0], Is.EqualTo(2));
        Assert.That(order[1], Is.EqualTo(1));
        Assert.That(order, Has.Count.EqualTo(3));
    }
}
