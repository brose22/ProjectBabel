using UnityEngine;

// The four element statuses (GDD): Water slows movement, Fire burns over time, Earth stuns
// (slower attacks), Air makes the enemy dizzy (walks the wrong way).
public enum StatusKind { Slow, Burn, Stun, Dizzy }

public static class StatusRules
{
    public const int KindCount = 4;

    public const float SlowSpeedMultiplier = 0.5f;
    // Burn damage per second, as a fraction of the caster's power.
    public const float BurnPowerPerSecond = 0.3f;
    public const float StunAttackIntervalMultiplier = 2f;

    // An enemy weak to the attacker's element takes this much more damage (GDD: a weakness
    // raises both damage taken and status chance).
    public const float WeaknessDamageMultiplier = 1.5f;
    public const float WeaknessStatusChanceMultiplier = 2f;

    public static StatusKind ForElement(Element element) => element switch
    {
        Element.Water => StatusKind.Slow,
        Element.Fire => StatusKind.Burn,
        Element.Earth => StatusKind.Stun,
        _ => StatusKind.Dizzy,
    };

    public static float DurationOf(StatusKind kind) => kind switch
    {
        StatusKind.Dizzy => 1.5f,
        _ => 3f,
    };

    public static Color TintOf(StatusKind kind) => kind switch
    {
        StatusKind.Slow => new Color(0.3f, 0.6f, 1f),
        StatusKind.Burn => new Color(1f, 0.55f, 0.1f),
        StatusKind.Stun => new Color(0.6f, 0.45f, 0.25f),
        _ => new Color(0.85f, 0.95f, 1f),
    };
}
