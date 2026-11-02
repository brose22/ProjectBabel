using NUnit.Framework;

public class TowerReachTests
{
    [Test]
    public void Range_IsTheAverageOfTheSlotReaches() =>
        Assert.That(TowerReach.Range(new[] { 2f, 3f, 2f, 3f }, 0f), Is.EqualTo(2.5f).Within(1e-5f));

    [Test]
    public void Range_AllKnightsIsTheLowerLimit() =>
        Assert.That(TowerReach.Range(new[] { 1.5f, 1.5f, 1.5f, 1.5f }, 0f), Is.EqualTo(1.5f).Within(1e-5f));

    [Test]
    public void Range_AllMagesIsTheUpperLimit() =>
        Assert.That(TowerReach.Range(new[] { 3f, 3f, 3f, 3f }, 0f), Is.EqualTo(3f).Within(1e-5f));

    [Test]
    public void Range_AddsTheCaptainBonus() =>
        Assert.That(TowerReach.Range(new[] { 2f, 4f }, 0.5f), Is.EqualTo(3.5f).Within(1e-5f));

    [Test]
    public void Range_WithoutSlotsIsTheBonus() =>
        Assert.That(TowerReach.Range(new float[0], 1f), Is.EqualTo(1f));
}
