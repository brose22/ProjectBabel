using UnityEngine;

// The four element statuses (GDD): Water slows movement, Fire burns over time, Ice freezes the
// enemy in place, Grass tangles it in vines (stun: slower attacks).
public enum StatusKind { Slow, Burn, Stun, Freeze }

public static class StatusRules
{
    public const int KindCount = 4;

    public const float SlowSpeedMultiplier = 0.5f;
    // Burn damage per second, as a fraction of the caster's power.
    public const float BurnPowerPerSecond = 0.3f;
    public const float StunAttackIntervalMultiplier = 2f;

    public static StatusKind ForElement(Element element) => element switch
    {
        Element.Water => StatusKind.Slow,
        Element.Fire => StatusKind.Burn,
        Element.Grass => StatusKind.Stun,
        _ => StatusKind.Freeze,
    };

    public static float DurationOf(StatusKind kind) => kind switch
    {
        StatusKind.Freeze => 1.5f,
        _ => 3f,
    };

    public static Color TintOf(StatusKind kind) => kind switch
    {
        StatusKind.Slow => new Color(0.3f, 0.6f, 1f),
        StatusKind.Burn => new Color(1f, 0.55f, 0.1f),
        StatusKind.Stun => new Color(0.45f, 0.75f, 0.3f),
        _ => new Color(0.75f, 0.95f, 1f),
    };
}
