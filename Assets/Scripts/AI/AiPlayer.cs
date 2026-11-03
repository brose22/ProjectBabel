using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AiOptions
{
    public int runs = 1;
    public float speed = 4f;
    public int seed;
    // With a layout index, each run is one match on that map at this layer with this party.
    // Otherwise (-1) each run is a whole run with a party the AI picks.
    public int layout = -1;
    public int layer;
    // The location index whole runs are played at (opened in the AI's profile); -1 plays the
    // furthest one open, which is the first on a new profile.
    public int location = -1;
    // How many captains the party fields; 0 keeps the game's party size.
    public int captains;
    // The bond rank (0 = D to 4 = S) every captain in the party starts the run at.
    public int bondRank;
    // For a veteran party: the level every captain starts the run at (0 leaves them as rolled),
    // and the least rarity of their soldiers (-1 leaves them as rolled).
    public int captainLevel;
    public int soldierRarity = -1;
    // The roles of a single match's party, rolled from the templates when the run starts.
    public List<CaptainRole> partyRoles = new();

    public bool IsSingleMatch => layout >= 0;
}

// Plays whole runs by itself and writes a report. It survives the Match scene reloading at every
// node, finds the new MatchManager each time and drives the game through the same public calls
// the UI uses. Pure decisions live in the Ai* classes in Core/Pure; this class adapts game
// objects to them. Saves are redirected for the length of the batch so the player's are untouched.
public class AiPlayer : MonoBehaviour
{
    enum Phase { Running, Done }

    // Real seconds between decisions; waves are checked more often.
    const float ThinkSeconds = 0.25f;
    const float WaveThinkSeconds = 0.15f;
    // Real seconds with no progress before a run is given up as stalled.
    const float StallSeconds = 120f;
    const int MaxConsecutiveErrors = 3;
    // Think ticks to let towers draw their first Gold before the first wave starts.
    const int PrepTicks = 3;

    public static AiPlayer Instance { get; private set; }

    AiOptions options;
    readonly AiRunRecorder recorder = new();
    readonly List<AiRunRecord> finished = new();
    AiOverlay overlay;
    Phase phase;
    float speed;
    int runIndex;
    int runSeed;
    float gameSeconds;
    float thinkTimer;
    float lastProgressTime;
    int progressKey;
    int errorStreak;
    string reportPath = "";
    string csvPath = "";

    MatchManager match;
    CaptainRequests requests;
    bool matchEnded;
    bool stopHandled;
    int prepTicks;
    int lastPrepWave = -1;
    int movedAtWave = -1;
    bool pendingLoad;

    // Starts a batch from the main menu: redirects saves, then loads the Match scene on a fresh run.
    public static void Begin(AiOptions options)
    {
        if (Instance != null) return;
        var go = new GameObject("AI Player");
        DontDestroyOnLoad(go);
        go.AddComponent<AiPlayer>().Setup(options);
        SceneManager.LoadScene(MainMenu.MatchSceneName);
    }

    void Setup(AiOptions chosen)
    {
        Instance = this;
        options = chosen;
        speed = Mathf.Clamp(chosen.speed, 1f, MatchManager.MaxAiGameSpeed);
        runIndex = 1;
        runSeed = SeedFor(1);

        string folder = Path.Combine(Application.persistentDataPath, "ai");
        Directory.CreateDirectory(folder);
        RunSave.PathOverride = Path.Combine(folder, "run.json");
        ProfileSave.PathOverride = Path.Combine(folder, "profile.json");
        MatchStats.WriteCsvEnabled = false;
        RunSave.Delete();
        ProfileSave.Delete();
        RunState.Clear();
        Profile.Clear();

        recorder.Subscribe();
        overlay = new AiOverlay(transform, StatusText, () => recorder.LastDecision, () => speed, SetSpeed, () => Finish("Stopped"));
        lastProgressTime = Time.unscaledTime;
    }

    int SeedFor(int index) => options.seed + (index - 1) * 7919;

    void SetSpeed(float value)
    {
        speed = Mathf.Clamp(value, 1f, MatchManager.MaxAiGameSpeed);
        if (match != null) match.SetGameSpeed(speed);
    }

