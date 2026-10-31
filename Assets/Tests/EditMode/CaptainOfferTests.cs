using System.Linq;
using NUnit.Framework;

public class CaptainOfferTests
{
    [TestCase(5, 3, 12, 5)]
    [TestCase(2, 3, 12, 3)]
    [TestCase(20, 3, 12, 12)]
    [TestCase(5, 3, 4, 4)]
    public void Size_StaysBetweenPartyAndRoster(int setting, int party, int roster, int expected) =>
        Assert.That(CaptainOffer.Size(setting, party, roster), Is.EqualTo(expected));

    [Test]
    public void Draw_GivesDistinctSortedIndicesOfTheSize()
    {
        var offer = CaptainOffer.Draw(12, 5, 42);
        Assert.That(offer.Count, Is.EqualTo(5));
        Assert.That(offer, Is.Unique);
        Assert.That(offer, Is.Ordered);
        Assert.That(offer, Has.All.InRange(0, 11));
    }

    [Test]
    public void Draw_SameSeedGivesTheSameOffer() =>
        Assert.That(CaptainOffer.Draw(12, 5, 7), Is.EqualTo(CaptainOffer.Draw(12, 5, 7)));

    [Test]
    public void Draw_VariesWithTheSeed()
    {
        bool anyDifferent = false;
        for (int seed = 1; seed < 20; seed++)
            if (!CaptainOffer.Draw(12, 5, seed).SequenceEqual(CaptainOffer.Draw(12, 5, 0))) anyDifferent = true;
        Assert.That(anyDifferent);
    }

    [Test]
    public void Draw_AskingForMoreThanTheRosterGivesTheWholeRoster() =>
        Assert.That(CaptainOffer.Draw(4, 9, 1), Is.EqualTo(new[] { 0, 1, 2, 3 }));
}
