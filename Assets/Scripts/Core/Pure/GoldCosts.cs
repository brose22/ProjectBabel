using UnityEngine;

// GDD: cost = class base x level factor. Every such Gold cost goes through here so the curve
// lives in one place.
public static class GoldCosts
{
    // Linear: level 1 pays the base, each further level adds half the base (L2 = 1.5x, L3 = 2x).
    public static float LevelFactor(int level) => 1f + 0.5f * (Mathf.Max(1, level) - 1);

    public static int Scaled(int baseCost, int level) =>
        Mathf.Max(0, Mathf.RoundToInt(baseCost * LevelFactor(level)));
}