    string StatusText()
    {
        if (match == null) return $"Run {runIndex}/{options.runs}: loading";
        string where = match.State switch
        {
            MatchState.Setup => "picking a party",
            MatchState.Map => "on the map",
            MatchState.Stop => $"at a {match.City?.kind.ToString().ToLowerInvariant()}",
            _ => match.City != null ? $"{match.City.kind} (layer {match.City.layer}), wave {match.WavesCleared + (match.State == MatchState.WaveActive ? 1 : 0)}/{match.WaveCount}" : "match",
        };
        RunState run = RunState.Current;
        string health = run != null ? $"  HP {run.Health}/{run.MaxHealth}  Gold {run.Gold}" : "";
        return $"Run {runIndex}/{options.runs} (seed {runSeed}): {where}{health}";
    }

    void OnDestroy()
    {
        recorder.Unsubscribe();
        if (Instance == this) Instance = null;
        EndIsolation();
    }

    static void EndIsolation()
    {
        RunSave.PathOverride = null;
        ProfileSave.PathOverride = null;
        MatchStats.WriteCsvEnabled = true;
    }

    void Decide(string where, string text) => recorder.Decide(gameSeconds, where, text);

    void LateUpdate() => overlay?.Refresh();

    void Update()
    {
        if (phase == Phase.Done) return;
        gameSeconds += Time.deltaTime;

        // Leaving the match for the menu or the Castle by hand ends the batch where it stands.
        if (SceneManager.GetActiveScene().name is MainMenu.SceneName or CastleScreen.SceneName)
        {
            Finish("Stopped", showScreen: false);
            Destroy(gameObject);
            return;
        }

        if (match == null)
        {
            match = FindAnyObjectByType<MatchManager>();
            if (match == null) return;
            Bind();
        }

        AnswerRequests();

        thinkTimer -= Time.unscaledDeltaTime;
        if (thinkTimer > 0f) return;
        // Above 8x the AI thinks more often in real time, so it decides as often in game time as at 8x.
        thinkTimer = (match.State == MatchState.WaveActive ? WaveThinkSeconds : ThinkSeconds) * Mathf.Min(1f, MatchManager.MaxGameSpeed / speed);

        try
        {
            if (match.GameSpeed != speed) match.SetGameSpeed(speed);
            Think();
            errorStreak = 0;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (++errorStreak >= MaxConsecutiveErrors) Finish("Error");
        }
        CheckStall();
    }

    // A new scene's MatchManager: forget everything tied to the old one.
    void Bind()
    {
        requests = match.GetComponent<CaptainRequests>();
        matchEnded = false;
        stopHandled = false;
        prepTicks = 0;
        lastPrepWave = -1;
        movedAtWave = -1;
        pendingLoad = false;
        match.SetGameSpeed(speed);
    }

    void CheckStall()
    {
        RunState run = RunState.Current;
        // The boss outlasts the last wave, so its walk and its wounds count as progress too.
        Enemy boss = match.IsBossAlive ? match.Boss : null;
        int bossKey = boss == null ? 0 : HashCode.Combine(match.Grid.WorldToCell(boss.transform.position), Mathf.CeilToInt(boss.Health));
        int key = HashCode.Combine((int)match.State, match.WavesCleared, match.Kills, match.Leaks, run?.MatchesPlayed ?? -1,
            match.City?.id ?? -1, run?.Gold ?? 0, HashCode.Combine(run?.Party.Count ?? 0, bossKey));
        if (key != progressKey)
        {
            progressKey = key;
            lastProgressTime = Time.unscaledTime;
        }
        else if (Time.unscaledTime - lastProgressTime > StallSeconds)
        {
            Decide("Watchdog", $"no progress for {StallSeconds:0}s in state {match.State}");
            Finish("Stalled");
        }
    }

    // ---------- State machine ----------

    void Think()
    {
        if (pendingLoad) return;
        switch (match.State)
        {
            case MatchState.Setup: ThinkSetup(); break;
            case MatchState.Map: ThinkMap(); break;
            case MatchState.Stop: ThinkStop(); break;
            case MatchState.Preparing: ThinkPrepare(); break;
            case MatchState.WaveActive: ThinkWave(); break;
            default: ThinkEnd(); break;
        }
    }

