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

    // GDD: D 0 / C 30 / B 50 / A 70 / S 90. Anything below C, negative included, is D.
    static readonly float[] RankThresholds = { float.NegativeInfinity, 30f, 50f, 70f, 90f };
    static readonly string[] RankNames = { "D", "C", "B", "A", "S" };

    public static float Clamp(float approval) => Mathf.Clamp(approval, MinApproval, MaxApproval);

    public static int RankFor(float approval)
    {
        int rank = 0;
        for (int i = 1; i <= MaxRank; i++)
            if (approval >= RankThresholds[i]) rank = i;
        return rank;
    }

    public static string RankName(int rank) => RankNames[Mathf.Clamp(rank, 0, MaxRank)];

    // GDD: 2 soldiers at D and C, 3 at B and A, 4 at S.
    public static int SoldierCount(int rank) => rank >= 4 ? 4 : rank >= 2 ? 3 : 2;

    // The ability's status chance is a flat value that rises with rank (GDD).
    public static float StatusChanceBonus(int rank) => 0.05f * Mathf.Clamp(rank, 0, MaxRank);

    // Approval makes Gold go further (extra reserve per coin drawn) and the special charge faster.
    // Only positive approval helps; a captain who dislikes the player isn't penalised for it here.
    public static float ReserveEfficiencyBonus(float approval) => Mathf.Clamp(approval, 0f, MaxApproval) / 250f;
    public static float ChargeRateMultiplier(float approval) => 1f + Mathf.Clamp(approval, 0f, MaxApproval) / 200f;

    public static bool HasWalkedOut(float bestThisRun, float current) => current <= bestThisRun - WalkoutDrop;
}
