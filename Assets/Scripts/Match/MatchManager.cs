using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Setup is the run setup screen, shown before a run's first match while no party is chosen. Map is
// the world map between matches, while the player picks the next city. Stop is a visit to a rest,
// shop or event picked on the map, in place of a match.
public enum MatchState { Setup, Map, Stop, Preparing, WaveActive, Won, Lost }

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
    public bool IsEliteMatch => City != null && City.kind == NodeKind.Elite;
    // The enemy base's boss once it has walked on; it stays set after it dies.
    public Enemy Boss { get; private set; }
    public bool IsBossAlive => Boss != null && Boss.IsTargetable;
    // The match data's waves, scaled for how far into the run this city is.
    List<WaveDefinition> waves;
    // Seconds until the next wave starts by itself, or below zero when waiting for the player.
    public float NextWaveCountdown { get; private set; } = -1f;
    public float GameSpeed { get; private set; } = 1f;

    public int Kills { get; private set; }
    public int Leaks { get; private set; }
    public int Casualties { get; private set; }
    // Global health restored by winning this match.
    public int HealthRestored { get; private set; }

    public bool IsOver => State is MatchState.Won or MatchState.Lost;
    public bool IsBetweenMatches => State is MatchState.Setup or MatchState.Map or MatchState.Stop;
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
        // Pick up a saved run if there is one, otherwise start fresh.
        if (RunState.Current == null && !RunSave.TryResume(data.roster))
            RunState.StartNewRun(data.startingHealth, data.startingGold);
        // A run without a party starts at the setup screen, then the world map until a city is picked.
        RunState run = RunState.Current;
        City = run.Map?.Target;
        State = !run.HasParty ? MatchState.Setup : City == null ? MatchState.Map : City.IsMatch ? MatchState.Preparing : MatchState.Stop;
        waves = ScaledWaves();
        if (CityLayout != null) grid.ApplyLayout(CityLayout);
        // A stop is saved with it picked, so quitting there (say after shopping) comes back to it.
        if (State is MatchState.Map or MatchState.Stop) RunSave.Save(run);
        Time.timeScale = GameSpeed;

        // Added at runtime so existing scenes need no rebuild.
        Camera cam = Camera.main;
        if (cam != null)
            cameraShake = cam.TryGetComponent(out CameraShake shake) ? shake : cam.gameObject.AddComponent<CameraShake>();
        if (!TryGetComponent(out Relationships _)) gameObject.AddComponent<Relationships>();
        if (!TryGetComponent(out CaptainRequests _)) gameObject.AddComponent<CaptainRequests>();
        if (!TryGetComponent(out RunScreens _)) gameObject.AddComponent<RunScreens>();
        if (!TryGetComponent(out MatchStats _)) gameObject.AddComponent<MatchStats>();
        // Testing cheats stay out of release builds.
        if (Debug.isDebugBuild && !TryGetComponent(out CheatWindow _)) gameObject.AddComponent<CheatWindow>();
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
            // After the last wave the base waits on the boss, which ends the match when it dies.
            if (WavesCleared < WaveCount && BossRules.IsWaveOver(spawner.IsSpawning, Enemy.Active.Count, IsBossAlive))
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
        if (IsBaseMatch && data.boss != null && Boss == null)
            Boss = spawner.SpawnOne(data.boss, RunScaling.HealthMultiplier(City.layer, data.enemyHealthGrowthPerMatch));
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
            if (BossRules.IsMatchWon(WavesCleared, WaveCount, IsBossAlive)) Finish(MatchState.Won);
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
        RunSave.Delete();
        RunState.StartNewRun(data.startingHealth, data.startingGold);
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Starts the run with the chosen captains and a fresh world map; called from the setup screen.
    public void BeginRun(IEnumerable<CaptainData> party)
    {
        if (State != MatchState.Setup) return;
        RunState.Current.SetParty(party);
        RunState.Current.SetMap(WorldMap.Generate(Random.Range(int.MinValue, int.MaxValue), data.matchesPerRun,
            data.layouts.Count, data.minCitiesPerLayer, data.maxCitiesPerLayer, data.intelDepth, data.nodeOdds));
        nextCaptainIndex = 0;
        State = MatchState.Map;
        RunSave.Save(RunState.Current);
    }

    // Takes an offered captain into the party (or none, for null) and saves the choice.
    public void Recruit(CaptainData captain)
    {
        RunState.Current.Recruit(captain);
        RunSave.Save(RunState.Current);
    }

    // A won city offers a few captains from outside the party to join, while there's room.
    // Elite cities offer a wider choice.
    void OfferRecruits()
    {
        RunState run = RunState.Current;
        if (run.ActivePartyCount >= data.maxPartySize) return;
        var candidates = data.roster.FindAll(c => c != null && !run.Party.Contains(c));
        var offer = new List<CaptainData>();
        int choices = data.recruitChoices + (IsEliteMatch ? Mathf.Max(0, data.eliteExtraRecruitChoices) : 0);
        while (offer.Count < choices && candidates.Count > 0)
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

    // ---------- Stops ----------

    public int RestAmount => RunHealing.RestAmount(RunState.Current.MaxHealth, data.restHealFraction);

    // Rests restore part of the run's health, then the player moves on.
    public void Rest()
    {
        if (State != MatchState.Stop || City.kind != NodeKind.Rest) return;
        RunState.Current.Heal(RestAmount);
        LeaveStop();
    }

    // Party members who can take a gift at the shop: everyone who hasn't walked out.
    public bool CanBuyGift(CaptainData captain) =>
        State == MatchState.Stop && City.kind == NodeKind.Shop && captain != null && RunState.Current.Party.Contains(captain)
        && !RunState.Current.HasDeparted(captain) && ApprovalStore.ApprovalOf(captain) < BondMath.MaxApproval
        && RunState.Current.Gold >= data.giftCost;

    // Buys a gift for a captain at the shop: Gold for approval. Saved straight away, since approval
    // is saved with the bonds as soon as it changes.
    public void BuyGift(CaptainData captain)
    {
        if (!CanBuyGift(captain) || !RunState.Current.TrySpendGold(data.giftCost)) return;
        ApprovalStore.Change(captain, data.giftApproval);
        ApprovalStore.Save();
        RunSave.Save(RunState.Current);
    }

    // The event at this event node, picked from the pool by the node's seed; null with no events.
    public EventData StopEvent =>
        State == MatchState.Stop && City.kind == NodeKind.Event && data.events.Count > 0 ? data.events[City.seed % data.events.Count] : null;

    public bool CanTakeEventChoice(EventChoice choice) =>
        StopEvent != null && choice != null && choice.CanTake(RunState.Current.Gold, RunState.Current.Health);

    // Applies an event choice to the run and moves on.
    public void TakeEventChoice(EventChoice choice)
    {
        if (!CanTakeEventChoice(choice)) return;
        RunState run = RunState.Current;
        if (choice.gold >= 0) run.AddGold(choice.gold);
        else run.TrySpendGold(-choice.gold);
        if (choice.health >= 0) run.Heal(choice.health);
        else run.TakeDamage(-choice.health);
        foreach (CaptainData captain in run.Party)
        {
            if (run.HasDeparted(captain)) continue;
            run.AddXp(captain, choice.xp);
            if (choice.approval != 0f) ApprovalStore.Change(captain, choice.approval);
        }
        if (choice.approval != 0f) ApprovalStore.Save();
        LeaveStop();
    }

    // Moves on from a rest, shop or event: it counts as visited and the map opens again.
    public void LeaveStop()
    {
        if (State != MatchState.Stop) return;
        RunState.Current.CompleteStop(data.intelDepth);
        RunSave.Save(RunState.Current);
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // After a win, back to the world map. Health, Gold, XP, the party and any walkouts carry over
    // because they live in RunState.
    public void ReturnToMap()
    {
        if (State != MatchState.Won) return;
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Copies the authored waves with more and tougher enemies further into the run. Elite cities
    // play a few layers tougher. The enemy base plays them baseWaveRepeats times over, each pass a
    // layer tougher.
    List<WaveDefinition> ScaledWaves()
    {
        int layer = RunScaling.DifficultyLayer(City, data.eliteLayerBonus);
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

    // Cheat: wins the match on the spot, as if every wave were cleared. Enemies on the field are
    // removed without paying Gold or XP.
    public bool CanCheatWin => State is MatchState.Preparing or MatchState.WaveActive;

    public void CheatWinMatch()
    {
        if (!CanCheatWin) return;
        foreach (Enemy enemy in Enemy.Active.ToArray()) Destroy(enemy.gameObject);
        WavesCleared = WaveCount;
        Finish(MatchState.Won);
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
        bool won = result == MatchState.Won;
        if (won) RunState.Current.CompleteMatch(data.intelDepth);
        // Walkouts are judged on this, so recruits are offered after it.
        GameEvents.RaiseMatchEnded(won);

        // The city falls as soon as the match is won and is saved straight away, so the win counts
        // even if the player quits here. A lost or finished run has nothing left to resume.
        bool runGoesOn = won && !RunState.Current.Map.IsComplete;
        if (runGoesOn)
        {
            int before = RunState.Current.Health;
            RunState.Current.Heal(data.healthPerWin);
            HealthRestored = RunState.Current.Health - before;
            OfferRecruits();
            RunSave.Save(RunState.Current);
        }
        else RunSave.Delete();
    }

    // The tower that landed the killing blow earns the XP; a destroyed (sold) tower earns nothing.
    // Elite cities pay extra Gold on top of what the kill already added.
    void OnEnemyKilled(Enemy enemy)
    {
        Kills++;
        if (enemy == Boss && !IsOver && BossRules.IsMatchWon(WavesCleared, WaveCount, false)) Finish(MatchState.Won);
        if (IsEliteMatch) RunState.Current.AddGold(Mathf.RoundToInt(enemy.Data.goldReward * (data.eliteGoldMultiplier - 1f)));
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