    void ThinkSetup()
    {
        // Seeded before the offer is drawn and the map is generated, which draw from Random.
        UnityEngine.Random.InitState(runSeed);
        if (options.IsSingleMatch)
        {
            string map = match.Data.layouts[options.layout].displayName;
            List<Captain> rolled = RollParty(options.partyRoles);
            PrepareParty(rolled);
            recorder.BeginRun(runIndex, runSeed, rolled, Time.realtimeSinceStartup, gameSeconds);
            recorder.Run.note = $"One match on {map} at layer {options.layer + 1} of {match.Data.matchesPerRun - 1}, as picked on the AI Run screen.";
            Decide("Setup", $"rolled party {string.Join(", ", rolled.Select(c => $"{c.displayName} ({c.ClassName})"))} on {map}");
            match.BeginSingleMatch(rolled, options.layout, options.layer);
            pendingLoad = true;
            return;
        }
        if (options.location >= 0) match.SetupAtLocation(options.location);
        int partySize = options.captains > 0 ? options.captains : match.Data.partySize;
        // The setup screen offers the roster; top it up further when the party is bigger than that.
        match.SetupOfferMinimum = partySize;
        List<Captain> roster = match.SetupOffer.ToList();
        List<int> picks = AiPartyPicker.PickParty(roster.ConvertAll(Info), partySize);
        var party = picks.ConvertAll(i => roster[i]);
        PrepareParty(party);
        recorder.BeginRun(runIndex, runSeed, party, Time.realtimeSinceStartup, gameSeconds, match.Location != null ? match.Location.displayName : "");
        Decide("Setup", $"party {string.Join(", ", party.Select(c => c.displayName))}: strongest mix with a support");
        match.BeginRun(party);
    }

    // Sets the party's starting bond rank. The captains field the soldiers they join the run with.
    void PrepareParty(IEnumerable<Captain> party)
    {
        foreach (Captain captain in party)
        {
            ApprovalStore.SetRank(captain, options.bondRank);
            if (options.captainLevel > 1) captain.xp = Mathf.Max(captain.xp, LevelMath.XpForLevel(options.captainLevel));
            if (options.soldierRarity < 0) continue;
            foreach (SoldierItem soldier in RunState.Current.Soldiers.EquippedTo(captain.id))
                if ((int)soldier.rarity < options.soldierRarity) soldier.rarity = (SoldierRarity)options.soldierRarity;
        }
    }

    void ThinkMap()
    {
        RunState run = RunState.Current;
        if (run.RecruitOffer.Count > 0)
        {
            List<Captain> offer = run.RecruitOffer.ToList();
            var party = run.Party.FindAll(c => !run.HasDeparted(c)).ConvertAll(Info);
            int pick = AiPartyPicker.PickRecruit(offer.ConvertAll(Info), party);
            Captain recruit = pick >= 0 ? offer[pick] : null;
            Decide("Recruit", recruit != null ? $"took {recruit.displayName} to fill a gap in roles and elements" : "nobody on offer improved the party");
            match.Recruit(recruit);
            return;
        }

        List<MapNode> choices = run.Map.Choices();
        var roads = choices.ConvertAll(n => new AiRouteOption { id = n.id, kind = n.kind, revealed = n.revealed });
        int best = AiRoutePicker.Pick(roads, (float)run.Health / run.MaxHealth, run.Gold, match.Data.GiftCost, out string reason);
        if (best < 0)
        {
            Decide("Map", "no road to take");
            Finish("Stalled");
            return;
        }
        MapNode node = choices[best];
        Decide("Map", $"to {(node.revealed ? node.kind.ToString() : "unknown")} city {node.id} on layer {node.layer}: {reason}");
        recorder.NoteRoute(node.revealed ? node.kind : NodeKind.City);
        match.ChooseCity(node.id);
        pendingLoad = true;
    }

