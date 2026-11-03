using UnityEngine;

// Each captain's approval and best bond rank, kept on the Captain itself so they save with it.
// This applies the rules every change goes through and raises the events.
public static class ApprovalStore
{
    public static float ApprovalOf(Captain captain) => captain != null ? captain.approval : 0f;

    public static int RankOf(Captain captain) =>
        captain != null ? Mathf.Max(captain.bondRank, BondMath.RankFor(captain.approval)) : 0;

    // Applies a change, clamped to the approval range, and returns the change actually made.
    // Gains shrink with the captain's current rank, and during a run approval can't climb past
    // the rank after the one the captain started with. Raises ApprovalChanged, and BondRankUp
    // when the change reaches a new rank.
    public static float Change(Captain captain, float delta)
    {
        if (captain == null) return 0f;
        float before = captain.approval;
        int rankBefore = RankOf(captain);
        if (delta > 0f)
        {
            delta *= BondMath.GainMultiplier(BondMath.RankFor(before));
            if (RunState.Current != null)
                delta = Mathf.Min(delta, Mathf.Max(0f, BondMath.RunApprovalCap(RunState.Current.StartRankOf(captain)) - before));
        }
        else if (RunState.Current != null) RunState.Current.StartRankOf(captain);
        captain.approval = BondMath.Clamp(before + delta);
        captain.bondRank = Mathf.Max(captain.bondRank, BondMath.RankFor(captain.approval));
        float applied = captain.approval - before;
        if (applied != 0f) GameEvents.RaiseApprovalChanged(captain);
        if (RankOf(captain) > rankBefore) GameEvents.RaiseBondRankUp(captain);
        return applied;
    }

    // Puts a captain's bond at exactly the given rank, outside the limits that gains work under.
    // For starting an AI run at a chosen bond.
    public static void SetRank(Captain captain, int rank)
    {
        if (captain == null) return;
        captain.approval = BondMath.ApprovalForRank(rank);
        captain.bondRank = Mathf.Clamp(rank, 0, BondMath.MaxRank);
    }
}
