using System.Collections.Generic;
using UnityEngine;

// State that lasts for a whole run (about 30 matches): global health, the shared Gold pool and
// each captain's XP (levels reset each run, GDD). Lives outside any scene so it survives
// loading the next match.
public class RunState
{
    public static RunState Current { get; private set; }

    readonly Dictionary<CaptainData, int> captainXp = new();

    public int Health { get; private set; }
    public int MaxHealth { get; private set; }
    public int Gold { get; private set; }
    public bool IsDefeated => Health <= 0;

    public static RunState StartNewRun(int health, int gold)
    {
        Current = new RunState { Health = health, MaxHealth = health, Gold = gold };
        return Current;
    }

    public void AddGold(int amount) => Gold += Mathf.Max(0, amount);

    public bool TrySpendGold(int amount)
    {
        if (amount > Gold) return false;
        Gold -= amount;
        return true;
    }

    public void TakeDamage(int amount) => Health = Mathf.Max(0, Health - amount);

    public int XpOf(CaptainData captain) => captainXp.TryGetValue(captain, out int xp) ? xp : 0;

    public void AddXp(CaptainData captain, int amount)
    {
        if (amount > 0) captainXp[captain] = XpOf(captain) + amount;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;
}