    void ThinkStop()
    {
        RunState run = RunState.Current;
        switch (match.City.kind)
        {
            case NodeKind.Rest:
                if (AiStopChoice.ShouldRest(run.Health, run.MaxHealth))
                {
                    LogStop($"rested at {run.Health}/{run.MaxHealth} health");
                    match.Rest();
                }
                else Leave("rest skipped at full health");
                break;
            default:
                EventData stopEvent = match.StopEvent;
                if (stopEvent == null) { Leave("no event here"); break; }
                int pick = AiStopChoice.PickEvent(stopEvent.choices, run.Gold, run.Health, run.MaxHealth, run.ActivePartyCount);
                if (pick < 0) { Leave("no event choice could be taken"); break; }
                EventChoice choice = stopEvent.choices[pick];
                LogStop($"event '{stopEvent.title}': {choice.label} ({choice.Describe()})");
                int goldBefore = run.Gold;
                match.TakeEventChoice(choice);
                recorder.NoteGold(run.Gold > goldBefore, "Events", System.Math.Abs(run.Gold - goldBefore));
                // A recruit for a full roster: the AI keeps the roster it has.
                if (match.PendingRecruit != null) match.ReleaseForRecruit(match.PendingRecruit);
                break;
        }
        pendingLoad = match.State == MatchState.Stop && stopHandled;
    }

    void LogStop(string text)
    {
        if (stopHandled) return;
        stopHandled = true;
        recorder.RecordStop(match);
        Decide("Stop", text);
    }

    void Leave(string text)
    {
        LogStop(text);
        match.LeaveStop();
    }

    void EnsureMatchRow()
    {
        if (recorder.CurrentMatch == null && !matchEnded) recorder.BeginMatch(match);
    }

    void ThinkPrepare()
    {
        EnsureMatchRow();
        RestoreTowers();
        PlaceTowers();
        SetDrawRates();
        MaybeMoveOne();

        // Waves after the first start on their own timer; start early only when that has run out.
        if (match.NextWaveCountdown > 0f) return;
        if (lastPrepWave != match.WavesCleared)
        {
            lastPrepWave = match.WavesCleared;
            prepTicks = 0;
        }
        if (++prepTicks < PrepTicks || Tower.All.Count == 0) return;
        match.StartNextWave();
    }

    void ThinkWave()
    {
        EnsureMatchRow();
        RestoreTowers();
        SetDrawRates();
        foreach (Tower tower in Tower.All.ToArray())
        {
            if (!tower.CanUseAbility || !ShouldFire(tower, out string reason)) continue;
            if (tower.TryUseAbility()) Decide("Special", $"{tower.Captain.displayName} fired {tower.Captain.Ability.displayName}: {reason}");
        }
    }

    void ThinkEnd()
    {
        if (!matchEnded)
        {
            matchEnded = true;
            bool won = match.State == MatchState.Won;
            recorder.EndMatch(match, won ? "Won" : "Lost");
            Decide("Match", $"{(won ? "won" : "lost")} at {match.WavesCleared}/{match.WaveCount} waves, {match.Kills} kills, {match.Leaks} leaks");
        }
        if (match.State == MatchState.Lost) NextRunOrFinish("Defeat");
        else if (options.IsSingleMatch) NextRunOrFinish("Victory");
        else if (RunState.Current.Map.IsComplete)
        {
            recorder.Run.note = $"The enemy base fell after {match.WavesCleared} of {match.WaveCount} waves.";
            NextRunOrFinish("Victory");
        }
        else
        {
            match.ReturnToMap();
            pendingLoad = true;
        }
    }

    void NextRunOrFinish(string result)
    {
        recorder.FinishRun(result, Time.realtimeSinceStartup, gameSeconds);
        finished.Add(recorder.Run);
        if (runIndex >= options.runs)
        {
            Finish(null);
            return;
        }
        runIndex++;
        runSeed = SeedFor(runIndex);
        // Each run starts from a new profile, so runs in a batch compare.
        match.RestartRun(freshProfile: true);
        pendingLoad = true;
    }

    // ---------- Setup helpers ----------

    // A captain rolled from the template of each role, seeded by the run so a batch can be replayed.
    List<Captain> RollParty(IEnumerable<CaptainRole> roles)
    {
        var party = new List<Captain>();
        int i = 0;
        foreach (CaptainRole role in roles)
        {
            CaptainData template = match.Data.captainTemplates.Find(t => t != null && t.role == role);
            if (template != null) party.Add(template.Roll(runSeed + 104729 * ++i));
        }
        return party;
    }

