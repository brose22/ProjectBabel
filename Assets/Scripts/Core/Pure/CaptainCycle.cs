using System.Collections.Generic;

// Stepping through the party with the match's captain cycler. Empty slots are skipped and the
// ends wrap around, so the wheel or Q/E always lands on a captain while there is one.
public static class CaptainCycle
{
    // The slot step captains away from index (negative steps go back), skipping empty slots. From
    // an empty or out-of-range index, forward starts before the first slot and back after the last,
    // and a zero step finds the first captain. -1 when the party is empty.
    public static int Step<T>(IReadOnlyList<T> party, int index, int step) where T : class
    {
        int count = party.Count;
        bool onCaptain = index >= 0 && index < count && party[index] != null;
        if (onCaptain && step == 0) return index;
        int direction = step < 0 ? -1 : 1;
        int remaining = step == 0 ? 1 : System.Math.Abs(step);
        int current = onCaptain ? index : direction > 0 ? -1 : count;
        for (int tries = 0; tries < count * remaining; tries++)
        {
            current = ((current + direction) % count + count) % count;
            if (party[current] != null && --remaining == 0) return current;
        }
        return -1;
    }

    // The captains either side of index, for the cycler's previous and next slots: -1 for a side
    // that would only repeat the selected captain (or, with two captains, the other side).
    public static (int previous, int next) Neighbours<T>(IReadOnlyList<T> party, int index) where T : class
    {
        int present = 0;
        foreach (T captain in party) if (captain != null) present++;
        if (present < 2 || index < 0 || index >= party.Count || party[index] == null) return (-1, -1);
        return (present > 2 ? Step(party, index, -1) : -1, Step(party, index, 1));
    }
}
