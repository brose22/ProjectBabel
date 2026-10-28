using UnityEngine;

// Pure aura arithmetic, kept free of gameplay types so it can be unit tested.
public static class AuraMath
{
    // A buff that starts at full strength when the wave begins and falls linearly to zero over
    // fadeSeconds. A non-positive fadeSeconds means the buff never fades.
    public static float FadedStrength(float startStrength, float secondsSinceWaveStart, float fadeSeconds)
    {
        if (fadeSeconds <= 0f) return startStrength;
        return startStrength * Mathf.Clamp01(1f - secondsSinceWaveStart / fadeSeconds);
    }

    // Defense can never make soldiers fully immune; this caps the fraction of damage blocked.
    public const float MaxDefense = 0.9f;

    public static float DamageTakenMultiplier(float defense) => 1f - Mathf.Clamp(defense, 0f, MaxDefense);
}