    AiCaptainInfo Info(Captain captain) => new()
    {
        name = captain.displayName,
        role = captain.role,
        support = captain.IsSupport,
        element = (int)captain.element,
        strength = captain.power * captain.speed,
        range = match.RangeFor(captain),
    };


    // ---------- Match helpers ----------

    List<(int x, int y)> RoadCells() => match.Grid.RoadCells.Select(c => (c.x, c.y)).ToList();

    // The road cells the towers on the field already reach, so new ones spread out.
    bool[] CoveredBy(IEnumerable<Tower> towers, List<(int x, int y)> road)
    {
        var covered = new bool[road.Count];
        foreach (Tower tower in towers)
            AiPlacementScorer.MarkCovered(tower.Cell.x, tower.Cell.y, tower.Range, road, covered);
        return covered;
    }

    static bool IsSupport(Captain captain) => captain.IsSupport;

    void PlaceTowers()
    {
        RunState run = RunState.Current;
        Vector2Int size = match.Grid.Size;
        while (match.CanPlaceTower)
        {
            var active = run.Party.FindAll(c => !run.HasDeparted(c));
            List<int> order = AiPartyPicker.FieldOrder(active.ConvertAll(Info));
            Captain captain = order.Select(i => active[i]).FirstOrDefault(c => !Tower.All.Exists(t => t.Captain == c));
            if (captain == null) return;
            match.ChooseNextCaptain(captain);

            var cells = new List<Vector2Int>();
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    if (match.CanPlaceOn(new Vector2Int(x, y))) cells.Add(new Vector2Int(x, y));
            List<(int x, int y)> road = RoadCells();
            int pick = AiPlacementScorer.PickCell(cells.ConvertAll(c => (c.x, c.y)), match.RangeFor(captain), road, IsSupport(captain), CoveredBy(Tower.All, road), out float score);
            if (pick < 0) return;
            Tower tower = match.PlaceTower(cells[pick]);
            if (tower == null) return;
            Decide("Placement", $"{captain.displayName} at {cells[pick].x},{cells[pick].y}: {(IsSupport(captain) ? "off the road, close to the party" : "widest road coverage")} (score {score:0.#})");
        }
    }

    void SetDrawRates()
    {
        int rate = AiPlacementScorer.DrawRateFor(RunState.Current.Gold, Tower.All.Count);
        foreach (Tower tower in Tower.All)
            if ((int)tower.DrawRate != rate) tower.SetDrawRate((DrawRate)rate);
    }

    void RestoreTowers()
    {
        foreach (Tower tower in Tower.All.ToArray())
        {
            if (!match.CanRestore(tower)) continue;
            int cost = match.RestoreCost(tower);
            if (!AiPlacementScorer.ShouldRestore(cost, RunState.Current.Gold, Tower.All.Count)) continue;
            if (!match.TryRestoreTower(tower)) continue;
            recorder.NoteGold(false, "Restores", cost);
            Decide("Restore", $"{tower.Captain.displayName} back on the field for {cost} Gold");
        }
    }

    // Between waves, relocates one captain whose spot covers little road, if Gold allows.
    void MaybeMoveOne()
    {
        if (match.WavesCleared == 0 || movedAtWave == match.WavesCleared) return;
        movedAtWave = match.WavesCleared;
        Vector2Int size = match.Grid.Size;
        List<(int x, int y)> road = RoadCells();
        foreach (Tower tower in Tower.All.ToArray())
        {
            if (!match.CanMove(tower)) continue;
            var others = Tower.All.Where(t => t != tower);
            bool[] covered = CoveredBy(others, road);
            bool support = IsSupport(tower.Captain);
            float current = AiPlacementScorer.Score(tower.Cell.x, tower.Cell.y, tower.Range, road, support, covered);
            var cells = new List<Vector2Int>();
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    if (match.CanMoveTo(tower, new Vector2Int(x, y))) cells.Add(new Vector2Int(x, y));
            int pick = AiPlacementScorer.PickCell(cells.ConvertAll(c => (c.x, c.y)), tower.Range, road, support, covered, out float best);
            if (pick < 0 || !AiPlacementScorer.ShouldMove(current, best, RunState.Current.Gold, match.MoveCost(tower))) continue;
            int moveCost = match.MoveCost(tower);
            if (!match.MoveTower(tower, cells[pick])) continue;
            recorder.NoteGold(false, "Moves", moveCost);
            Decide("Move", $"{tower.Captain.displayName} to {cells[pick].x},{cells[pick].y}: coverage {current:0.#} rose to {best:0.#}");
            return;
        }
    }

