using NUnit.Framework;

public class RequestRulesTests
{
    [TestCase(RequestOutcome.Refused, -3f)]
    [TestCase(RequestOutcome.Granted, 2f)]
    [TestCase(RequestOutcome.Expired, 0f)]
    public void ApprovalChange_ByOutcome(RequestOutcome outcome, float expected) =>
        Assert.That(RequestRules.ApprovalChange(outcome, 2f, 3f), Is.EqualTo(expected));

    static RequestSnapshot Snap(RequestKind kind) => new RequestSnapshot { Kind = kind, ApprovalYes = 2f, Gold = 300, SpawnsToElite = -1, Area = "road 1" };

    [Test]
    public void MoreDraw_PoorRunIsBad_RichRunIsNot()
    {
        RequestSnapshot poor = Snap(RequestKind.MoreDraw); poor.Gold = 20; poor.DrawingCaptains = 1;
        RequestSnapshot rich = Snap(RequestKind.MoreDraw); rich.Gold = 600;
        Assert.That(RequestEvaluator.Evaluate(poor).IsBad, Is.True);
        Assert.That(RequestEvaluator.Evaluate(rich).IsBad, Is.False);
        Assert.That(RequestEvaluator.Evaluate(poor).Cons[0].Text, Does.Contain("20 Gold left").And.Contain("2 captains"));
    }

    [Test]
    public void UseSpecial_EliteSoonIsBadWithoutAThreat()
    {
        RequestSnapshot soon = Snap(RequestKind.UseSpecial); soon.SpawnsToElite = 2;
        RequestSnapshot far = Snap(RequestKind.UseSpecial); far.SpawnsToElite = 12;
        RequestSnapshot soonThreat = soon; soonThreat.ThreatPresent = true;
        Assert.That(RequestEvaluator.Evaluate(soon).IsBad, Is.True);
        Assert.That(RequestEvaluator.Evaluate(soon).Cons[0].Text, Does.Contain("Elite in 2 spawns"));
        Assert.That(RequestEvaluator.Evaluate(far).IsBad, Is.False);
        Assert.That(RequestEvaluator.Evaluate(soonThreat).IsBad, Is.False);
    }

    [TestCase(RequestKind.Relocate)]
    [TestCase(RequestKind.Reinforce)]
    public void Moves_NameWhatTheyLeaveUncovered(RequestKind kind)
    {
        RequestSnapshot s = Snap(kind); s.UncoveredRoadTiles = 10;
        RequestEvaluation bad = RequestEvaluator.Evaluate(s);
        Assert.That(bad.IsBad, Is.True);
        Assert.That(bad.Cons[0].Text, Does.Contain("10 road tiles").And.Contain("road 1"));
        s.UncoveredRoadTiles = 0;
        Assert.That(RequestEvaluator.Evaluate(s).IsBad, Is.False);
    }

    [Test]
    public void LowerDraw_CostsHalfTheReserve()
    {
        RequestSnapshot big = Snap(RequestKind.LowerDraw); big.ReserveGold = 80;
        RequestSnapshot small = Snap(RequestKind.LowerDraw); small.ReserveGold = 10;
        Assert.That(RequestEvaluator.Evaluate(big).Cons[0].Text, Does.Contain("keeps 40 Gold"));
        Assert.That(RequestEvaluator.Evaluate(big).IsBad, Is.True);
        Assert.That(RequestEvaluator.Evaluate(small).IsBad, Is.False);
    }

    [Test]
    public void RoadReach_CountLost_CountsOnlyTilesDropped()
    {
        var road = new System.Collections.Generic.List<UnityEngine.Vector2> { new(0, 0), new(1, 0), new(5, 0) };
        Assert.That(RoadReach.CountLost(new UnityEngine.Vector2(0, 0), new UnityEngine.Vector2(5, 0), road, 1.5f), Is.EqualTo(2));
    }

    [Test]
    public void GreedyAndGloryAsksAreBad_WhenMatchMakesThemSo()
    {
        RequestSnapshot greedy = Snap(RequestKind.MoreDraw); greedy.Gold = 40;
        RequestSnapshot glory = Snap(RequestKind.UseSpecial); glory.SpawnsToElite = 5;
        RequestSnapshot cautious = Snap(RequestKind.Retreat); cautious.UncoveredRoadTiles = 8;
        Assert.That(RequestEvaluator.Evaluate(greedy).IsBad, Is.True);
        Assert.That(RequestEvaluator.Evaluate(glory).IsBad, Is.True);
        Assert.That(RequestEvaluator.Evaluate(glory).Cons.Exists(c => c.Text.Contains("weak enemies")), Is.True);
        Assert.That(RequestEvaluator.Evaluate(cautious).IsBad, Is.True);
    }
}

