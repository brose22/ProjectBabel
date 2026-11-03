using System;
using System.Collections.Generic;

// A roster captain as the AI sees it. Element is the Element enum's value (Water, Fire, Ice, Grass).
public struct AiCaptainInfo
{
    public string name;
    public CaptainRole role;
    // Whether the captain's ability heals or buffs rather than hitting enemies.
    public bool support;
    public int element;
    // Damage per second at level 1: power x speed.
    public float strength;
    public float range;
}

// Chooses the starting party, recruits and the order captains take the field in.
public static class AiPartyPicker
{
    // Higher is better.
    static float Score(AiCaptainInfo captain, IReadOnlyList<AiCaptainInfo> party, int slotsLeft)
    {
        float score = captain.strength + captain.range;
        int supports = 0;
        int sameElement = 0;
        foreach (AiCaptainInfo member in party)
        {
            if (member.support) supports++;
            if (member.element == captain.element) sameElement++;
        }
        if (!captain.support)
        {
            // The last slot has to go to a support if the party still has none.
            if (supports == 0 && slotsLeft <= 1) score -= 100f;
        }
        else
        {
            // One support is worth a slot; a second costs an attacker's damage.
            score += supports == 0 ? 12f : supports == 1 ? -10f : -30f;
        }
        score -= sameElement * 3f;
        return score;
    }

    // The indices of count captains from the roster, best first.
    public static List<int> PickParty(IReadOnlyList<AiCaptainInfo> roster, int count)
    {
        var chosen = new List<int>();
        var members = new List<AiCaptainInfo>();
        while (chosen.Count < count && chosen.Count < roster.Count)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < roster.Count; i++)
            {
                if (chosen.Contains(i)) continue;
                float score = Score(roster[i], members, count - chosen.Count);
                if (score <= bestScore) continue;
                best = i;
                bestScore = score;
            }
            chosen.Add(best);
            members.Add(roster[best]);
        }
        return chosen;
    }

    // The index in offer to recruit, or -1 to pass.
    public static int PickRecruit(IReadOnlyList<AiCaptainInfo> offer, IReadOnlyList<AiCaptainInfo> party)
    {
        int best = -1;
        float bestScore = 0f;
        for (int i = 0; i < offer.Count; i++)
        {
            float score = Score(offer[i], party, 2);
            if (score <= bestScore) continue;
            best = i;
            bestScore = score;
        }
        return best;
    }

    // The order to place party members in when the field is smaller than the party: the strongest
    // attacker, a support (if any), then the rest by strength.
    public static List<int> FieldOrder(IReadOnlyList<AiCaptainInfo> party)
    {
        var order = new List<int>();
        for (int i = 0; i < party.Count; i++) order.Add(i);
        order.Sort((a, b) => party[b].strength.CompareTo(party[a].strength));
        int firstAttacker = order.FindIndex(i => !party[i].support);
        int firstSupport = order.FindIndex(i => party[i].support);
        var result = new List<int>();
        if (firstAttacker >= 0) result.Add(order[firstAttacker]);
        if (firstSupport >= 0) result.Add(order[firstSupport]);
        foreach (int i in order)
            if (!result.Contains(i)) result.Add(i);
        return result;
    }
}

