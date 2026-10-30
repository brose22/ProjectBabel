using UnityEngine;

// GDD: cost = class base x level factor. Every such Gold cost goes through here so the curve
// lives in one place.
public static class GoldCosts
{
    // Linear: level 1 pays the base, each further level adds a tenth (L2 = 1.1x, L3 = 1.2x), in step
    // with the 10% soldier power each level brings. Steeper, levelling made towers worse: costs
    // outran both power and the flat Gold draw into the reserve.
    public static float LevelFactor(int level) => 1f + 0.1f * (Mathf.Max(1, level) - 1);

    // The share of a tower's unspent reserve that returns to the shared pool when a match is won.
    public static int Refund(int reserve, float fraction) =>
        Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, reserve) * Mathf.Clamp01(fraction)));

    public static int Scaled(int baseCost, int level) =>
        Mathf.Max(0, Mathf.RoundToInt(baseCost * LevelFactor(level)));
}
