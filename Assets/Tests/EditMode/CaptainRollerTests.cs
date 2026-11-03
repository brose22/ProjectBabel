using System.Linq;
using NUnit.Framework;

public class CaptainRollerTests
{
    static CaptainRollSpec Spec(CaptainRole role = CaptainRole.Knight, float special = 0.15f) => new()
    {
        role = role,
        specialClassChance = special,
        power = new StatRange(7f, 14f),
        range = new StatRange(0f, 0f),
        speed = new StatRange(0.6f, 1f),
        health = new StatRange(100f, 120f),
        defense = new StatRange(1.5f, 2.5f),
        defensePerLevel = new StatRange(0.25f, 0.4f),
        refusalTemper = new StatRange(0.75f, 1.25f),
        nameCount = 6,
        portraitElements = new[] { 0, 3, 3 },
        abilityCount = 3,
        likePool = new[] { 0, 1, 2, 3, 6, 10 },
        dislikePool = new[] { 0, 2, 4, 5, 6, 7, 11 },
        likedGiftPool = new[] { 0, 1, 2, 3 },
        dislikedGiftPool = new[] { 3, 4, 5 },
        greetingCount = 2,
        likedLineCount = 2,
        neutralLineCount = 2,
        dislikedLineCount = 2,
    };

    [Test]
    public void Roll_SameSeedGivesTheSameCaptain()
    {
        CaptainRoll a = CaptainRoller.Roll(Spec(), 1234), b = CaptainRoller.Roll(Spec(), 1234);
        Assert.That(a.id, Is.EqualTo(b.id));
        Assert.That((a.nameIndex, a.portraitIndex, a.abilityIndex, a.element, a.damageType), Is.EqualTo((b.nameIndex, b.portraitIndex, b.abilityIndex, b.element, b.damageType)));
        Assert.That((a.power, a.speed, a.health, a.defense, a.defensePerLevel, a.refusalTemper), Is.EqualTo((b.power, b.speed, b.health, b.defense, b.defensePerLevel, b.refusalTemper)));
        Assert.That(a.likes, Is.EqualTo(b.likes));
        Assert.That(a.dislikes, Is.EqualTo(b.dislikes));
        Assert.That(a.likedGifts, Is.EqualTo(b.likedGifts));
        Assert.That(a.dislikedGifts, Is.EqualTo(b.dislikedGifts));
        Assert.That((a.greetingLine, a.likedLine, a.neutralLine, a.dislikedLine), Is.EqualTo((b.greetingLine, b.likedLine, b.neutralLine, b.dislikedLine)));
    }

    [Test]
    public void Roll_DifferentSeedsGiveDifferentIds() =>
        Assert.That(Enumerable.Range(0, 200).Select(seed => CaptainRoller.Roll(Spec(), seed).id), Is.Unique);

    [Test]
    public void Roll_NeighbouringSeedsSpreadOverElementsAndNames()
    {
        var elements = new int[ElementRules.ElementCount];
        var names = new int[6];
        for (int seed = 0; seed < 400; seed++)
        {
            CaptainRoll roll = CaptainRoller.Roll(Spec(), seed);
            elements[roll.element]++;
            names[roll.nameIndex]++;
        }
        Assert.That(elements, Has.All.InRange(60, 140));
        Assert.That(names, Has.All.InRange(35, 100));
    }

