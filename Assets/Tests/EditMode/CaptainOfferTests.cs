using System.Linq;
using NUnit.Framework;

public class CaptainOfferTests
{
    [TestCase(5, 3, 5)]
    [TestCase(2, 3, 3)]
    [TestCase(20, 3, 20)]
    public void Size_IsAtLeastAParty(int setting, int party, int expected) =>
        Assert.That(CaptainOffer.Size(setting, party), Is.EqualTo(expected));

    [Test]
    public void Draw_UsesEveryTemplateOnceBeforeRepeating()
    {
        var offer = CaptainOffer.Draw(5, 7, 42);
        Assert.That(offer.Count, Is.EqualTo(7));
        Assert.That(offer.Select(o => o.template).Distinct().Count(), Is.EqualTo(5));
        Assert.That(offer.Select(o => o.template), Is.Ordered);
        Assert.That(offer.Select(o => o.template), Has.All.InRange(0, 4));
    }

    [Test]
    public void Draw_SmallOfferHasNoRepeats() =>
        Assert.That(CaptainOffer.Draw(5, 3, 9).Select(o => o.template), Is.Unique);

    [Test]
    public void Draw_SameSeedGivesTheSameOffer() =>
        Assert.That(CaptainOffer.Draw(5, 5, 7), Is.EqualTo(CaptainOffer.Draw(5, 5, 7)));

    [Test]
    public void Draw_RollSeedsDiffer() =>
        Assert.That(CaptainOffer.Draw(5, 5, 7).Select(o => o.seed), Is.Unique);

    [Test]
    public void Draw_NoTemplatesGivesNothing() =>
        Assert.That(CaptainOffer.Draw(0, 5, 1), Is.Empty);
}
