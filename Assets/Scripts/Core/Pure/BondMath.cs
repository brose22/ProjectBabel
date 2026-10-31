using UnityEngine;

// Approval and bond-rank rules (GDD), kept free of gameplay types so they can be unit tested.
// Rank index 0 is D, up to 4 for S.
public static class BondMath
{
    public const float MinApproval = -100f;
    public const float MaxApproval = 100f;
    public const int MaxRank = 4;
    // A captain whose approval falls this far below its best this run leaves for the rest of the run.
    public const float WalkoutDrop = 20f;

    // Slower than the GDD's 30 / 50 / 70 / 90: C 40 / B 65 / A 85 / S 100. Anything below C,
    // negative included, is D. S needs the full 100.
    static readonly float[] RankThresholds = { float.NegativeInfinity, 40f, 65f, 85f, 100f };
    // Approval gains shrink by this much per rank, so the climb slows as the bond deepens.
    const float GainFalloffPerRank = 0.15f;
    // A captain stops this far short of the rank two above the one it started the run with.
    const float RunRankCapMargin = 1f;
    static readonly string[] RankNames = { "D", "C", "B", "A", "S" };

    public static float Clamp(float approval) => Mathf.Clamp(approval, MinApproval, MaxApproval);

    public static int RankFor(float approval)
    {
        int rank = 0;
        for (int i = 1; i <= MaxRank; i++)
            if (approval >= RankThresholds[i]) rank = i;
        return rank;
    }

    // Multiplier on approval gains at a rank: 1 at D, falling to 0.4 at S.
    public static float GainMultiplier(int rank) => 1f - GainFalloffPerRank * Mathf.Clamp(rank, 0, MaxRank);

    // The most approval a captain can reach in one run: a rank can rise by at most 1 per run, so
    // approval stops just short of the rank after next.
    public static float RunApprovalCap(int startRank)
    {
        int blocked = Mathf.Clamp(startRank, 0, MaxRank) + 2;
        return blocked > MaxRank ? MaxApproval : RankThresholds[blocked] - RunRankCapMargin;
    }

    public static string RankName(int rank) => RankNames[Mathf.Clamp(rank, 0, MaxRank)];

    // 2 soldiers at D, 3 at C and B, 4 at A and S. Shifted a rank down from the GDD's 2 / 2 / 3 / 3 / 4,
    // since a captain can only rise one rank in a run and would otherwise wait several runs for a third soldier.
    public static int SoldierCount(int rank) => rank >= 3 ? 4 : rank >= 1 ? 3 : 2;

    // The ability's status chance is a flat value that rises with rank (GDD).
    public static float StatusChanceBonus(int rank) => 0.05f * Mathf.Clamp(rank, 0, MaxRank);

    // Approval makes Gold go further (extra reserve per coin drawn) and the special charge faster.
    // Only positive approval helps; a captain who dislikes the player isn't penalised for it here.
    public static float ReserveEfficiencyBonus(float approval) => Mathf.Clamp(approval, 0f, MaxApproval) / 250f;
    public static float ChargeRateMultiplier(float approval) => 1f + Mathf.Clamp(approval, 0f, MaxApproval) / 200f;

    // A flawless wave: the captain got kills, and nobody in the party lost a soldier or let an enemy through.
    public static bool IsFlawlessWave(int captainKills, int partyCasualties, int leaks) =>
        captainKills > 0 && partyCasualties == 0 && leaks == 0;

    public static bool HasWalkedOut(float bestThisRun, float current) => current <= bestThisRun - WalkoutDrop;
}
