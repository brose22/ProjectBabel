using System;
using UnityEngine;

// Match-wide event bus. Gameplay code raises these; later systems (approval, requests,
// captain likes/dislikes) listen to them without the raisers needing to know.
public static class GameEvents
{
    public static event Action<Enemy> EnemyKilled;
    public static event Action<Enemy> EnemyLeaked;
    public static event Action<Soldier> SoldierDied;
    public static event Action<Tower> TowerPlaced;
    public static event Action<Tower> TowerMoved;
    public static event Action<Tower> TowerSold;
    public static event Action<Tower> AbilityUsed;
    public static event Action<Tower> DrawRateChanged;
    public static event Action<int> WaveStarted;
    public static event Action<int> WaveEnded;

    public static void RaiseEnemyKilled(Enemy enemy) => EnemyKilled?.Invoke(enemy);
    public static void RaiseEnemyLeaked(Enemy enemy) => EnemyLeaked?.Invoke(enemy);
    public static void RaiseSoldierDied(Soldier soldier) => SoldierDied?.Invoke(soldier);
    public static void RaiseTowerPlaced(Tower tower) => TowerPlaced?.Invoke(tower);
    public static void RaiseTowerMoved(Tower tower) => TowerMoved?.Invoke(tower);
    public static void RaiseTowerSold(Tower tower) => TowerSold?.Invoke(tower);
    public static void RaiseAbilityUsed(Tower tower) => AbilityUsed?.Invoke(tower);
    public static void RaiseDrawRateChanged(Tower tower) => DrawRateChanged?.Invoke(tower);
    public static void RaiseWaveStarted(int waveNumber) => WaveStarted?.Invoke(waveNumber);
    public static void RaiseWaveEnded(int waveNumber) => WaveEnded?.Invoke(waveNumber);

    // Statics survive play sessions when domain reload is disabled, so clear them on entry.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        EnemyKilled = null;
        EnemyLeaked = null;
        SoldierDied = null;
        TowerPlaced = null;
        TowerMoved = null;
        TowerSold = null;
        AbilityUsed = null;
        DrawRateChanged = null;
        WaveStarted = null;
        WaveEnded = null;
    }
}
