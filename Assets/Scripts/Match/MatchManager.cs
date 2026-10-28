using UnityEngine;
using UnityEngine.SceneManagement;

public enum MatchState { Preparing, WaveActive, Won, Lost }

// Runs one match: wave flow, tower placement, and the win/lose result.
public class MatchManager : MonoBehaviour
{
    [SerializeField] MatchData data;
    [SerializeField] GridMap grid;
    [SerializeField] WaveSpawner spawner;
    [SerializeField] Tower towerPrefab;

    public MatchData Data => data;
    public MatchState State { get; private set; } = MatchState.Preparing;
    public int WavesCleared { get; private set; }
    public int WaveCount => data.waves.Count;
    // Seconds until the next wave starts by itself, or below zero when waiting for the player.
    public float NextWaveCountdown { get; private set; } = -1f;
    public float GameSpeed { get; private set; } = 1f;

    public int Kills { get; private set; }
    public int Leaks { get; private set; }
    public int Casualties { get; private set; }

    public bool IsOver => State is MatchState.Won or MatchState.Lost;
    public bool IsHitStopped => hitStopRemaining > 0f;

    // Real-time seconds left on the current ability hit-stop.
    float hitStopRemaining;

    // The first roster captain who isn't already on the field.
    public CaptainData NextCaptain
    {
        get
        {
            foreach (CaptainData captain in data.roster)
                if (!Tower.All.Exists(t => t.Captain == captain))
                    return captain;
            return null;
        }
    }

    public bool CanPlaceTower => !IsOver && Tower.All.Count < data.maxTowers && NextCaptain != null;
    public bool CanAffordMove => RunState.Current.Gold >= data.relocationCost;

    void Awake()
    {
        if (RunState.Current == null)
            RunState.StartNewRun(data.startingHealth, data.startingGold);
        Time.timeScale = GameSpeed;
    }

    void OnEnable()
    {
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.EnemyLeaked += OnEnemyLeaked;
        GameEvents.SoldierDied += OnSoldierDied;
        GameEvents.TowerDisabled += OnTowerDisabled;
        GameEvents.AbilityUsed += OnAbilityUsed;
    }

    void OnDisable()
    {
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.EnemyLeaked -= OnEnemyLeaked;
        GameEvents.SoldierDied -= OnSoldierDied;
        GameEvents.TowerDisabled -= OnTowerDisabled;
        GameEvents.AbilityUsed -= OnAbilityUsed;
    }

    void OnDestroy() => Time.timeScale = 1f;

    void Update()
    {
        TickHitStop();

        if (State == MatchState.WaveActive)
        {
            if (!spawner.IsSpawning && Enemy.Active.Count == 0)
                EndWave();
        }
        else if (State == MatchState.Preparing && NextWaveCountdown > 0f)
        {
            NextWaveCountdown -= Time.deltaTime;
            if (NextWaveCountdown <= 0f) StartNextWave();
        }
    }

    public void StartNextWave()
    {
        if (State != MatchState.Preparing) return;
        NextWaveCountdown = -1f;
        State = MatchState.WaveActive;
        spawner.SpawnWave(data.waves[WavesCleared]);
        GameEvents.RaiseWaveStarted(WavesCleared + 1);
    }

    void EndWave()
    {
        WavesCleared++;
        GameEvents.RaiseWaveEnded(WavesCleared);
        if (WavesCleared >= WaveCount)
        {
            Finish(MatchState.Won);
            return;
        }
        State = MatchState.Preparing;
        NextWaveCountdown = data.timeBetweenWaves;
    }

    public Tower PlaceTower(Vector2Int cell)
    {
        if (!CanPlaceTower || !grid.IsBuildable(cell)) return null;
        Tower tower = Instantiate(towerPrefab, grid.CellCenter(cell), Quaternion.identity);
        var slotClasses = new SoldierClassData[data.soldiersPerTower];
        for (int i = 0; i < slotClasses.Length; i++)
            slotClasses[i] = data.DefaultClassForSlot(i);
        tower.Init(NextCaptain, slotClasses, cell);
        grid.SetOccupant(cell, tower);
        GameEvents.RaiseTowerPlaced(tower);
        return tower;
    }

    public void SellTower(Tower tower)
    {
        grid.ClearOccupant(tower.Cell);
        tower.Sell();
    }

    public int RestoreCost(Tower tower) => GoldCosts.Scaled(data.restoreBaseCost, tower.Captain.level);

    public bool CanRestore(Tower tower) =>
        !IsOver && tower.IsDisabled && WavesCleared >= tower.RestorableAfterWave;

    public bool TryRestoreTower(Tower tower)
    {
        if (!CanRestore(tower) || !RunState.Current.TrySpendGold(RestoreCost(tower))) return false;
        tower.Restore();
        return true;
    }

    // Relocates a tower for Gold. It keeps its reserve and soldiers. A disabled tower stays put
    // until it is restored.
    public bool CanMove(Tower tower) => !IsOver && tower != null && !tower.IsDisabled && Tower.All.Contains(tower);

    public bool MoveTower(Tower tower, Vector2Int cell)
    {
        if (!CanMove(tower) || !grid.IsBuildable(cell)) return false;
        if (data.relocationCost > 0 && !RunState.Current.TrySpendGold(data.relocationCost)) return false;
        grid.ClearOccupant(tower.Cell);
        grid.SetOccupant(cell, tower);
        tower.MoveTo(cell, grid.CellCenter(cell));
        GameEvents.RaiseTowerMoved(tower);
        return true;
    }

    public void ToggleGameSpeed()
    {
        GameSpeed = GameSpeed > 1f ? 1f : 2f;
        if (!IsOver && !IsHitStopped) Time.timeScale = GameSpeed;
    }

    public void RestartRun()
    {
        RunState.StartNewRun(data.startingHealth, data.startingGold);
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Freezes the match for a moment when an ability lands so the hit reads. Overlapping
    // abilities extend the freeze to the longest one rather than stacking.
    void OnAbilityUsed(Tower tower)
    {
        float seconds = tower.Captain.ability.hitStopSeconds;
        if (seconds <= 0f || IsOver) return;
        hitStopRemaining = Mathf.Max(hitStopRemaining, seconds);
        Time.timeScale = 0f;
    }

    // Counts in unscaled time, since the freeze itself sets timeScale to 0.
    void TickHitStop()
    {
        if (hitStopRemaining <= 0f) return;
        hitStopRemaining -= Time.unscaledDeltaTime;
        if (hitStopRemaining <= 0f && !IsOver) Time.timeScale = GameSpeed;
    }

    void Finish(MatchState result)
    {
        State = result;
        spawner.StopSpawning();
        Time.timeScale = 0f;
    }

    void OnEnemyKilled(Enemy enemy) => Kills++;

    void OnSoldierDied(Soldier soldier) => Casualties++;

    // A disabled tower sits out the whole of the next wave. Disabled during wave N: restorable
    // once wave N+1 ends. Disabled between waves (after wave N ended): also once wave N+1 ends.
    void OnTowerDisabled(Tower tower) =>
        tower.SetRestorableAfterWave(WavesCleared + (State == MatchState.WaveActive ? 2 : 1));

    void OnEnemyLeaked(Enemy enemy)
    {
        Leaks++;
        if (!IsOver && RunState.Current.IsDefeated)
            Finish(MatchState.Lost);
    }
}
