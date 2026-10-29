using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Setup is the run setup screen, shown before a run's first match while no party is chosen. Map is
// the world map between matches, while the player picks the next city.
public enum MatchState { Setup, Map, Preparing, WaveActive, Won, Lost }

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
    public int WaveCount => waves.Count;
    // The city being played, or null on the setup and map screens.
    public MapNode City { get; private set; }
    public CityLayout CityLayout => City != null && data.layouts.Count > 0 ? data.layouts[City.layout % data.layouts.Count] : null;
    public bool IsBaseMatch => City != null && RunState.Current.Map != null && City.id == RunState.Current.Map.BaseId;
    // The match data's waves, scaled for how far into the run this city is.
    List<WaveDefinition> waves;
    // Seconds until the next wave starts by itself, or below zero when waiting for the player.
    public float NextWaveCountdown { get; private set; } = -1f;
    public float GameSpeed { get; private set; } = 1f;

    public int Kills { get; private set; }
    public int Leaks { get; private set; }
    public int Casualties { get; private set; }

    public bool IsOver => State is MatchState.Won or MatchState.Lost;
    public bool IsBetweenMatches => State is MatchState.Setup or MatchState.Map;
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
        // A run without a party starts at the setup screen, then the world map until a city is picked.
        RunState run = RunState.Current;
        City = run.Map?.Target;
        State = !run.HasParty ? MatchState.Setup : City == null ? MatchState.Map : MatchState.Preparing;
        waves = ScaledWaves();
        if (CityLayout != null) grid.ApplyLayout(CityLayout);
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
        spawner.SpawnWave(waves[WavesCleared]);
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

    // Starts the run with the chosen captains and a fresh world map; called from the setup screen.
    public void BeginRun(IEnumerable<CaptainData> party)
    {
        if (State != MatchState.Setup) return;
        RunState.Current.SetParty(party);
        RunState.Current.SetMap(WorldMap.Generate(Random.Range(int.MinValue, int.MaxValue), data.matchesPerRun,
            data.layouts.Count, data.minCitiesPerLayer, data.maxCitiesPerLayer, data.intelDepth));
        nextCaptainIndex = 0;
        State = MatchState.Map;
    }

    // A won city offers a few captains from outside the party to join, while there's room.
    void OfferRecruits()
    {
        RunState run = RunState.Current;
        if (run.ActivePartyCount >= data.maxPartySize) return;
        var candidates = data.roster.FindAll(c => c != null && !run.Party.Contains(c));
        var offer = new List<CaptainData>();
        while (offer.Count < data.recruitChoices && candidates.Count > 0)
        {
            int pick = Random.Range(0, candidates.Count);
            offer.Add(candidates[pick]);
            candidates.RemoveAt(pick);
        }
        run.OfferRecruits(offer);
    }

    // Plays the picked city: reloads the scene, which lays out that city's map and waves.
    public void ChooseCity(int id)
    {
        if (State != MatchState.Map || !RunState.Current.Map.TryChoose(id)) return;
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // After a win, back to the world map. Health, Gold, XP, the party and any walkouts carry over
    // because they live in RunState.
    public void ReturnToMap()
    {
        if (State != MatchState.Won) return;
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Copies the authored waves with more and tougher enemies further into the run. The enemy base
    // plays them baseWaveRepeats times over, each pass a layer tougher.
    List<WaveDefinition> ScaledWaves()
    {
        int layer = City != null ? City.layer : 0;
        int repeats = IsBaseMatch ? Mathf.Max(1, data.baseWaveRepeats) : 1;
        var scaled = new List<WaveDefinition>();
        for (int pass = 0; pass < repeats; pass++)
        {
            float health = RunScaling.HealthMultiplier(layer + pass, data.enemyHealthGrowthPerMatch);
            foreach (WaveDefinition wave in data.waves)
            {
                var copy = new WaveDefinition { healthMultiplier = wave.healthMultiplier * health };
                foreach (SpawnGroup group in wave.groups)
                    copy.groups.Add(new SpawnGroup
                    {
                        enemy = group.enemy,
                        count = RunScaling.EnemyCount(group.count, layer + pass, data.enemyCountGrowthPerMatch),
                        spawnInterval = group.spawnInterval,
                        delayBefore = group.delayBefore,
                    });
                scaled.Add(copy);
            }
        }
        return scaled;
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
        // The city falls as soon as the match is won, so the win counts even if the player quits here.
        if (result == MatchState.Won)
        {
            RunState.Current.CompleteMatch(data.intelDepth);
            if (!RunState.Current.Map.IsComplete) OfferRecruits();
        }
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
