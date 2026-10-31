using NUnit.Framework;

public class ArmorRulesTests
{
    const int Knight = 0, Archer = 1, Mage = 2, Rogue = 3;

    [TestCase(ArmorType.Unarmored, Knight, 1f)]
    [TestCase(ArmorType.Unarmored, Mage, 1f)]
    [TestCase(ArmorType.Armored, Knight, 0.65f)]
    [TestCase(ArmorType.Armored, Mage, 0.65f)]
    [TestCase(ArmorType.Armored, Archer, 1f)]
    [TestCase(ArmorType.Armored, Rogue, 1.4f)]
    [TestCase(ArmorType.Swift, Archer, 0.65f)]
    [TestCase(ArmorType.Swift, Rogue, 0.65f)]
    [TestCase(ArmorType.Swift, Mage, 1.4f)]
    [TestCase(ArmorType.Swift, Knight, 1f)]
    public void DamageMultiplier_FollowsTheCounterTable(ArmorType armor, int soldierClass, float expected) =>
        Assert.That(ArmorRules.DamageMultiplier(armor, soldierClass), Is.EqualTo(expected).Within(1e-5f));

    [TestCase(-1)]
    [TestCase(4)]
    public void DamageMultiplier_UnknownClassIsNeutral(int soldierClass) =>
        Assert.That(ArmorRules.DamageMultiplier(ArmorType.Armored, soldierClass), Is.EqualTo(1f));

    [Test]
    public void EveryArmoredTypeHasAClassItCountersAndOneThatCountersIt()
    {
        foreach (ArmorType armor in new[] { ArmorType.Armored, ArmorType.Swift })
        {
            bool strong = false, weak = false;
            for (int c = 0; c < ArmorRules.ClassCount; c++)
            {
                float m = ArmorRules.DamageMultiplier(armor, c);
                strong |= m > 1f;
                weak |= m < 1f;
            }
            Assert.That(strong && weak, Is.True, armor.ToString());
        }
    }
}
