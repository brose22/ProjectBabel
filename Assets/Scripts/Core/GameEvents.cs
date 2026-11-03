using System;
using UnityEngine;

// Match-wide event bus. Gameplay code raises these; later systems (approval, requests,
// captain likes/dislikes) listen to them without the raisers needing to know.
public static class GameEvents
{
    public static event Action<Enemy> EnemyKilled;
    public static event Action<Enemy> EnemyLeaked;
    // An elite or the boss has walked on.
    public static event Action<Enemy> EnemyArrived;
    public static event Action<Soldier> SoldierDied;
    public static event Action<Tower> TowerPlaced;
    public static event Action<Tower> TowerSold;
    public static event Action<Tower> AbilityUsed;
    // A tower's soldier, ability or burn hurt the boss.
    public static event Action<Tower> BossDamaged;
    // Gold or reserve was spent on the army: restoring a tower or retraining a soldier.
    public static event Action GoldSpent;
    // Damage an enemy took, after weakness: the tower whose soldier, ability or burn dealt it (null
    // when none did), the amount actually taken and the element it came as, if any.
    public static event Action<Tower, float, Element?> DamageDealt;
    public static event Action<Tower> DrawRateChanged;
    public static event Action<int> WaveStarted;
    public static event Action<int> WaveEnded;
    public static event Action<Tower> TowerDisabled;
    public static event Action<Tower> TowerRestored;
    // The tower and the level it had before.
    public static event Action<Tower, int> CaptainLeveledUp;
    public static event Action<Captain> ApprovalChanged;
    public static event Action<Captain> BondRankUp;
    // An approval change with its reason ("liked Kills") and the amount actually applied.
    public static event Action<ApprovalNotice> ApprovalNoticed;
    // True for a win, false for a loss.
    public static event Action<bool> MatchEnded;

    public static void RaiseEnemyKilled(Enemy enemy) => EnemyKilled?.Invoke(enemy);
    public static void RaiseEnemyLeaked(Enemy enemy) => EnemyLeaked?.Invoke(enemy);
    public static void RaiseEnemyArrived(Enemy enemy) => EnemyArrived?.Invoke(enemy);
    public static void RaiseSoldierDied(Soldier soldier) => SoldierDied?.Invoke(soldier);
    public static void RaiseTowerPlaced(Tower tower) => TowerPlaced?.Invoke(tower);
    public static void RaiseTowerSold(Tower tower) => TowerSold?.Invoke(tower);
    public static void RaiseAbilityUsed(Tower tower) => AbilityUsed?.Invoke(tower);
    public static void RaiseBossDamaged(Tower tower) => BossDamaged?.Invoke(tower);
    public static void RaiseGoldSpent() => GoldSpent?.Invoke();
    public static void RaiseDamageDealt(Tower source, float amount, Element? element) => DamageDealt?.Invoke(source, amount, element);
    public static void RaiseDrawRateChanged(Tower tower) => DrawRateChanged?.Invoke(tower);
    public static void RaiseWaveStarted(int waveNumber) => WaveStarted?.Invoke(waveNumber);
    public static void RaiseWaveEnded(int waveNumber) => WaveEnded?.Invoke(waveNumber);
    public static void RaiseTowerDisabled(Tower tower) => TowerDisabled?.Invoke(tower);
    public static void RaiseTowerRestored(Tower tower) => TowerRestored?.Invoke(tower);
    public static void RaiseCaptainLeveledUp(Tower tower, int previousLevel) => CaptainLeveledUp?.Invoke(tower, previousLevel);
    public static void RaiseApprovalChanged(Captain captain) => ApprovalChanged?.Invoke(captain);
    public static void RaiseBondRankUp(Captain captain) => BondRankUp?.Invoke(captain);
    public static void RaiseApprovalNoticed(ApprovalNotice notice) => ApprovalNoticed?.Invoke(notice);
    public static void RaiseMatchEnded(bool won) => MatchEnded?.Invoke(won);

    // Statics survive play sessions when domain reload is disabled, so clear them on entry.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        EnemyKilled = null;
        EnemyLeaked = null;
        EnemyArrived = null;
        SoldierDied = null;
        TowerPlaced = null;
        TowerSold = null;
        AbilityUsed = null;
        BossDamaged = null;
        GoldSpent = null;
        DamageDealt = null;
        DrawRateChanged = null;
        WaveStarted = null;
        WaveEnded = null;
        TowerDisabled = null;
        TowerRestored = null;
        CaptainLeveledUp = null;
        ApprovalChanged = null;
        BondRankUp = null;
        ApprovalNoticed = null;
        MatchEnded = null;
    }
}
