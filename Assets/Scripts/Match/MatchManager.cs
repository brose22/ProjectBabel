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
    // Gold the win paid into the pool: the flat bonus plus the towers' unspent reserves.
    public int WinGoldEarned { get; private set; }
    public int ReserveRefunded { get; private set; }
    // Gold the wave just cleared paid out.
    public int LastWaveClearGold { get; private set; }

    public bool IsOver => State is MatchState.Won or MatchState.Lost;
    public bool IsBetweenMatches => State is MatchState.Setup or MatchState.Map or MatchState.Stop;
    public bool IsHitStopped => hitStopRemaining > 0f;
    // Held while a captain's request waits for an answer.
    public bool IsRequestPaused { get; private set; }

    // Real-time seconds left on the current ability hit-stop.
    float hitStopRemaining;
    CameraShake cameraShake;
    CameraFocus cameraFocus;

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

    // Makes this captain the next one placed, if they aren't on the field already.
    public void ChooseNextCaptain(CaptainData captain)
    {
        int index = RunState.Current.Party.IndexOf(captain);
        if (index >= 0) nextCaptainIndex = index;
    }

    public bool CanPlaceTower => !IsOver && Tower.All.Count < data.maxTowers && NextCaptain != null;
    // Relocation cost for this tower, after any discount a granted request gave it.
    public int MoveCost(Tower tower) => tower == null ? data.relocationCost : Mathf.RoundToInt(data.relocationCost * tower.MoveCostMultiplier);
    public bool CanAffordMove(Tower tower) => RunState.Current.Gold >= MoveCost(tower);

    void Awake()
    {
        // Pick up a saved run if there is one, otherwise start fresh.
        if (RunState.Current == null && !RunSave.TryResume(data.roster, data.availableClasses, data.shopGifts))
            RunState.StartNewRun(data.startingHealth, data.startingGold);
        // A run without a party starts at the setup screen, then the world map until a city is picked.
        RunState run = RunState.Current;
        City = run.Map?.Target;
        State = !run.HasParty ? MatchState.Setup : City == null ? MatchState.Map : City.IsMatch ? MatchState.Preparing : MatchState.Stop;
        waves = ScaledWaves();
        if (CityLayout != null) grid.ApplyLayout(CityLayout);
        // A stop is saved with it picked, so quitting there (say after shopping) comes back to it.
        if (State is MatchState.Map or MatchState.Stop) RunSave.Save(run);
        ApplyTimeScale();

        // Added at runtime so existing scenes need no rebuild.
        Camera cam = Camera.main;
        if (cam != null)
        {
            cameraShake = cam.TryGetComponent(out CameraShake shake) ? shake : cam.gameObject.AddComponent<CameraShake>();
            cameraFocus = cam.TryGetComponent(out CameraFocus focus) ? focus : cam.gameObject.AddComponent<CameraFocus>();
            // A map of its own size opens centred, since it may not fit the screen.
            if (CityLayout != null && CityLayout.Area > 0)
                cameraShake.RestPosition = new Vector3(grid.Size.x * 0.5f, grid.Size.y * 0.5f, cameraShake.RestPosition.z);
            if (!cam.TryGetComponent(out CameraPan pan)) pan = cam.gameObject.AddComponent<CameraPan>();
            pan.Setup(grid);
        }
        if (!TryGetComponent(out Relationships _)) gameObject.AddComponent<Relationships>();
        if (!TryGetComponent(out CaptainRequests _)) gameObject.AddComponent<CaptainRequests>();
        if (!TryGetComponent(out RunScreens _)) gameObject.AddComponent<RunScreens>();
        if (!TryGetComponent(out MatchStats _)) gameObject.AddComponent<MatchStats>();
        if (!TryGetComponent(out ThreatAlerts _)) gameObject.AddComponent<ThreatAlerts>();
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
        {
            Boss = spawner.SpawnOne(data.boss, RunScaling.HealthMultiplier(City.layer, data.enemyHealthGrowthPerMatch),
                RunScaling.GoldMultiplier(City.layer, data.goldGrowthPerMatch));
            Boss.MarkBoss();
        }
        GameEvents.RaiseWaveStarted(WavesCleared + 1);
    }

    void EndWave()
    {
        WavesCleared++;
        // Buffer specials last for the rest of the wave.
        Tower.ClearWaveBoosts();
        LastWaveClearGold = RunScaling.WaveClearGold(data.waveClearGold, data.waveClearGoldPerWave, WavesCleared,
            RunScaling.DifficultyLayer(City, data.eliteLayerBonus), data.goldGrowthPerMatch);
        RunState.Current.AddGold(LastWaveClearGold);
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
    public int SoldierCountFor(CaptainData captain) =>
        Mathf.Max(data.soldiersPerTower, BondMath.SoldierCount(ApprovalStore.RankOf(captain)));

    void OnBondRankUp(CaptainData captain)
    {
        Tower tower = Tower.All.Find(t => t.Captain == captain);
        if (tower != null) tower.GrowSoldierSlots(SoldierCountFor(captain), slot => ClassForSlot(captain, slot));
    }

    // The class the player picked for this slot on the world map, or the match's default.
    public SoldierClassData ClassForSlot(CaptainData captain, int slot) =>
        RunState.Current.LoadoutClass(captain, slot) ?? data.DefaultClassForSlot(slot);

    public SoldierClassData[] SlotClassesFor(CaptainData captain)
    {
        var slotClasses = new SoldierClassData[SoldierCountFor(captain)];
        for (int i = 0; i < slotClasses.Length; i++)
            slotClasses[i] = ClassForSlot(captain, i);
        return slotClasses;
    }

    // The range a captain would have with the soldier classes it would field now: what a new
    // tower of theirs reaches, for placement checks before it exists.
    public float RangeFor(CaptainData captain)
    {
        var reaches = new List<float>();
        foreach (SoldierClassData soldierClass in SlotClassesFor(captain)) reaches.Add(soldierClass.reach);
        return TowerReach.Range(reaches, captain.range);
    }

    // Captains can't stand on a soldier's tile. A moving captain's own soldiers will pick new
    // tiles anyway, so theirs don't count.
    public bool CanPlaceOn(Vector2Int cell) => CanPlaceTower && grid.IsBuildable(cell) && !Tower.IsSoldierHome(cell);
    public bool CanMoveTo(Tower tower, Vector2Int cell) => grid.IsBuildable(cell) && !Tower.IsSoldierHome(cell, tower);

    public Tower PlaceTower(Vector2Int cell)
    {
        if (!CanPlaceOn(cell)) return null;
        Tower tower = Instantiate(towerPrefab, grid.CellCenter(cell), Quaternion.identity);
        CaptainData captain = NextCaptain;
        tower.Init(this, grid, captain, SlotClassesFor(captain), cell);
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
        GameEvents.RaiseGoldSpent();
        return true;
    }

    // Relocates a tower for Gold. It keeps its reserve and soldiers. A disabled tower stays put
    // until it is restored.
    public bool CanMove(Tower tower) => !IsOver && tower != null && !tower.IsDisabled && Tower.All.Contains(tower);

    public bool MoveTower(Tower tower, Vector2Int cell)
    {
        if (!CanMove(tower) || !CanMoveTo(tower, cell)) return false;
        int cost = MoveCost(tower);
        if (cost > 0 && !RunState.Current.TrySpendGold(cost)) return false;
        tower.ClearMoveDiscount();
        grid.ClearOccupant(tower.Cell);
        grid.SetOccupant(cell, tower);
        tower.MoveTo(cell, grid.CellCenter(cell));
        return true;
    }

    public void ToggleGameSpeed()
    {
        GameSpeed = GameSpeed > 1f ? 1f : 2f;
        ApplyTimeScale();
    }

    public const float MaxGameSpeed = 8f;

    // Any speed from 1x to 8x, for the AI Run; the time scale still resolves through TimeScaleRules.
    public void SetGameSpeed(float speed)
    {
        GameSpeed = Mathf.Clamp(speed, 1f, MaxGameSpeed);
        ApplyTimeScale();
    }

    public GridMap Grid => grid;
    public bool IsSpawning => spawner != null && spawner.IsSpawning;

    // Zooms the camera in on a tower, such as a captain making a request, or back out with null.
    public void FocusCamera(Tower tower)
    {
        if (cameraFocus != null) cameraFocus.Focus(tower != null ? tower.transform : null);
    }

    public void SetRequestPause(bool paused)
    {
        IsRequestPaused = paused && GameSettings.PauseDuringRequests;
        ApplyTimeScale();
    }

    // Every freeze (match over, hit-stop, a waiting request) goes through here, so ending one
    // never restarts the match while another still holds it.
    void ApplyTimeScale() => Time.timeScale = TimeScaleRules.Resolve(IsOver, IsHitStopped, IsRequestPaused, GameSpeed, IsPaused);

    // The player's pause from the header menu; held until they resume or leave.
    public bool IsPaused { get; private set; }

    public void SetPaused(bool paused)
    {
        if (paused && (IsOver || IsBetweenMatches)) return;
        IsPaused = paused;
        ApplyTimeScale();
    }

    // Leaves the match for the main menu. The run keeps its last save, so Resume run restarts this stop.
    public void ExitMatch()
    {
        IsPaused = false;
        MainMenu.Open();
    }

    // Ends the run: back to the setup screen with fresh health and Gold. Approval and bond ranks
    // are untouched, since they carry between runs.
    public void RestartRun()
    {
        RunSave.Delete();
        RunState.StartNewRun(data.startingHealth, data.startingGold);
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Back to the main menu from the setup, map or results screen. The run is already saved
    // whenever it can be resumed, so nothing is lost.
    public void ReturnToMenu()
    {
        if (!IsBetweenMatches && !IsOver) return;
        MainMenu.Open();
    }

    // The captains the setup screen offers: a random subset of the roster, drawn once per run.
    public IReadOnlyList<CaptainData> SetupOffer
    {
        get
        {
            List<CaptainData> offer = RunState.Current.SetupOffer;
            if (offer.Count > 0) return offer;
            List<CaptainData> roster = data.roster.FindAll(c => c != null);
            int size = CaptainOffer.Size(GameSettings.CaptainsOffered, data.partySize, roster.Count);
            foreach (int index in CaptainOffer.Draw(roster.Count, size, Random.Range(int.MinValue, int.MaxValue)))
                offer.Add(roster[index]);
            return offer;
        }
    }

    // The indices of the layouts with the biggest map, where the enemy base is played. Empty when
    // no layout sets a map size of its own.
    List<int> LargestLayouts()
    {
        int largest = 0;
        foreach (CityLayout layout in data.layouts)
            if (layout != null) largest = Mathf.Max(largest, layout.Area);
        var indices = new List<int>();
        if (largest == 0) return indices;
        for (int i = 0; i < data.layouts.Count; i++)
            if (data.layouts[i] != null && data.layouts[i].Area == largest) indices.Add(i);
        return indices;
    }

    // Starts the run with the chosen captains and a fresh world map; called from the setup screen.
    public void BeginRun(IEnumerable<CaptainData> party)
    {
        if (State != MatchState.Setup) return;
        RunState.Current.SetParty(party);
        RunState.Current.SetMap(WorldMap.Generate(Random.Range(int.MinValue, int.MaxValue), data.matchesPerRun,
            data.layouts.Count, data.minCitiesPerLayer, data.maxCitiesPerLayer, data.intelDepth, data.nodeOdds, LargestLayouts()));
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

    // The gifts on this shop's shelves: shopSize of the full list, picked by the shop's seed.
    public List<GiftData> ShopStock
    {
        get
        {
            var stock = new List<GiftData>();
            if (State != MatchState.Stop || City.kind != NodeKind.Shop) return stock;
            foreach (int index in global::ShopStock.Pick(data.shopGifts.Count, data.shopSize, City.seed))
                if (data.shopGifts[index] != null) stock.Add(data.shopGifts[index]);
            return stock;
        }
    }

    // A shop item the player can pay for right now.
    public bool CanBuyGift(GiftData gift) =>
        State == MatchState.Stop && City.kind == NodeKind.Shop && gift != null && RunState.Current.Gold >= gift.cost
        && ShopStock.Contains(gift);

    // Buys a gift at the shop for Gold; it goes into the run's inventory. Saved straight away so
    // quitting at the shop keeps it.
    public void BuyGift(GiftData gift)
    {
        if (!CanBuyGift(gift) || !RunState.Current.TrySpendGold(gift.cost)) return;
        RunState.Current.AddGift(gift);
        RunSave.Save(RunState.Current);
    }

    // Whether a captain can be given an item from the inventory: they're in the party, still here,
    // and the player has one.
    public bool CanGiveGift(CaptainData captain, GiftData gift) =>
        captain != null && gift != null && RunState.Current.Party.Contains(captain)
        && !RunState.Current.HasDeparted(captain) && RunState.Current.GiftCount(gift) > 0;

    // Gives a captain one item, changing their approval by how they feel about it. Returns the
    // reaction, or null when the gift couldn't be given.
    public GiftReaction? GiveGift(CaptainData captain, GiftData gift)
    {
        if (!CanGiveGift(captain, gift) || !RunState.Current.TryRemoveGift(gift)) return null;
        GiftReaction reaction = captain.ReactionTo(gift);
        ApprovalStore.Change(captain, gift.ApprovalFor(reaction));
        ApprovalStore.Save();
        RunSave.Save(RunState.Current);
        return reaction;
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
                var copy = new WaveDefinition
                {
                    healthMultiplier = wave.healthMultiplier * health,
                    // Kill Gold follows the wave's health: tankier waves cost more attacks per kill.
                    goldMultiplier = wave.goldMultiplier * wave.healthMultiplier * RunScaling.GoldMultiplier(layer + pass, data.goldGrowthPerMatch),
                };
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

    // Cheat: kills the enemy base's boss on the spot, with no tower credited.
    public bool CanCheatKillBoss => !IsOver && IsBossAlive;

    public void CheatKillBoss()
    {
        if (CanCheatKillBoss) Boss.TakeDamage(Boss.Health + 1f);
    }

    // Cheats: fill the run's Gold or health.
    public void CheatAddGold(int amount) => RunState.Current.AddGold(amount);

    public void CheatHealFully() => RunState.Current.Heal(RunState.Current.MaxHealth);

    // Cheat: reveals the whole world map and redraws it.
    public bool CanCheatRevealMap => RunState.Current.Map != null && RunState.Current.Map.nodes.Exists(n => !n.revealed);

    public void CheatRevealMap()
    {
        if (!CanCheatRevealMap) return;
        foreach (MapNode node in RunState.Current.Map.nodes) node.revealed = true;
        if (State != MatchState.Map) return;
        RunSave.Save(RunState.Current);
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Cheat: adds a shop as one more choice for the next step on the map, and redraws it.
    public void CheatAddShop()
    {
        if (State != MatchState.Map || RunState.Current.Map.AddStop(NodeKind.Shop) == null) return;
        RunSave.Save(RunState.Current);
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
        ApplyTimeScale();
    }

    // Counts in unscaled time, since the freeze itself sets timeScale to 0.
    void TickHitStop()
    {
        if (hitStopRemaining <= 0f) return;
        hitStopRemaining -= Time.unscaledDeltaTime;
        if (hitStopRemaining <= 0f) ApplyTimeScale();
    }

    void Finish(MatchState result)
    {
        State = result;
        spawner.StopSpawning();
        Tower.ClearWaveBoosts();
        ApplyTimeScale();
        bool won = result == MatchState.Won;
        if (won)
        {
            // Pay the win before the run moves on: the flat bonus, then the towers' unspent reserves.
            WinGoldEarned = RunScaling.WinGold(data.winGold, RunScaling.DifficultyLayer(City, data.eliteLayerBonus), data.goldGrowthPerMatch);
            RunState.Current.AddGold(WinGoldEarned);
            foreach (Tower tower in Tower.All) ReserveRefunded += tower.ReturnReserve(data.reserveRefundFraction);
            RunState.Current.CompleteMatch(data.intelDepth);
        }
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
        if (IsEliteMatch) RunState.Current.AddGold(Mathf.RoundToInt(enemy.GoldReward * (data.eliteGoldMultiplier - 1f)));
        if (enemy.KilledBy != null) enemy.KilledBy.GainXp(enemy.Data.xpReward);
    }

    void OnSoldierDied(Soldier soldier) => Casualties++;

    // A disabled tower sits out GameSettings.TowerDisabledWaves waves, counted from the end of the wave
    // in progress. With 0 it is restorable once the current wave concludes (straight away between waves).
    void OnTowerDisabled(Tower tower) =>
        tower.SetRestorableAfterWave(WavesCleared + (State == MatchState.WaveActive ? 1 : 0) + GameSettings.TowerDisabledWaves);

    void OnEnemyLeaked(Enemy enemy)
    {
        Leaks++;
        if (!IsOver && RunState.Current.IsDefeated)
            Finish(MatchState.Lost);
    }
}
