using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    // The run this match is part of: everything between matches, and what the run gains or loses
    // when one ends.
    public RunController Run => runController ??= new RunController(this);
    RunController runController;
    // Where the run is played, or null when the match data has no locations.
    LocationData Location => Run.Location;
    public int WavesCleared { get; private set; }
    public int WaveCount => waves.Count;
    // The city being played, or null on the setup and map screens.
    public MapNode City { get; private set; }
    public CityLayout CityLayout => City != null && data.layouts.Count > 0 ? data.layouts[City.layout % data.layouts.Count] : null;
    public bool IsBaseMatch => City != null && RunState.Current.Map != null && City.id == RunState.Current.Map.BaseId;
    public bool IsEliteMatch => City != null && City.kind == NodeKind.Elite;
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
    // Reserves handed back by towers sold this match, and the extra Gold elite kills paid, for MatchStats.
    public int SellRefunded { get; private set; }
    public int EliteGoldEarned { get; private set; }
    // Every captain placed on the field this match, even if later sold. A lost match takes these.
    readonly List<Captain> deployed = new();
    public IReadOnlyList<Captain> Deployed => deployed;
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

    // For the run: the cycler goes back to the party's first captain.
    public void ResetCaptainCycle() => nextCaptainIndex = 0;

    // For the run: the party is chosen, so the setup screen gives way to the world map.
    public void EnterMap()
    {
        if (State != MatchState.Setup) return;
        nextCaptainIndex = 0;
        State = MatchState.Map;
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
        Run.Load();
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
        SellRefunded += tower.Reserve;
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
    public bool CanMove(Tower tower) => !IsOver && tower != null && !tower.IsDisabled && !tower.MoveLocked && Tower.All.Contains(tower);

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

    // The element an enemy type of the next wave fights with: its group's, or the boss's location element.
    public Element NextWaveElement(EnemyData enemy)
    {
        if (waves != null && WavesCleared < WaveCount)
            foreach (SpawnGroup group in waves[WavesCleared].groups)
                if (group.enemy == enemy) return group.element ?? enemy.element;
        return enemy == BossData ? ElementFor(BossData) ?? enemy.element : enemy.element;
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
    public int SpawnsToElite => spawner != null ? spawner.SpawnsToElite() : -1;

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

    // For the run: drops the pause as the scene is left, without touching the time scale.
    public void Unpause() => IsPaused = false;

    public void SetPaused(bool paused)
    {
        if (paused && (IsOver || IsBetweenMatches)) return;
        IsPaused = paused;
        ApplyTimeScale();
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
            Run.OfferSoldiers();
            RunSave.Save(RunState.Current);
        }
        else
        {
            // The run is over; the profile it leaves behind is kept. Winning it opens the next location.
            if (won) Run.RecordRunWon();
            else Run.LoseRun();
            ProfileSave.Save(RunState.Current.Profile);
            RunSave.Delete();
        }
    }

    // The tower that landed the killing blow earns the XP; a destroyed (sold) tower earns nothing.
    // Elite cities pay extra Gold on top of what the kill already added.
    void OnEnemyKilled(Enemy enemy)
    {
        Kills++;
        if (enemy == Boss && !IsOver && BossRules.IsMatchWon(WavesCleared, WaveCount, false)) Finish(MatchState.Won);
        if (IsEliteMatch)
        {
            int extra = Mathf.Max(0, Mathf.RoundToInt(enemy.GoldReward * (data.eliteGoldMultiplier - 1f)));
            RunState.Current.AddGold(extra);
            EliteGoldEarned += extra;
        }
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
