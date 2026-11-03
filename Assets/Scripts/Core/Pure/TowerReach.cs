using System.Collections.Generic;

// A captain's reach: the average of what its equipped soldiers' classes reach, plus the captain's
// own bonus. Soldiers count whether or not they are alive, so it never flickers. A captain with
// no soldiers reaches as far as a Knight, so its special still has a range.
public static class TowerReach
{
    public const float UnarmedReach = 1.5f;

    public static float Range(IReadOnlyList<float> slotReaches, float bonus)
    {
        if (slotReaches == null || slotReaches.Count == 0) return UnarmedReach + bonus;
        float total = 0f;
        for (int i = 0; i < slotReaches.Count; i++) total += slotReaches[i];
        return total / slotReaches.Count + bonus;
    }
}
