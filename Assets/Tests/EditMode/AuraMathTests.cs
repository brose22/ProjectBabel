using NUnit.Framework;

public class AuraMathTests
{
    [Test]
    public void FadedStrength_AtWaveStart_IsFullStrength() =>
        Assert.That(AuraMath.FadedStrength(0.5f, 0f, 60f), Is.EqualTo(0.5f).Within(1e-5f));

    [Test]
    public void FadedStrength_HalfwayThroughTheFade_IsHalfStrength() =>
        Assert.That(AuraMath.FadedStrength(0.5f, 30f, 60f), Is.EqualTo(0.25f).Within(1e-5f));

    [TestCase(60f)]
    [TestCase(61f)]
    [TestCase(600f)]
    public void FadedStrength_AtOrPastTheFade_IsZero(float seconds) =>
        Assert.That(AuraMath.FadedStrength(0.5f, seconds, 60f), Is.EqualTo(0f).Within(1e-5f));

    [TestCase(0f)]
    [TestCase(-5f)]
    public void FadedStrength_NonPositiveFadeSeconds_NeverFades(float fadeSeconds) =>
        Assert.That(AuraMath.FadedStrength(0.5f, 1000f, fadeSeconds), Is.EqualTo(0.5f).Within(1e-5f));

    [Test]
    public void DamageTakenMultiplier_NoDefense_TakesFullDamage() =>
        Assert.That(AuraMath.DamageTakenMultiplier(0f), Is.EqualTo(1f).Within(1e-5f));

    [Test]
    public void DamageTakenMultiplier_HalfDefense_TakesHalfDamage() =>
        Assert.That(AuraMath.DamageTakenMultiplier(0.5f), Is.EqualTo(0.5f).Within(1e-5f));

    [Test]
    public void DamageTakenMultiplier_ExcessDefense_ClampsAtMaxDefense() =>
        Assert.That(AuraMath.DamageTakenMultiplier(2f), Is.EqualTo(1f - AuraMath.MaxDefense).Within(1e-5f));

    [Test]
    public void DamageTakenMultiplier_NegativeDefense_TakesFullDamage() =>
        Assert.That(AuraMath.DamageTakenMultiplier(-0.5f), Is.EqualTo(1f).Within(1e-5f));
}