    [Test]
    public void Roll_StatsStayInTheirRanges()
    {
        CaptainRollSpec spec = Spec();
        for (int seed = 0; seed < 500; seed++)
        {
            CaptainRoll roll = CaptainRoller.Roll(spec, seed);
            Assert.That(spec.power.Contains(roll.power), $"power {roll.power}");
            Assert.That(roll.range, Is.EqualTo(0f));
            Assert.That(spec.speed.Contains(roll.speed), $"speed {roll.speed}");
            Assert.That(roll.health, Is.InRange(100, 120));
            Assert.That(spec.defense.Contains(roll.defense), $"defense {roll.defense}");
            Assert.That(spec.defensePerLevel.Contains(roll.defensePerLevel), $"defense per level {roll.defensePerLevel}");
            Assert.That(spec.refusalTemper.Contains(roll.refusalTemper), $"temper {roll.refusalTemper}");
            Assert.That(roll.nameIndex, Is.InRange(0, 5));
            Assert.That(roll.abilityIndex, Is.InRange(0, 2));
            Assert.That(roll.element, Is.InRange(0, ElementRules.ElementCount - 1));
        }
    }

    [Test]
    public void Roll_LikesAndDislikesComeFromTheirPoolsAndNeverOverlap()
    {
        CaptainRollSpec spec = Spec();
        for (int seed = 0; seed < 200; seed++)
        {
            CaptainRoll roll = CaptainRoller.Roll(spec, seed);
            Assert.That(roll.likes.Count, Is.EqualTo(3));
            Assert.That(roll.dislikes.Count, Is.EqualTo(3));
            Assert.That(roll.likes, Is.Unique.And.SubsetOf(spec.likePool));
            Assert.That(roll.dislikes, Is.Unique.And.SubsetOf(spec.dislikePool));
            Assert.That(roll.likes.Intersect(roll.dislikes), Is.Empty);
            Assert.That(roll.likedGifts.Count, Is.EqualTo(3));
            Assert.That(roll.dislikedGifts.Count, Is.EqualTo(2));
            Assert.That(roll.likedGifts.Intersect(roll.dislikedGifts), Is.Empty);
        }
    }

    [Test]
    public void Roll_PortraitMatchesTheElementWhenOneDoes()
    {
        CaptainRollSpec spec = Spec();
        for (int seed = 0; seed < 200; seed++)
        {
            CaptainRoll roll = CaptainRoller.Roll(spec, seed);
            if (roll.element is 0 or 3) Assert.That(spec.portraitElements[roll.portraitIndex], Is.EqualTo(roll.element));
            else Assert.That(roll.portraitIndex, Is.InRange(0, 2));
        }
    }

    [Test]
    public void Roll_EmptyPoolsGiveNoPick()
    {
        CaptainRoll roll = CaptainRoller.Roll(new CaptainRollSpec(), 5);
        Assert.That((roll.nameIndex, roll.portraitIndex, roll.abilityIndex, roll.greetingLine), Is.EqualTo((-1, -1, -1, -1)));
        Assert.That(roll.likes, Is.Empty);
    }

    [TestCase(CaptainRole.Knight, DamageType.Melee)]
    [TestCase(CaptainRole.Mage, DamageType.Magic)]
    [TestCase(CaptainRole.Rogue, DamageType.Melee)]
    [TestCase(CaptainRole.Archer, DamageType.Range)]
    [TestCase(CaptainRole.Healer, DamageType.Magic)]
    public void UsualDamageType_FollowsTheRole(CaptainRole role, DamageType expected) =>
        Assert.That(CaptainRoller.UsualDamageType(role), Is.EqualTo(expected));

    [Test]
    public void Roll_SpecialClassesAreUncommonButHappen()
    {
        int special = 0;
        const int rolls = 2000;
        for (int seed = 0; seed < rolls; seed++)
            if (CaptainRoller.IsSpecialClass(CaptainRole.Knight, CaptainRoller.Roll(Spec(), seed).damageType)) special++;
        Assert.That(special, Is.GreaterThan(rolls * 0.08f).And.LessThan(rolls * 0.22f));
    }

    [Test]
    public void Roll_NoSpecialChanceAlwaysGivesTheUsualType()
    {
        for (int seed = 0; seed < 200; seed++)
            Assert.That(CaptainRoller.Roll(Spec(CaptainRole.Archer, 0f), seed).damageType, Is.EqualTo(DamageType.Range));
    }
}
