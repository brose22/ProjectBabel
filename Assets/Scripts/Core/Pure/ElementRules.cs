// The element cycle (item: elements): Water beats Fire, Fire beats Ice, Ice beats Grass and Grass
// beats Water. The pairs across the cycle (Water and Ice, Fire and Grass) are neutral. Elements are
// indexed in the order of the Element enum (Water, Fire, Ice, Grass), so this stays free of game types.
public static class ElementRules
{
    public const int ElementCount = 4;
    public const float AdvantageMultiplier = 1.5f;
    public const float DisadvantageMultiplier = 0.75f;

    // True when the attacker's element beats the target's.
    public static bool Beats(int attacker, int target) =>
        IsElement(attacker) && IsElement(target) && (attacker + 1) % ElementCount == target;

    // Damage multiplier for an attacker's element against a target's; 1 for neutral pairs and anything unknown.
    public static float DamageMultiplier(int attacker, int target)
    {
        if (Beats(attacker, target)) return AdvantageMultiplier;
        if (Beats(target, attacker)) return DisadvantageMultiplier;
        return 1f;
    }

    // The element that beats this one (what a target of this element is weak to); -1 for none.
    public static int WeakTo(int target) => IsElement(target) ? (target + ElementCount - 1) % ElementCount : -1;

    static bool IsElement(int element) => element >= 0 && element < ElementCount;
}
