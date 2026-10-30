// How a captain's special charge is earned (all as fractions of a full meter), kept free of
// gameplay types so it can be unit tested. Soldier hits are the main source; the timer is a slow trickle.
public static class ChargeMath
{
    // The timer fills this much as fast as the captain's charge rate alone would.
    public const float TrickleMultiplier = 0.3f;
    // Dealing damage worth a whole enemy's health earns this much.
    public const float PerEnemyHealthDealt = 0.04f;
    // Flat bonuses on top of the damage charge.
    public const float WeaknessHitBonus = 0.004f;
    public const float KnockbackBonus = 0.004f;
    public const float AfflictedHitBonus = 0.003f;
    public const float KillBonus = 0.01f;
    public const float EliteKillBonus = 0.06f;
    public const float BossKillBonus = 0.12f;
    // Most one captain can earn from fighting in a single wave, so nothing can be farmed.
    public const float WaveCap = 0.6f;

    // Health actually removed counts, so overkill does not pay extra.
    public static float ForDamage(float healthRemoved, float maxHealth) =>
        maxHealth > 0f ? PerEnemyHealthDealt * healthRemoved / maxHealth : 0f;

    public static float ForKill(bool elite, bool boss) =>
        boss ? BossKillBonus : elite ? EliteKillBonus : KillBonus;
}
