using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Setup is the run setup screen, shown before a run's first match while no party is chosen.
public enum MatchState { Setup, Preparing, WaveActive, Won, Lost }

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
    CameraShake cameraShake;

    // Game time the current wave started.
    public float WaveStartTime { get; private set; }

    // Roster index the player has cycled to; NextCaptain skips anyone already on the field.
    int nextCaptainIndex;

    // The captain the next placed tower gets: the chosen one, or the next one after it who
    // isn't already on the field.
    public CaptainData NextCaptain
    {
        get
        {
            List<CaptainData> party = RunState.Current.Party;
            int count = party.Count;
            for (int i = 0; i < count; i++)
            {
                CaptainData captain = party[(nextCaptainIndex + i) % count];
                if (captain != null && !Tower.All.Exists(t => t.Captain == captain) && !RunState.Current.HasDeparted(captain))
                    return captain;
            }
            return null;
        }
    }

    // Moves the choice to the next captain not yet on the field, wrapping around the roster.
    public void CycleNextCaptain()
    {
        CaptainData current = NextCaptain;
        if (current == null) return;
        List<CaptainData> party = RunState.Current.Party;
        nextCaptainIndex = (party.IndexOf(current) + 1) % party.Count;
    }

    public bool CanPlaceTower => !IsOver && Tower.All.Count < data.maxTowers && NextCaptain != null;
    // Relocation cost for this tower, after any discount a granted request gave it.
    public int MoveCost(Tower tower) => tower == null ? data.relocationCost : Mathf.RoundToInt(data.relocationCost * tower.MoveCostMultiplier);
    public bool CanAffordMove(Tower tower) => RunState.Current.Gold >= MoveCost(tower);

    void Awake()
    {
        if (RunState.Current == null)
            RunState.StartNewRun(data.startingHealth, data.startingGold);
        // A run without a party starts at the setup screen; later matches go straight to play.
        State = RunState.Current.HasParty ? MatchState.Preparing : MatchState.Setup;
        Time.timeScale = GameSpeed;

        // Added at runtime so existing scenes need no rebuild.
        Camera cam = Camera.main;
        if (cam != null)
            cameraShake = cam.TryGetComponent(out CameraShake shake) ? shake : cam.gameObject.AddComponent<CameraShake>();
        if (!TryGetComponent(out Relationships _)) gameObject.AddComponent<Relationships>();
        if (!TryGetComponent(out CaptainRequests _)) gameObject.AddComponent<CaptainRequests>();
        if (!TryGetComponent(out RunScreens _)) gameObject.AddComponent<RunScreens>();
        if (!TryGetComponent(out MatchStats _)) gameObject.AddComponent<MatchStats>();
    }

    void OnEnable()
    {
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.EnemyLeaked += OnEnemyLeaked;
        GameEvents.SoldierDied += OnSoldierDied;
        GameEvents.TowerDisabled += OnTowerDisabled;
        GameEvents.AbilityUsed += OnAbilityUsed;
        GameEvents.BondRankUp += OnBondRankUp;
    }

    void OnDisable()
    {
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.EnemyLeaked -= OnEnemyLeaked;
        GameEvents.SoldierDied -= OnSoldierDied;
        GameEvents.TowerDisabled -= OnTowerDisabled;
        GameEvents.AbilityUsed -= OnAbilityUsed;
        GameEvents.BondRankUp -= OnBondRankUp;
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
        WaveStartTime = Time.time;
        Tower.ClearWaveBoosts();
        spawner.SpawnWave(data.waves[WavesCleared]);
        GameEvents.RaiseWaveStarted(WavesCleared + 1);
    }

    void EndWave()
    {
        WavesCleared++;
        // Buffer specials last for the rest of the wave.
        Tower.ClearWaveBoosts();
        GameEvents.RaiseWaveEnded(WavesCleared);
        if (WavesCleared >= WaveCount)
        {
            Finish(MatchState.Won);
            return;
        }
        State = MatchState.Preparing;
        NextWaveCountdown = data.timeBetweenWaves;
    }

    // Bond rank sets how many soldiers a captain fields (GDD); soldiersPerTower is the floor.
    int SoldierCountFor(CaptainData captain) =>
        Mathf.Max(data.soldiersPerTower, BondMath.SoldierCount(ApprovalStore.RankOf(captain)));

    void OnBondRankUp(CaptainData captain)
    {
        Tower tower = Tower.All.Find(t => t.Captain == captain);
        if (tower != null) tower.GrowSoldierSlots(SoldierCountFor(captain), data.DefaultClassForSlot);
    }

    public Tower PlaceTower(Vector2Int cell)
    {
        if (!CanPlaceTower || !grid.IsBuildable(cell)) return null;
        Tower tower = Instantiate(towerPrefab, grid.CellCenter(cell), Quaternion.identity);
        CaptainData captain = NextCaptain;
        var slotClasses = new SoldierClassData[SoldierCountFor(captain)];
        for (int i = 0; i < slotClasses.Length; i++)
            slotClasses[i] = data.DefaultClassForSlot(i);
        tower.Init(this, captain, slotClasses, cell);
        grid.SetOccupant(cell, tower);
        GameEvents.RaiseTowerPlaced(tower);
        return tower;
    }

    public void SellTower(Tower tower)
    {
        grid.ClearOccupant(tower.Cell);
        tower.Sell();
    }

    public int RestoreCost(Tower tower) => GoldCosts.Scaled(data.restoreBaseCost, tower.Level);

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
        int cost = MoveCost(tower);
        if (cost > 0 && !RunState.Current.TrySpendGold(cost)) return false;
        tower.ClearMoveDiscount();
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

    // Ends the run: back to the setup screen with fresh health and Gold. Approval and bond ranks
    // are untouched, since they carry between runs.
    public void RestartRun()
    {
        RunState.StartNewRun(data.startingHealth, data.startingGold);
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Starts the run with the chosen captains; called from the setup screen.
    public void BeginRun(IEnumerable<CaptainData> party)
    {
        if (State != MatchState.Setup) return;
        RunState.Current.SetParty(party);
        nextCaptainIndex = 0;
        State = MatchState.Preparing;
    }

    // After a win, plays the next match of the run. Health, Gold, XP, the party and any walkouts
    // carry over because they live in RunState.
    public void NextMatch()
    {
        if (State != MatchState.Won) return;
        RunState.Current.CompleteMatch();
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Jolts the camera and freezes the match for a moment when an ability lands so the hit reads.
    // Overlapping abilities extend the freeze to the longest one rather than stacking.
    void OnAbilityUsed(Tower tower)
    {
        AbilityData ability = tower.Captain.ability;
        if (cameraShake != null) cameraShake.Shake(ability.cameraShake, ability.cameraShakeSeconds);

        float seconds = ability.hitStopSeconds;
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
        Tower.ClearWaveBoosts();
        Time.timeScale = 0f;
        GameEvents.RaiseMatchEnded(result == MatchState.Won);
    }

    // The tower that landed the killing blow earns the XP; a destroyed (sold) tower earns nothing.
    void OnEnemyKilled(Enemy enemy)
    {
        Kills++;
        if (enemy.KilledBy != null) enemy.KilledBy.GainXp(enemy.Data.xpReward);
    }

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
