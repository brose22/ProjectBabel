using System.Collections.Generic;
using UnityEngine;

// How much of the road a spot can reach, for judging whether a captain's move keeps it useful.
public static class RoadReach
{
    public static int CountInRange(Vector2 position, IReadOnlyList<Vector2> roadCenters, float range)
    {
        int count = 0;
        for (int i = 0; i < roadCenters.Count; i++)
            if (Vector2.Distance(position, roadCenters[i]) <= range) count++;
        return count;
    }

    // A new spot is acceptable when it reaches some road and at least as much as the current one.
    public static bool KeepsCoverage(int candidate, int current) => candidate > 0 && candidate >= current;
}
