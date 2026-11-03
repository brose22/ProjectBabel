using System.Collections.Generic;

// What a lost run costs the profile: a share of its Gold, and every party captain still in the
// roster, with the soldiers equipped to them. A won run opens the next location (LocationRules).
public static class RunEndRules
{
    public const int LossGoldPercent = 20;

    // The Gold a lost run takes, rounded down.
    public static int GoldLost(int gold) => gold <= 0 ? 0 : gold * LossGoldPercent / 100;

    // The party captains a lost run takes from the roster: those still in it, so walkouts aren't
    // counted twice.
    public static List<T> CaptainsLost<T>(IEnumerable<T> party, ICollection<T> roster)
    {
        var lost = new List<T>();
        foreach (T captain in party)
            if (captain != null && roster.Contains(captain) && !lost.Contains(captain)) lost.Add(captain);
        return lost;
    }
}

// Who is in the roster when a captain joins during a run (item: roster). With room the newcomer
// just joins. A full roster makes the player release one first: either the newcomer, who then
// doesn't join, or a captain already there.
public static class RosterRules
{
    public static bool NeedsRelease(int rosterCount, int maxRoster) => rosterCount >= maxRoster;

    // Adds the newcomer, first releasing the released captain when the roster is full. Returns
    // whether the newcomer joined; released captains leave the roster either way.
    public static bool Join<T>(List<T> roster, T newcomer, T released, int maxRoster) where T : class
    {
        if (newcomer == null || roster.Contains(newcomer) || released == newcomer) return false;
        if (released != null && NeedsRelease(roster.Count, maxRoster)) roster.Remove(released);
        if (NeedsRelease(roster.Count, maxRoster)) return false;
        roster.Add(newcomer);
        return true;
    }
}
