using UnityEngine;

// Captain Defense: a flat reduction to each hit the tower and its soldiers take, applied after the
// advantage multipliers. Kept free of gameplay types so it can be unit tested.
public static class DefenseMath
{
    // Defense never takes a hit below this, so every hit still lands.
    public const float MinDamage = 1f;

    // Defense at a level: the base, plus the growth for each level above 1.
    public static float AtLevel(float baseDefense, float perLevel, int level) =>
        Mathf.Max(0f, baseDefense + perLevel * (Mathf.Max(1, level) - 1));

    // Damage left from a hit after Defense. A hit already below MinDamage keeps its own value.
    public static float DamageAfterDefense(float damage, float defense)
    {
        if (damage <= 0f) return 0f;
        return Mathf.Max(damage - Mathf.Max(0f, defense), Mathf.Min(damage, MinDamage));
    }
}