public class CaptainTraitTests
{
    [Test]
    public void Roll_GivesEveryTrait_AndIsDeterministic()
    {
        var seen = new System.Collections.Generic.HashSet<CaptainTrait>();
        for (int i = 0; i < 200; i++) seen.Add(CaptainTraits.Roll(new System.Random(i)));
        Assert.That(seen.Count, Is.EqualTo(CaptainTraits.Count));
        Assert.That(CaptainTraits.ForLegacyId("abc"), Is.EqualTo(CaptainTraits.ForLegacyId("abc")));
    }
}

public class RequestPaybackTests
{
    [Test]
    public void CleanWave_RefundsPenaltyPlusBonus() => Assert.That(RequestRules.Payback(3f, true), Is.EqualTo(4f));

    [Test]
    public void DirtyWave_RefundsNothing() => Assert.That(RequestRules.Payback(3f, false), Is.EqualTo(0f));
}

public class RivalRulesTests
{
    [Test]
    public void PickPair_NeedsTwoCaptains()
    {
        Assert.That(RivalRules.PickPair(new[] { CaptainTrait.Greedy }, new System.Random(1)), Is.Null);
        var pair = RivalRules.PickPair(new[] { CaptainTrait.Greedy, CaptainTrait.Cautious }, new System.Random(1));
        Assert.That(pair, Is.EqualTo((0, 1)));
    }

    [Test]
    public void PickPair_FavoursClashingTraits()
    {
        var traits = new[] { CaptainTrait.Steady, CaptainTrait.Steady, CaptainTrait.Greedy, CaptainTrait.Cautious };
        int clashing = 0;
        for (int i = 0; i < 400; i++)
            if (RivalRules.PickPair(traits, new System.Random(i)) == (2, 3)) clashing++;
        // Weight 6 of 1+2+2+2+2+6 = 15: about 40% of picks.
        Assert.That(clashing, Is.InRange(100, 220));
    }

    [Test]
    public void Cost_RisesWhenRequestBringsTheTwoTogether()
    {
        Assert.That(RivalRules.Cost(RequestKind.UseSpecial, false, 1f), Is.EqualTo(1f));
        Assert.That(RivalRules.Cost(RequestKind.Reinforce, true, 1f), Is.EqualTo(2f));
        Assert.That(RivalRules.Cost(RequestKind.MoreDraw, false, 0.2f), Is.EqualTo(2f));
        Assert.That(RivalRules.Cost(RequestKind.MoreDraw, false, 0.9f), Is.EqualTo(1f));
    }

    [Test]
    public void Evaluate_ListsRivalCostAsACon()
    {
        var s = new RequestSnapshot { Kind = RequestKind.UseSpecial, ApprovalYes = 2f, Gold = 300, SpawnsToElite = -1, RivalName = "Ser Aldric", RivalCost = 1f };
        Assert.That(RequestEvaluator.Evaluate(s).Cons.Exists(c => c.Text == "Ser Aldric: -1 approval"), Is.True);
    }
}

public class AiRequestAnswerTests
{
    [Test]
    public void Refuses_BadRequests_AndGrantsCheapOnes()
    {
        var bad = new RequestSnapshot { Kind = RequestKind.MoreDraw, ApprovalYes = 2f, Gold = 20, SpawnsToElite = -1 };
        var cheap = new RequestSnapshot { Kind = RequestKind.UseSpecial, ApprovalYes = 2f, Gold = 300, SpawnsToElite = -1, ThreatPresent = true };
        Assert.That(AiAbilityRules.AnswerRequest(RequestEvaluator.Evaluate(bad)), Is.EqualTo(AiAbilityRules.AnswerNo));
        Assert.That(AiAbilityRules.AnswerRequest(RequestEvaluator.Evaluate(cheap)), Is.EqualTo(AiAbilityRules.AnswerYes));
    }

    [Test]
    public void ReportCountsRequestsByKindAndTrait()
    {
        var run = new AiRunRecord { badRequests = 2, badRequestsGranted = 1, paybacks = 1 };
        run.requestAnswers["MoreDraw: Yes"] = 1;
        run.requestAnswers["MoreDraw: No"] = 3;
        run.requestsByTrait["Greedy: No"] = 3;
        string md = AiReportFormatter.Markdown(new[] { run, new AiRunRecord() }, "now");
        Assert.That(md, Does.Contain("| Refused | 3 (75%) |"));
        Assert.That(md, Does.Contain("| Refusals proven right | 1 |"));
        Assert.That(md, Does.Contain("| Greedy | 0 | 3 |"));
    }
}
