using NUnit.Framework;

public class AiAbilityRulesTests
{
    [Test]
    public void Attack_WaitsForACrowdOrBoss()
    {
        Assert.That(AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Attack, 1, false, 1f, 10, false), Is.False);
        Assert.That(AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Attack, 3, false, 1f, 10, false), Is.True);
        Assert.That(AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Attack, 1, true, 1f, 10, false), Is.True);
    }

    [Test]
    public void Attack_FiresAtTheTailOfAWave()
    {
        Assert.That(AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Attack, 1, false, 1f, 2, true), Is.True);
        Assert.That(AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Attack, 0, false, 1f, 2, true), Is.False);
    }

    [Test]
    public void Heal_FiresWhenSomeoneIsHurt()
    {
        Assert.That(AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Heal, 0, false, 0.4f, 5, false), Is.True);
        Assert.That(AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Heal, 0, false, 0.9f, 5, false), Is.False);
    }

    [Test]
    public void Buff_FiresOnceEnemiesAreOut()
    {
        Assert.That(AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Buff, 0, false, 1f, 4, false), Is.True);
        Assert.That(AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Buff, 0, false, 1f, 0, false), Is.False);
    }

    [Test]
    public void Requests_AreGrantedUnlessTheyCostGold()
    {
        Assert.That(AiAbilityRules.AnswerRequest(AiAbilityRules.RequestRelocate, 0, 3), Is.EqualTo(AiAbilityRules.AnswerYes));
        Assert.That(AiAbilityRules.AnswerRequest(AiAbilityRules.RequestMoreDraw, 500, 3), Is.EqualTo(AiAbilityRules.AnswerYes));
        Assert.That(AiAbilityRules.AnswerRequest(AiAbilityRules.RequestMoreDraw, 50, 3), Is.EqualTo(AiAbilityRules.AnswerNo));
        Assert.That(AiAbilityRules.AnswerRequest(AiAbilityRules.RequestUseSpecial, 50, 3), Is.EqualTo(AiAbilityRules.AnswerYes));
    }
}
