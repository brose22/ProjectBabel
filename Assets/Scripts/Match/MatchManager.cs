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
    }

    void OnDisable()
    {
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.EnemyLeaked -= OnEnemyLeaked;
        GameEvents.SoldierDied -= OnSoldierDied;
    }

    void OnDestroy() => Time.timeScale = 1f;

    void Update()
    {
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
        tower.Init(NextCaptain, data.defaultSoldierClass, data.soldiersPerTower, cell);
        grid.SetOccupant(cell, tower);
        GameEvents.RaiseTowerPlaced(tower);
        return tower;
    }

    public void SellTower(Tower tower)
    {
        grid.ClearOccupant(tower.Cell);
        tower.Sell();
    }

    public void ToggleGameSpeed()
    {
        GameSpeed = GameSpeed > 1f ? 1f : 2f;
        if (!IsOver) Time.timeScale = GameSpeed;
    }

    public void RestartRun()
    {
        RunState.StartNewRun(data.startingHealth, data.startingGold);
        SceneManager.LoadScene(gameObject.scene.name);
    }

    void Finish(MatchState result)
    {
        State = result;
        spawner.StopSpawning();
        Time.timeScale = 0f;
    }

    void OnEnemyKilled(Enemy enemy) => Kills++;

    void OnSoldierDied(Soldier soldier) => Casualties++;

    void OnEnemyLeaked(Enemy enemy)
    {
        Leaks++;
        if (!IsOver && RunState.Current.IsDefeated)
            Finish(MatchState.Lost);
    }
}
