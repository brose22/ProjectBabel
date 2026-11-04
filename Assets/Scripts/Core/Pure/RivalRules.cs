using System;
using System.Collections.Generic;

// Two captains in the party who don't get on: granting a request for one costs the other approval.
public static class RivalRules
{
    // Approval the rival loses whenever a request is granted.
    public const float BaseCost = 1f;
    // Extra when the request brings the two together badly.
    public const float ExtraCost = 1f;
    // A rival whose reserve is below this share resents the other drawing more.
    public const float ShortReserveFraction = 0.5f;

    // How likely a pair is to be picked: different personalities clash most, a steady captain least.
    public static int ClashWeight(CaptainTrait a, CaptainTrait b)
    {
        if (a == CaptainTrait.Steady && b == CaptainTrait.Steady) return 1;
        if (a == CaptainTrait.Steady || b == CaptainTrait.Steady) return 2;
        return a == b ? 3 : 6;
    }

    // A pair of party indices, chosen at random weighted by clash, or null for a party under two.
    public static (int, int)? PickPair(IReadOnlyList<CaptainTrait> traits, Random random)
    {
        int total = 0;
        for (int i = 0; i < traits.Count; i++)
            for (int j = i + 1; j < traits.Count; j++) total += ClashWeight(traits[i], traits[j]);
        if (total == 0) return null;
        int roll = random.Next(total);
        for (int i = 0; i < traits.Count; i++)
        {
            for (int j = i + 1; j < traits.Count; j++)
            {
                roll -= ClashWeight(traits[i], traits[j]);
                if (roll < 0) return (i, j);
            }
        }
        return null;
    }

    // What granting this request costs the asker's rival. fallenIsRival: a Reinforce is moving in to cover
    // the rival. rivalReserveFraction: the rival's reserve share (1 when unknown).
    public static float Cost(RequestKind kind, bool fallenIsRival, float rivalReserveFraction)
    {
        float cost = BaseCost;
        if (kind == RequestKind.Reinforce && fallenIsRival) cost += ExtraCost;
        if (kind == RequestKind.MoreDraw && rivalReserveFraction < ShortReserveFraction) cost += ExtraCost;
        return cost;
    }
}