    bool ShouldFire(Tower tower, out string reason)
    {
        reason = "";
        AbilityData ability = tower.Captain.Ability;
        float weakest = 1f;
        foreach (Tower t in Tower.All)
            if (!t.IsDisabled) weakest = Mathf.Min(weakest, t.Health / t.MaxHealth);
        int inRange = 0;
        bool boss = false;
        foreach (Enemy enemy in Enemy.Active)
        {
            if (Vector2.Distance(tower.transform.position, enemy.transform.position) > tower.AbilityRange) continue;
            inRange++;
            boss |= enemy.IsBoss;
        }
        int left = Enemy.Active.Count;
        bool spawningDone = !match.IsSpawning;

        if (tower.IsSupportAbility)
        {
            if (ability.healFraction > 0f && AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Heal, inRange, boss, weakest, left, spawningDone))
            {
                reason = $"a captain is down to {weakest:P0} health";
                return true;
            }
            if (ability.HasBuff && AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Buff, inRange, boss, weakest, left, spawningDone))
            {
                reason = $"{left} enemies on the field to buff against";
                return true;
            }
            return false;
        }
        if (!AiAbilityRules.ShouldFire(AiAbilityRules.Kind.Attack, inRange, boss, weakest, left, spawningDone)) return false;
        reason = boss ? "boss in reach" : $"{inRange} enemies in reach";
        return true;
    }

    // Captains' requests pause the match, so they are answered at once rather than on a think tick.
    void AnswerRequests()
    {
        CaptainRequests.Request request = requests != null ? requests.Active : null;
        if (request == null) return;
        int answer = AiAbilityRules.AnswerRequest((int)request.Kind, RunState.Current.Gold, Tower.All.Count);
        string who = request.Tower != null ? request.Tower.Captain.displayName : "A captain";
        Decide("Request", $"{who} asked: {request.Text} Answered {(RequestAnswer)answer}");
        recorder.NoteRequest(request.Kind, (RequestAnswer)answer);
        requests.Answer(request, (RequestAnswer)answer);
    }

    // ---------- Finishing ----------

    // Ends the batch: closes the run in progress with the given result (null when it already has
    // one), writes the report and shows the finished screen.
    void Finish(string interruptedResult, bool showScreen = true)
    {
        if (phase == Phase.Done) return;
        phase = Phase.Done;
        if (interruptedResult != null && recorder.Run != null)
        {
            if (match != null && recorder.CurrentMatch != null) recorder.EndMatch(match, interruptedResult);
            recorder.FinishRun(interruptedResult, Time.realtimeSinceStartup, gameSeconds);
            finished.Add(recorder.Run);
        }
        WriteReport();
        if (!showScreen) return;
        if (match != null) match.SetGameSpeed(1f);
        Time.timeScale = 0f;
        int wins = finished.Count(r => r.Won);
        overlay.ShowDone(reportPath, csvPath, $"{finished.Count} run{(finished.Count == 1 ? "" : "s")} played, {wins} won.", ExitToMenu);
    }

    void WriteReport()
    {
        try
        {
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Reports");
            Directory.CreateDirectory(folder);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            reportPath = Path.Combine(folder, $"AiRun_{stamp}.md");
            csvPath = Path.Combine(folder, $"AiRun_{stamp}_waves.csv");
            File.WriteAllText(reportPath, AiReportFormatter.Markdown(finished, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            File.WriteAllText(csvPath, AiReportFormatter.Csv(finished));
            Debug.Log($"[AI Run] Report written to {reportPath}");
        }
        catch (Exception e)
        {
            reportPath = "";
            Debug.LogWarning($"[AI Run] Couldn't write the report: {e.Message}");
        }
    }

    void ExitToMenu()
    {
        EndIsolation();
        Time.timeScale = 1f;
        Destroy(gameObject);
        MainMenu.Open();
    }
}
