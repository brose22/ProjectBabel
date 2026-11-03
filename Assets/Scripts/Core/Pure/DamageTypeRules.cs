// The damage-type triangle (item: damage types): Melee beats Range, Range beats Magic and Magic
// beats Melee. Ordered round the triangle so each type beats the next.
public enum DamageType { Melee, Range, Magic }

public static class DamageTypeRules
{
    public const int TypeCount = 3;

    // True when the attacker's damage type beats the target's.
    public static bool Beats(DamageType attacker, DamageType target) =>
        IsType(attacker) && IsType(target) && ((int)attacker + 1) % TypeCount == (int)target;

    // Damage multiplier for an attacker's damage type against a target's; 1 for the same type and anything unknown.
    public static float DamageMultiplier(DamageType attacker, DamageType target)
    {
        if (Beats(attacker, target)) return ElementRules.AdvantageMultiplier;
        if (Beats(target, attacker)) return ElementRules.DisadvantageMultiplier;
        return 1f;
    }

    // Both matchups stacked: damage type times element (elements by ElementRules index, -1 for
    // none), from x0.5625 when both are at a disadvantage to x2.25 when both have the advantage.
    public static float CombinedMultiplier(DamageType attackerType, int attackerElement, DamageType targetType, int targetElement) =>
        DamageMultiplier(attackerType, targetType) * ElementRules.DamageMultiplier(attackerElement, targetElement);

    static bool IsType(DamageType type) => type >= 0 && (int)type < TypeCount;
}
