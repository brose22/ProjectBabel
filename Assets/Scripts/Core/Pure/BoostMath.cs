using UnityEngine;

// Pure boost arithmetic, kept free of gameplay types so it can be unit tested.
public static class BoostMath
{
    // Defense can never make towers or soldiers fully immune; this caps the fraction of damage blocked.
    public const float MaxDefense = 0.9f;

    public static float DamageTakenMultiplier(float defense) => 1f - Mathf.Clamp(defense, 0f, MaxDefense);
}
