using System.Collections.Generic;

// What a lost run costs the profile: a share of its Gold, and every captain placed on the field in
// the losing match who is still in the roster, with the soldiers equipped to them. Party captains
// who were never deployed go home safely. A won run opens the next location (LocationRules).
public static class RunEndRules
{
    public const int LossGoldPercent = 20;

    // The Gold a lost run takes, rounded down.
    public static int GoldLost(int gold) => gold <= 0 ? 0 : gold * LossGoldPercent / 100;

    // The captains a lost run takes from the roster: those deployed in the losing match who are
    // still in it, so walkouts aren't counted twice.
    public static List<T> CaptainsLost<T>(IEnumerable<T> deployed, ICollection<T> roster)
    {
        var lost = new List<T>();
        foreach (T captain in deployed)
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

// Who is in the run's party (item: party). A captain who leaves, by walkout or release, is taken
// out of the party and the roster rather than kept as an inactive entry, so no list shows them and
// party counts and saves see the smaller party.
public static class PartyRules
{
    // Takes the captain out of the party and the roster. Returns whether they were in the party.
    public static bool Leave<T>(List<T> party, ICollection<T> roster, T captain) where T : class
    {
        if (captain == null) return false;
        roster?.Remove(captain);
        return party.Remove(captain);
    }
}
