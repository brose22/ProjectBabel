using System.Collections.Generic;
using System.Linq;
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
    // Where this run is played, or null when the match data has no locations.
    public LocationData Location { get; private set; }
    // The enemy base's boss: the location's own, else the match's.
    EnemyData BossData => Location != null && Location.boss != null ? Location.boss : data.boss;
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
    // What a lost run cost the profile: Gold, and the deployed captains with their soldiers.
    public int GoldLost { get; private set; }
    public List<Captain> CaptainsLost { get; private set; } = new();
    public int SoldiersLost { get; private set; }
    // Every captain placed on the field this match, even if later sold. A lost match takes these.
    readonly List<Captain> deployed = new();
    public IReadOnlyList<Captain> Deployed => deployed;
    // The location a won run opened, or null.
    public LocationData LocationUnlocked { get; private set; }
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
    CameraPan cameraPan;

    // Game time the current wave started.
    public float WaveStartTime { get; private set; }

    // Roster index the player has cycled to; NextCaptain skips anyone already on the field.
    int nextCaptainIndex;

    // The captain the next placed tower gets: the chosen one, or the next one after it who
    // isn't already on the field.
    public Captain NextCaptain
    {
        get
        {
            List<Captain> party = RunState.Current.Party;
            int count = party.Count;
            for (int i = 0; i < count; i++)
            {
                Captain captain = party[(nextCaptainIndex + i) % count];
                if (captain != null && !Tower.All.Exists(t => t.Captain == captain))
                    return captain;
            }
            return null;
        }
    }

    // Makes this captain the next one placed, if they aren't on the field already.
    public void ChooseNextCaptain(Captain captain)
    {
        int index = RunState.Current.Party.IndexOf(captain);
        if (index >= 0) nextCaptainIndex = index;
    }

    // The party captain the player's cycler is on, placed or not; null with no party.
    public Captain SelectedCaptain
    {
        get
        {
            List<Captain> party = RunState.Current.Party;
            int index = CaptainCycle.Step(party, nextCaptainIndex, 0);
            return index >= 0 ? party[index] : null;
        }
    }

    // Moves the cycler step captains along the party, wrapping at the ends.
    public void CycleCaptain(int step)
    {
        int index = CaptainCycle.Step(RunState.Current.Party, nextCaptainIndex, step);
        if (index >= 0) nextCaptainIndex = index;
    }

    public static bool IsOnField(Captain captain) => captain != null && Tower.All.Exists(t => t.Captain == captain);

    // Whether a click on a free tile would place the selected captain: they aren't placed yet.
    public bool CanPlaceSelected => SelectedCaptain != null && !IsOnField(SelectedCaptain) && CanPlaceTower;

    public bool CanPlaceTower => !IsOver && Tower.All.Count < data.maxTowers && NextCaptain != null;
    // Captains can be rearranged for free until the first wave starts.
    bool MovesAreFree => State == MatchState.Preparing && WavesCleared == 0;
    // Relocation cost for this tower, after any discount a granted request gave it.
    public int MoveCost(Tower tower) => MovesAreFree ? 0 : tower == null ? data.relocationCost : Mathf.RoundToInt(data.relocationCost * tower.MoveCostMultiplier);
    public bool CanAffordMove(Tower tower) => RunState.Current.Gold >= MoveCost(tower);

    void Awake()
    {
        // Pick up the saved profile and run if there are any, otherwise start fresh.
        if (RunState.Current == null && !RunSave.TryResume(LoadProfile(), data.shopGifts))
            StartRun();
        // A run without a party starts at the setup screen, then the world map until a city is picked.
        // A run the Castle just set out on has its party but no map yet.
        RunState run = RunState.Current;
        Location = LocationSelect.Of(data, run);
        if (run.HasParty && run.Map == null) run.SetMap(NewWorldMap());
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
            if (!cam.TryGetComponent(out cameraPan)) cameraPan = cam.gameObject.AddComponent<CameraPan>();
            cameraPan.Setup(grid);
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
        resolvedAtWaveStart = Kills + Leaks;
        WaveEnemyTotal = 0;
        foreach (SpawnGroup group in waves[WavesCleared].groups) WaveEnemyTotal += group.count;
        Tower.ClearWaveBoosts();
        spawner.SpawnWave(waves[WavesCleared]);
        if (IsBaseMatch && BossData != null && Boss == null)
        {
            Boss = spawner.SpawnOne(BossData, RunScaling.HealthMultiplier(City.layer, data.enemyHealthGrowthPerMatch),
                RunScaling.GoldMultiplier(City.layer, data.goldGrowthPerMatch), ElementFor(BossData));
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
    public int SoldierCountFor(Captain captain) => SoldierCountFor(data, captain);

    public static int SoldierCountFor(MatchData data, Captain captain) =>
        Mathf.Max(data.soldiersPerTower, BondMath.SoldierCount(ApprovalStore.RankOf(captain)));

    // A rank-up mid-match fields any soldier equipped in the slot it opens.
    void OnBondRankUp(Captain captain)
    {
        Tower tower = Tower.All.Find(t => t.Captain == captain);
        if (tower != null) tower.FieldSoldiers(FieldedSoldiers(captain));
    }

    // The soldiers a captain fields: those equipped in the slots its bond has opened, in slot
    // order. Empty slots field nobody.
    public List<SoldierItem> FieldedSoldiers(Captain captain)
    {
        int open = SoldierCountFor(captain);
        return RunState.Current.SoldiersOf(captain).FindAll(s => s.slot < open && data.ClassData(s.soldierClass) != null);
    }

    public List<SoldierClassData> SlotClassesFor(Captain captain) =>
        FieldedSoldiers(captain).ConvertAll(s => data.ClassData(s.soldierClass));

    // What a captain's fielded soldiers add to one of its stats.
    public float StatBonus(Captain captain, CaptainStat stat) => data.StatBonus(FieldedSoldiers(captain), stat);

    // The range a captain would have with the soldiers it would field now: what a new tower of
    // theirs reaches, for placement checks before it exists.
    public float RangeFor(Captain captain)
    {
        var reaches = new List<float>();
        foreach (SoldierClassData soldierClass in SlotClassesFor(captain)) reaches.Add(soldierClass.reach);
        return TowerReach.Range(reaches, captain.range + StatBonus(captain, CaptainStat.Range));
    }

    // A captain joining the roster outside the starting pick (a recruit, an AI run's extra
    // captains) with no soldiers brings a few random ones, equipped in its first slots.
    void GiveStartingSoldiers(IEnumerable<Captain> captains) => GiveStartingSoldiers(data, RunState.Current.Soldiers, captains);

    public static void GiveStartingSoldiers(MatchData data, SoldierInventory inventory, IEnumerable<Captain> captains)
    {
        foreach (Captain captain in captains)
        {
            if (captain == null || inventory.EquippedTo(captain.id).Count > 0) continue;
            for (int i = 0; i < data.startingSoldiersPerCaptain; i++)
            {
                int slot = inventory.FirstEmptySlot(captain.id, SoldierCountFor(data, captain));
                if (slot < 0) break;
                SoldierItem soldier = SoldierRules.Roll(Random.Range(int.MinValue, int.MaxValue));
                inventory.Add(soldier);
                inventory.Equip(soldier.id, captain.id, slot);
            }
        }
    }

    // Captains can't stand on a soldier's tile. A moving captain's own soldiers will pick new
    // tiles anyway, so theirs don't count. Nor may any of its soldiers end up waiting on a gate
    // or the castle, which only happens when every tile around the captain is taken.
    public bool CanPlaceOn(Vector2Int cell) => CanPlaceTower && grid.IsBuildable(cell) && !Tower.IsSoldierHome(cell)
        && HomesClear(Tower.PickHomes(grid, cell, SlotClassesFor(NextCaptain)));
    public bool CanMoveTo(Tower tower, Vector2Int cell) => grid.IsBuildable(cell) && !Tower.IsSoldierHome(cell, tower)
        && HomesClear(Tower.PickHomes(grid, cell, tower.SlotClasses(), tower));

    bool HomesClear(List<Vector2Int> homes) => homes.TrueForAll(home => !grid.IsGateOrCastle(home));

    public Tower PlaceTower(Vector2Int cell)
    {
        if (!CanPlaceOn(cell)) return null;
        Tower tower = Instantiate(towerPrefab, grid.CellCenter(cell), Quaternion.identity);
        Captain captain = NextCaptain;
        tower.Init(this, grid, captain, FieldedSoldiers(captain), cell);
        grid.SetOccupant(cell, tower);
        if (!deployed.Contains(captain)) deployed.Add(captain);
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

    // The player's speeds, stepped by the HUD button and the F key; the AI Run sets any speed directly.
    public static readonly float[] PlayerSpeeds = { 1f, 2f, 3f };

    public void CycleGameSpeed()
    {
        int next = 0;
        for (int i = 0; i < PlayerSpeeds.Length; i++)
            if (Mathf.Approximately(PlayerSpeeds[i], GameSpeed)) next = (i + 1) % PlayerSpeeds.Length;
        SetGameSpeed(PlayerSpeeds[next]);
    }

    // Enemies the current wave fields, and how many have been killed or leaked so far. The boss
    // is not counted in the total, so the progress is clamped.
    public int WaveEnemyTotal { get; private set; }
    public int WaveEnemiesResolved => Mathf.Clamp(Kills + Leaks - resolvedAtWaveStart, 0, WaveEnemyTotal);
    int resolvedAtWaveStart;

    // Distinct enemy types of the wave about to start, in the order they appear; empty when none is due.
    public List<EnemyData> NextWaveEnemies()
    {
        var types = new List<EnemyData>();
        if (waves == null || WavesCleared >= WaveCount) return types;
        foreach (SpawnGroup group in waves[WavesCleared].groups)
            if (group.enemy != null && !types.Contains(group.enemy)) types.Add(group.enemy);
        if (IsBaseMatch && BossData != null && Boss == null) types.Add(BossData);
        return types;
    }

    public const float MaxGameSpeed = 8f;
    // AI Run batches for tuning may go faster than a player can; frames get shorter with the frame cap off.
    public const float MaxAiGameSpeed = 32f;

    // Any speed from 1x to 32x, for the AI Run; the time scale still resolves through TimeScaleRules.
    public void SetGameSpeed(float speed)
    {
        GameSpeed = Mathf.Clamp(speed, 1f, MaxAiGameSpeed);
        ApplyTimeScale();
    }

    public GridMap Grid => grid;
    public bool IsSpawning => spawner != null && spawner.IsSpawning;

    // Zooms the camera in on a tower, such as a captain making a request, or back out with null.
    public void FocusCamera(Tower tower)
    {
        if (cameraFocus != null) cameraFocus.Focus(tower != null ? tower.transform : null);
    }

    // True when the player has panned or zoomed away from the default view.
    public bool CameraMoved => cameraPan != null && cameraPan.IsMoved;

    public void RecenterCamera()
    {
        if (cameraPan != null) cameraPan.Recenter();
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

    // Ends the run: back to the setup screen with fresh health. The profile, with the roster,
    // soldiers and Gold, carries on, unless freshProfile asks for a new one (for AI runs that
    // each start from scratch).
    public void RestartRun(bool freshProfile = false)
    {
        RunSave.Delete();
        if (freshProfile)
        {
            ProfileSave.Delete();
            Profile.Clear();
        }
        else if (RunState.Current != null) ProfileSave.Save(RunState.Current.Profile);
        StartRun();
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Ends the run, by abandoning it or once it is won or lost, and goes home to the Castle. The
    // profile, with the roster, soldiers and Gold, is kept.
    public void ReturnToCastle()
    {
        IsPaused = false;
        RunSave.Delete();
        if (RunState.Current != null) ProfileSave.Save(RunState.Current.Profile);
        CastleScreen.Open();
    }

    // The profile in memory, else the saved one, else a new one with the starting Gold.
    Profile LoadProfile()
    {
        if (Profile.Current == null && !ProfileSave.TryLoad(data.captainTemplates))
            Profile.StartNew(data.startingGold);
        return Profile.Current;
    }

    // A new run at the furthest open location, with fresh health. The Castle starts runs at the
    // location picked instead (LocationSelect.TryStartRun).
    void StartRun()
    {
        Profile profile = LoadProfile();
        RunState.StartNewRun(profile, LocationSelect.Default(data, profile)?.id, data.startingHealth);
    }

    // Back to the main menu from the setup, map or results screen. The run is already saved
    // whenever it can be resumed, so nothing is lost.
    public void ReturnToMenu()
    {
        if (!IsBetweenMatches && !IsOver) return;
        MainMenu.Open();
    }

    // AI runs: moves a run still at its setup to the location at this index, opening it (and those
    // before it) in the profile.
    public void SetupAtLocation(int index)
    {
        if (State != MatchState.Setup || index < 0 || index >= data.locations.Count || data.locations[index] == null) return;
        Profile profile = RunState.Current.Profile;
        profile.UnlockLocations(index + 1);
        RunState.StartNewRun(profile, data.locations[index].id, data.startingHealth);
        Location = data.locations[index];
    }

    // At least this many captains are offered, for an AI run that fields a bigger party than usual.
    public int SetupOfferMinimum { get; set; }

    // The captains the setup screen offers (AI runs; players pick the party at the Castle): the
    // profile's roster. A new profile first gets all of the starting captains, each with their
    // soldiers, for the AI to pick its party from; a bigger AI party tops the roster up with rolled
    // captains, each with a few soldiers.
    public IReadOnlyList<Captain> SetupOffer
    {
        get
        {
            Profile profile = RunState.Current.Profile;
            if (profile.Roster.Count == 0)
                TakeStartingCaptains(profile, RollStartingCaptains(data, Random.Range(int.MinValue, int.MaxValue)),
                    Random.Range(int.MinValue, int.MaxValue));
            int size = Mathf.Min(Profile.MaxRoster, Mathf.Max(SetupOfferMinimum, data.partySize));
            if (profile.Roster.Count < size)
                foreach (Captain captain in RollCaptains(size - profile.Roster.Count, Random.Range(int.MinValue, int.MaxValue)))
                    if (profile.TryAdd(captain)) GiveStartingSoldiers(new[] { captain });
            ProfileSave.Save(profile);
            return profile.Roster;
        }
    }

    // Rolls count captains from the templates, every template once before any repeats. Names
    // already in the roster or on offer are rerolled where the template has others, so names tell
    // captains apart.
    public List<Captain> RollCaptains(int count, int seed)
    {
        RunState run = RunState.Current;
        return RollCaptains(data, run.Profile.Roster.Concat(run.Party).Concat(run.Departed), count, seed);
    }

    // The same outside a match (the Castle), with the names of the given captains taken.
    public static List<Captain> RollCaptains(MatchData data, IEnumerable<Captain> named, int count, int seed)
    {
        List<CaptainData> templates = data.captainTemplates.FindAll(t => t != null);
        return Roll(templates, CaptainOffer.Draw(templates.Count, count, seed), named);
    }

    // The starting captains offered on the first play: one of each starting role, with no two
    // sharing a name.
    public static List<Captain> RollStartingCaptains(MatchData data, int seed)
    {
        List<CaptainData> templates = data.captainTemplates.FindAll(t => t != null);
        return Roll(templates, StartingCaptains.Draw(templates.ConvertAll(t => t.role), seed), Enumerable.Empty<Captain>());
    }

    // Adds the picked starting captains to the roster, each with its soldiers equipped. The
    // caller saves the profile.
    public static void TakeStartingCaptains(Profile profile, IEnumerable<Captain> picked, int seed)
    {
        var joined = picked.Where(profile.TryAdd).ToList();
        StartingCaptains.GiveSoldiers(profile.Soldiers, joined.Select(c => c.id), seed);
    }

    static List<Captain> Roll(List<CaptainData> templates, IEnumerable<(int template, int seed)> draws, IEnumerable<Captain> named)
    {
        var taken = new HashSet<string>(named.Select(c => c.displayName));
        var rolled = new List<Captain>();
        foreach ((int template, int rollSeed) in draws)
        {
            Captain captain = templates[template].Roll(rollSeed);
            for (int retry = 1; retry <= MaxNameRerolls && taken.Contains(captain.displayName); retry++)
                captain = templates[template].Roll(rollSeed + retry);
            taken.Add(captain.displayName);
            rolled.Add(captain);
        }
        return rolled;
    }

    const int MaxNameRerolls = 20;

    // How many gates each layout has, by layout index; a missing layout counts as one.
    List<int> LayoutGateCounts()
    {
        var counts = new List<int>();
        foreach (CityLayout layout in data.layouts)
            counts.Add(layout != null ? Mathf.Max(1, layout.AllRoutes().Count) : 1);
        return counts;
    }

    // A fresh world map for the run's location.
    WorldMap NewWorldMap()
    {
        int seed = Random.Range(int.MinValue, int.MaxValue);
        return Location != null
            ? WorldMap.Generate(seed, Location.Spec, data.layouts.Count, data.intelDepth, LayoutGateCounts())
            : WorldMap.Generate(seed, data.matchesPerRun, data.layouts.Count, data.minCitiesPerLayer, data.maxCitiesPerLayer,
                data.intelDepth, data.nodeOdds, LayoutGateCounts(), data.doubleGateFirstLayer, data.tripleGateFirstLayer);
    }

    // Starts the run with the chosen roster captains and a fresh world map for its location;
    // called from the setup screen.
    public void BeginRun(IEnumerable<Captain> party)
    {
        if (State != MatchState.Setup) return;
        RunState.Current.SetParty(party.Where(RunState.Current.Profile.Owns));
        RunState.Current.SetMap(NewWorldMap());
        nextCaptainIndex = 0;
        State = MatchState.Map;
        RunSave.Save(RunState.Current);
    }

    // Starts a run that is one match on the given layout, with no world map to cross. Layer sets
    // the difficulty; called by the AI run, which ends the run when the match does. Captains not
    // yet in the roster join it, with a few soldiers each, while there's room.
    public void BeginSingleMatch(IEnumerable<Captain> party, int layout, int layer)
    {
        if (State != MatchState.Setup) return;
        Profile profile = RunState.Current.Profile;
        foreach (Captain captain in party)
            if (profile.TryAdd(captain)) GiveStartingSoldiers(new[] { captain });
        RunState.Current.SetParty(party.Where(profile.Owns));
        RunState.Current.SetMap(WorldMap.SingleMatch(Random.Range(int.MinValue, int.MaxValue), data.matchesPerRun, layout, layer));
        nextCaptainIndex = 0;
        RunSave.Save(RunState.Current);
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // Takes an offered soldier into the soldier inventory (or none, for null) and saves the choice.
    public void TakeSoldier(SoldierItem soldier)
    {
        RunState.Current.TakeSoldier(soldier);
        RunSave.Save(RunState.Current);
    }

    // A won city offers a few newly rolled soldiers to pick one from. Elite cities roll rarity more
    // than once and keep the best, for better odds.
    void OfferSoldiers()
    {
        int rarityRolls = IsEliteMatch ? Mathf.Max(1, data.eliteRewardRarityRolls) : 1;
        RunState.Current.OfferSoldiers(SoldierRules.RollOffer(Random.Range(int.MinValue, int.MaxValue),
            Mathf.Max(0, data.rewardSoldierChoices), rarityRolls));
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

    // Whether the shop can be shopped at now: from the world map.
    public bool ShopOpen => State == MatchState.Map;

    // The gifts on the shelves: shopSize of the full list, picked by the shop node's seed, or by
    // the run's first node when shopping from the map.
    public List<GiftData> ShopStock
    {
        get
        {
            var stock = new List<GiftData>();
            if (!ShopOpen) return stock;
            int seed = State == MatchState.Stop ? City.seed : RunState.Current.Map.nodes[0].seed;
            foreach (int index in global::ShopStock.Pick(data.shopGifts.Count, data.shopSize, seed))
                if (data.shopGifts[index] != null) stock.Add(data.shopGifts[index]);
            return stock;
        }
    }

    // A shop item the player can pay for right now.
    public bool CanBuyGift(GiftData gift) =>
        ShopOpen && gift != null && RunState.Current.Gold >= gift.cost && ShopStock.Contains(gift);

    // Buys a gift at the shop for Gold; it goes into the run's inventory. Saved straight away so
    // quitting at the shop keeps it.
    public void BuyGift(GiftData gift)
    {
        if (!CanBuyGift(gift) || !RunState.Current.TrySpendGold(gift.cost)) return;
        RunState.Current.AddGift(gift);
        RunSave.Save(RunState.Current);
    }

    // Whether a captain can be given an item from the inventory: they're in the party and the
    // player has one.
    public bool CanGiveGift(Captain captain, GiftData gift) =>
        captain != null && gift != null && RunState.Current.Party.Contains(captain) && RunState.Current.GiftCount(gift) > 0;

    // Gives a captain one item, changing their approval by how they feel about it. Returns the
    // reaction, or null when the gift couldn't be given.
    public GiftReaction? GiveGift(Captain captain, GiftData gift)
    {
        if (!CanGiveGift(captain, gift) || !RunState.Current.TryRemoveGift(gift)) return null;
        GiftReaction reaction = captain.ReactionTo(gift);
        ApprovalStore.Change(captain, gift.ApprovalFor(reaction));
        RunSave.Save(RunState.Current);
        return reaction;
    }

    // The event at this event node, picked from the pool by the node's seed; null with no events.
    public EventData StopEvent =>
        State == MatchState.Stop && City.kind == NodeKind.Event && data.events.Count > 0 ? data.events[City.seed % data.events.Count] : null;

    public bool CanTakeEventChoice(EventChoice choice) =>
        StopEvent != null && choice != null && PendingRecruit == null && choice.CanTake(RunState.Current.Gold, RunState.Current.Health);

    // A captain an event recruited into a full roster, waiting on ReleaseForRecruit.
    public Captain PendingRecruit => RunState.Current?.PendingRecruit;

    // Applies an event choice to the run and moves on.
    public void TakeEventChoice(EventChoice choice)
    {
        if (!CanTakeEventChoice(choice)) return;
        RunState run = RunState.Current;
        if (choice.gold >= 0) run.AddGold(choice.gold);
        else run.TrySpendGold(-choice.gold);
        if (choice.health >= 0) run.Heal(choice.health);
        else run.TakeDamage(-choice.health);
        foreach (Captain captain in run.Party)
        {
            run.AddXp(captain, choice.xp);
            if (choice.approval != 0f) ApprovalStore.Change(captain, choice.approval);
        }
        // A newcomer for a full roster stays on the stop until the player picks who to release.
        if (choice.recruit && !RecruitFromEvent()) return;
        LeaveStop();
    }

    // Rolls a captain for an event to recruit. Returns false while they wait on a full roster.
    bool RecruitFromEvent()
    {
        RunState run = RunState.Current;
        Captain captain = RollCaptains(1, Random.Range(int.MinValue, int.MaxValue)).FirstOrDefault();
        if (run.RecruitFromEvent(captain, data.maxPartySize)) GiveStartingSoldiers(new[] { captain });
        return run.PendingRecruit == null;
    }

    // Settles the pending recruit by releasing either them or a captain from the roster, whose
    // soldiers go back to the inventory, then moves on from the event.
    public void ReleaseForRecruit(Captain released)
    {
        Captain newcomer = PendingRecruit;
        if (newcomer == null || released == null) return;
        if (RunState.Current.ReleaseFor(released, data.maxPartySize)) GiveStartingSoldiers(new[] { newcomer });
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

    // The element an enemy fights with in this city: its own, or the city's when the location
    // doesn't use its own. Null keeps the enemy's.
    Element? ElementFor(EnemyData enemy) =>
        Location != null && enemy != null ? Location.EnemyElement(enemy, City != null ? City.element : -1) : null;

    // Copies the authored waves with more and tougher enemies further into the run, played by the
    // location's enemies in its elements. Elite cities play a few layers tougher. The enemy base
    // plays them baseWaveRepeats times over, each pass a layer tougher.
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
                {
                    EnemyData enemy = Location != null ? Location.Field(group.enemy) : group.enemy;
                    copy.groups.Add(new SpawnGroup
                    {
                        enemy = enemy,
                        element = ElementFor(enemy),
                        count = RunScaling.EnemyCount(group.count, layer + pass, data.enemyCountGrowthPerMatch),
                        spawnInterval = group.spawnInterval,
                        delayBefore = group.delayBefore,
                        route = group.route,
                    });
                }
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

    // Jolts the camera and freezes the match for a moment when an ability lands so the hit reads.
    // Overlapping abilities extend the freeze to the longest one rather than stacking.
    void OnAbilityUsed(Tower tower)
    {
        AbilityData ability = tower.Captain.Ability;
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
        // Walkouts are judged on this, so the soldier reward is offered after it.
        GameEvents.RaiseMatchEnded(won);

        // The city falls as soon as the match is won and is saved straight away, so the win counts
        // even if the player quits here. A lost or finished run has nothing left to resume.
        bool runGoesOn = won && !RunState.Current.Map.IsComplete;
        if (runGoesOn)
        {
            int before = RunState.Current.Health;
            RunState.Current.Heal(data.healthPerWin);
            HealthRestored = RunState.Current.Health - before;
            OfferSoldiers();
            RunSave.Save(RunState.Current);
        }
        else
        {
            // The run is over; the profile it leaves behind is kept. Winning it opens the next location.
            if (won) RecordRunWon();
            else LoseRun();
            ProfileSave.Save(RunState.Current.Profile);
            RunSave.Delete();
        }
    }

    // A won run opens the next location; the party keeps its levels and bonds.
    void RecordRunWon()
    {
        Profile profile = RunState.Current.Profile;
        int before = profile.LocationsUnlocked;
        LocationSelect.RecordWin(data, profile, RunState.Current.Location);
        if (profile.LocationsUnlocked > before && profile.LocationsUnlocked <= data.locations.Count)
            LocationUnlocked = data.locations[profile.LocationsUnlocked - 1];
    }

    // A lost run costs a share of the Gold and every captain deployed this match, with their
    // soldiers. Undeployed party captains go home safely.
    void LoseRun()
    {
        RunState run = RunState.Current;
        CaptainsLost = run.Profile.LoseRun(deployed, out int goldLost, out int soldiersLost);
        GoldLost = goldLost;
        SoldiersLost = soldiersLost;
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
