using NUnit.Framework;

public class DamageTypeRulesTests
{
    const int Water = 0, Fire = 1, Ice = 2, Grass = 3;

    [TestCase(DamageType.Melee, DamageType.Melee, 1f)]
    [TestCase(DamageType.Melee, DamageType.Range, 1.5f)]
    [TestCase(DamageType.Melee, DamageType.Magic, 0.75f)]
    [TestCase(DamageType.Range, DamageType.Melee, 0.75f)]
    [TestCase(DamageType.Range, DamageType.Range, 1f)]
    [TestCase(DamageType.Range, DamageType.Magic, 1.5f)]
    [TestCase(DamageType.Magic, DamageType.Melee, 1.5f)]
    [TestCase(DamageType.Magic, DamageType.Range, 0.75f)]
    [TestCase(DamageType.Magic, DamageType.Magic, 1f)]
    public void DamageMultiplier_FollowsTheTriangle(DamageType attacker, DamageType target, float expected) =>
        Assert.That(DamageTypeRules.DamageMultiplier(attacker, target), Is.EqualTo(expected).Within(1e-5f));

    [Test]
    public void DamageMultiplier_UnknownTypeIsNeutral() =>
        Assert.That(DamageTypeRules.DamageMultiplier((DamageType)5, DamageType.Melee), Is.EqualTo(1f));

    [Test]
    public void EveryTypeBeatsOneAndLosesToOne()
    {
        for (int t = 0; t < DamageTypeRules.TypeCount; t++)
        {
            int beats = 0, losesTo = 0;
            for (int other = 0; other < DamageTypeRules.TypeCount; other++)
            {
                if (DamageTypeRules.Beats((DamageType)t, (DamageType)other)) beats++;
                if (DamageTypeRules.Beats((DamageType)other, (DamageType)t)) losesTo++;
            }
            Assert.That(beats, Is.EqualTo(1), $"type {t} beats");
            Assert.That(losesTo, Is.EqualTo(1), $"type {t} loses to");
        }
    }

    [TestCase(DamageType.Melee, Water, DamageType.Range, Fire, 2.25f)]
    [TestCase(DamageType.Range, Fire, DamageType.Melee, Water, 0.5625f)]
    [TestCase(DamageType.Melee, Water, DamageType.Magic, Fire, 1.125f)]
    [TestCase(DamageType.Melee, Grass, DamageType.Range, Water, 2.25f)]
    [TestCase(DamageType.Magic, Water, DamageType.Magic, Ice, 1f)]
    [TestCase(DamageType.Range, Ice, DamageType.Range, Fire, 0.75f)]
    [TestCase(DamageType.Range, -1, DamageType.Magic, Grass, 1.5f)]
    public void CombinedMultiplier_StacksBothMatchups(DamageType attackerType, int attackerElement,
        DamageType targetType, int targetElement, float expected) =>
        Assert.That(DamageTypeRules.CombinedMultiplier(attackerType, attackerElement, targetType, targetElement),
            Is.EqualTo(expected).Within(1e-5f));

    [Test]
    public void CombinedMultiplier_RangesFromQuarterLossToDoubleAndAQuarter()
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int a = 0; a < DamageTypeRules.TypeCount; a++)
        for (int ae = 0; ae < ElementRules.ElementCount; ae++)
        for (int t = 0; t < DamageTypeRules.TypeCount; t++)
        for (int te = 0; te < ElementRules.ElementCount; te++)
        {
            float m = DamageTypeRules.CombinedMultiplier((DamageType)a, ae, (DamageType)t, te);
            min = System.Math.Min(min, m);
            max = System.Math.Max(max, m);
        }
        Assert.That(min, Is.EqualTo(0.5625f).Within(1e-5f));
        Assert.That(max, Is.EqualTo(2.25f).Within(1e-5f));
    }

    [Test]
    public void EachTypeIsWeakToTheTypeThatBeatsIt()
    {
        Assert.That(DamageTypeRules.WeakTo(DamageType.Range), Is.EqualTo(DamageType.Melee));
        Assert.That(DamageTypeRules.WeakTo(DamageType.Magic), Is.EqualTo(DamageType.Range));
        Assert.That(DamageTypeRules.WeakTo(DamageType.Melee), Is.EqualTo(DamageType.Magic));
        for (int t = 0; t < DamageTypeRules.TypeCount; t++)
            Assert.That(DamageTypeRules.Beats(DamageTypeRules.WeakTo((DamageType)t), (DamageType)t));
    }
}
