using System.Collections.Generic;

// A captain's reach: the average of what the soldier classes in its slots reach, plus the
// captain's own bonus. Slots count whether or not their soldier is alive, so it never flickers.
public static class TowerReach
{
    public static float Range(IReadOnlyList<float> slotReaches, float bonus)
    {
        if (slotReaches == null || slotReaches.Count == 0) return bonus;
        float total = 0f;
        for (int i = 0; i < slotReaches.Count; i++) total += slotReaches[i];
        return total / slotReaches.Count + bonus;
    }
}
