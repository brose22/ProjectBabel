using NUnit.Framework;

public class ElementRulesTests
{
    const int Water = 0, Fire = 1, Ice = 2, Grass = 3;

    [TestCase(Water, Water, 1f)]
    [TestCase(Water, Fire, 1.5f)]
    [TestCase(Water, Ice, 1f)]
    [TestCase(Water, Grass, 0.75f)]
    [TestCase(Fire, Water, 0.75f)]
    [TestCase(Fire, Fire, 1f)]
    [TestCase(Fire, Ice, 1.5f)]
    [TestCase(Fire, Grass, 1f)]
    [TestCase(Ice, Water, 1f)]
    [TestCase(Ice, Fire, 0.75f)]
    [TestCase(Ice, Ice, 1f)]
    [TestCase(Ice, Grass, 1.5f)]
    [TestCase(Grass, Water, 1.5f)]
    [TestCase(Grass, Fire, 1f)]
    [TestCase(Grass, Ice, 0.75f)]
    [TestCase(Grass, Grass, 1f)]
    public void DamageMultiplier_FollowsTheCycle(int attacker, int target, float expected) =>
        Assert.That(ElementRules.DamageMultiplier(attacker, target), Is.EqualTo(expected).Within(1e-5f));

    [TestCase(-1, Water)]
    [TestCase(Grass, 4)]
    public void DamageMultiplier_UnknownElementIsNeutral(int attacker, int target) =>
        Assert.That(ElementRules.DamageMultiplier(attacker, target), Is.EqualTo(1f));

    [Test]
    public void EveryElementBeatsOneAndLosesToOne()
    {
        for (int e = 0; e < ElementRules.ElementCount; e++)
        {
            int beats = 0, losesTo = 0;
            for (int other = 0; other < ElementRules.ElementCount; other++)
            {
                if (ElementRules.Beats(e, other)) beats++;
                if (ElementRules.Beats(other, e)) losesTo++;
            }
            Assert.That(beats, Is.EqualTo(1), $"element {e} beats");
            Assert.That(losesTo, Is.EqualTo(1), $"element {e} loses to");
        }
    }
}
