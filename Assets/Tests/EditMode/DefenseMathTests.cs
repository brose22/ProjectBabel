using NUnit.Framework;

public class DefenseMathTests
{
    [TestCase(10f, 3f, 7f)]
    [TestCase(10f, 0f, 10f)]
    [TestCase(5.625f, 2f, 3.625f)]
    public void DamageAfterDefense_SubtractsDefense(float damage, float defense, float expected) =>
        Assert.That(DefenseMath.DamageAfterDefense(damage, defense), Is.EqualTo(expected).Within(1e-5f));

    [TestCase(4f, 4f)]
    [TestCase(4f, 10f)]
    [TestCase(1.5f, 3f)]
    public void DamageAfterDefense_NeverGoesBelowTheMinimum(float damage, float defense) =>
        Assert.That(DefenseMath.DamageAfterDefense(damage, defense), Is.EqualTo(DefenseMath.MinDamage));

    [Test]
    public void DamageAfterDefense_SmallHitKeepsItsValue() =>
        Assert.That(DefenseMath.DamageAfterDefense(0.5f, 3f), Is.EqualTo(0.5f));

    [Test]
    public void DamageAfterDefense_NoHitStaysZero() =>
        Assert.That(DefenseMath.DamageAfterDefense(0f, 3f), Is.EqualTo(0f));

    [Test]
    public void DamageAfterDefense_NegativeDefenseAddsNothing() =>
        Assert.That(DefenseMath.DamageAfterDefense(10f, -5f), Is.EqualTo(10f));

    [Test]
    public void DamageAfterDefense_AppliesAfterTheMultiplier()
    {
        // A Raider (Melee/Fire, 7.5) on a Melee/Water tower: x0.75, then Defense 2.
        float hit = 7.5f * DamageTypeRules.CombinedMultiplier(DamageType.Melee, 1, DamageType.Melee, 0);
        Assert.That(DefenseMath.DamageAfterDefense(hit, 2f), Is.EqualTo(3.625f).Within(1e-5f));
    }

    [TestCase(1, 1f)]
    [TestCase(2, 1.25f)]
    [TestCase(10, 3.25f)]
    public void AtLevel_GrowsPerLevelAboveOne(int level, float expected) =>
        Assert.That(DefenseMath.AtLevel(1f, 0.25f, level), Is.EqualTo(expected).Within(1e-5f));

    [Test]
    public void AtLevel_TreatsLevelsBelowOneAsOne() =>
        Assert.That(DefenseMath.AtLevel(1f, 0.25f, 0), Is.EqualTo(1f));
}
