using UnityEngine;

// Captain level and XP arithmetic, kept free of gameplay types so it can be unit tested.
public static class LevelMath
{
    public const int MaxLevel = 10;

    // Total XP needed to reach a level. Each level costs 50 more than the one before:
    // level 2 at 50, level 3 at 150, level 4 at 300, up to level 10 at 2250.
    public static int XpForLevel(int level) => level <= 1 ? 0 : 25 * level * (level - 1);

    public static int LevelForXp(int xp)
    {
        int level = 1;
        while (level < MaxLevel && xp >= XpForLevel(level + 1)) level++;
        return level;
    }

    // Multiplies soldier power and health: +10% per level above 1.
    public static float StatFactor(int level) => 1f + 0.1f * (Mathf.Max(1, level) - 1);
}
