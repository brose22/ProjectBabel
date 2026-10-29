using NUnit.Framework;

public class BoostMathTests
{
    [Test]
    public void DamageTakenMultiplier_NoDefense_TakesFullDamage() =>
        Assert.That(BoostMath.DamageTakenMultiplier(0f), Is.EqualTo(1f).Within(1e-5f));

    [Test]
    public void DamageTakenMultiplier_HalfDefense_TakesHalfDamage() =>
        Assert.That(BoostMath.DamageTakenMultiplier(0.5f), Is.EqualTo(0.5f).Within(1e-5f));

    [Test]
    public void DamageTakenMultiplier_ExcessDefense_ClampsAtMaxDefense() =>
        Assert.That(BoostMath.DamageTakenMultiplier(2f), Is.EqualTo(1f - BoostMath.MaxDefense).Within(1e-5f));

    [Test]
    public void DamageTakenMultiplier_NegativeDefense_TakesFullDamage() =>
        Assert.That(BoostMath.DamageTakenMultiplier(-0.5f), Is.EqualTo(1f).Within(1e-5f));
}
