// The enemy base's boss walks the whole match: it spawns with the first wave, doesn't hold up the
// waves behind it, and the base falls only once every wave is cleared and the boss is dead.
public static class BossRules
{
    // A wave is over when nothing is left to spawn and only the boss, if anyone, is still walking.
    public static bool IsWaveOver(bool spawning, int activeEnemies, bool bossAlive) =>
        !spawning && activeEnemies - (bossAlive ? 1 : 0) <= 0;

    public static bool IsMatchWon(int wavesCleared, int waveCount, bool bossAlive) => wavesCleared >= waveCount && !bossAlive;
}
