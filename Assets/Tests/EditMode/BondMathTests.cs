using NUnit.Framework;

public class BondMathTests
{
    [TestCase(-50f, 0)]
    [TestCase(0f, 0)]
    [TestCase(39.9f, 0)]
    [TestCase(40f, 1)]
    [TestCase(64f, 1)]
    [TestCase(65f, 2)]
    [TestCase(85f, 3)]
    [TestCase(99f, 3)]
    [TestCase(100f, 4)]
    public void RankFor_UsesTheRankThresholds(float approval, int expected) =>
        Assert.That(BondMath.RankFor(approval), Is.EqualTo(expected));

    [Test]
    public void RankName_NamesTheEndsOfTheScale()
    {
        Assert.That(BondMath.RankName(0), Is.EqualTo("D"));
        Assert.That(BondMath.RankName(4), Is.EqualTo("S"));
    }

    [Test]
    public void RankName_ClampsOutOfRangeRanks()
    {
        Assert.That(BondMath.RankName(-1), Is.EqualTo("D"));
        Assert.That(BondMath.RankName(9), Is.EqualTo("S"));
    }

    [TestCase(0, 2)]
    [TestCase(1, 3)]
    [TestCase(2, 3)]
    [TestCase(3, 4)]
    [TestCase(4, 4)]
    public void SoldierCount_GrowsWithRank(int rank, int expected) =>
        Assert.That(BondMath.SoldierCount(rank), Is.EqualTo(expected));

    [TestCase(0, 0f)]
    [TestCase(2, 0.1f)]
    [TestCase(4, 0.2f)]
    public void StatusChanceBonus_RisesWithRank(int rank, float expected) =>
        Assert.That(BondMath.StatusChanceBonus(rank), Is.EqualTo(expected).Within(1e-5f));

    [TestCase(0f, 0f)]
    [TestCase(-40f, 0f)]
    [TestCase(100f, 0.4f)]
    [TestCase(250f, 0.4f)]
    public void ReserveEfficiencyBonus_OnlyPositiveApprovalHelps_AndIsClamped(float approval, float expected) =>
        Assert.That(BondMath.ReserveEfficiencyBonus(approval), Is.EqualTo(expected).Within(1e-5f));

    [TestCase(0f, 1f)]
    [TestCase(-30f, 1f)]
    [TestCase(100f, 1.5f)]
    public void ChargeRateMultiplier_OnlyPositiveApprovalHelps(float approval, float expected) =>
        Assert.That(BondMath.ChargeRateMultiplier(approval), Is.EqualTo(expected).Within(1e-5f));

    [TestCase(40f, 20f, true)]
    [TestCase(40f, 21f, false)]
    [TestCase(0f, -20f, true)]
    public void HasWalkedOut_TriggersAtTwentyBelowBest(float best, float current, bool expected) =>
        Assert.That(BondMath.HasWalkedOut(best, current), Is.EqualTo(expected));

    [TestCase(-250f, -100f)]
    [TestCase(-100f, -100f)]
    [TestCase(37.5f, 37.5f)]
    [TestCase(100f, 100f)]
    [TestCase(250f, 100f)]
    public void Clamp_KeepsApprovalWithinBounds(float approval, float expected) =>
        Assert.That(BondMath.Clamp(approval), Is.EqualTo(expected));

    [TestCase(0, 1f)]
    [TestCase(1, 0.85f)]
    [TestCase(4, 0.4f)]
    [TestCase(9, 0.4f)]
    public void GainMultiplier_FallsWithRank(int rank, float expected) =>
        Assert.That(BondMath.GainMultiplier(rank), Is.EqualTo(expected).Within(1e-5f));

    [TestCase(0, 64f)]
    [TestCase(1, 84f)]
    [TestCase(2, 99f)]
    [TestCase(3, 100f)]
    [TestCase(4, 100f)]
    public void RunApprovalCap_StopsShortOfTheRankAfterNext(int startRank, float expected) =>
        Assert.That(BondMath.RunApprovalCap(startRank), Is.EqualTo(expected));

    [Test]
    public void RunApprovalCap_KeepsTheRankWithinOneOfTheStart()
    {
        for (int start = 0; start <= BondMath.MaxRank; start++)
            Assert.That(BondMath.RankFor(BondMath.RunApprovalCap(start)), Is.LessThanOrEqualTo(start + 1));
    }
}
