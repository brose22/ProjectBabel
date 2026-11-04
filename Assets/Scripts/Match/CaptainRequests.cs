using System;
using System.Collections.Generic;
using UnityEngine;

public enum RequestAnswer { No, Yes }

// Captains ask for things when something happens in the match (GDD: up to 2 per wave, often none):
//   MoreDraw   - a soldier fell and the reserve can't pay to bring it back.
//   Relocate   - nothing has come within the captain's range for a while. The captain moves
//                themselves, free, 10% closer to the enemy gate along the road.
//   UseSpecial - an elite or the boss walked on, an enemy leaked, or the special just finished
//                charging. The captain fires it by themselves, as soon as it can be used.
//   Reinforce  - another captain fell. The captain walks to the free spot nearest the fallen tower
//                that still covers road, free.
//   LowerDraw  - the reserve has sat full for a while. Sets the draw to Low.
// The player answers Yes, which grants the request and the approval reward, or No, which always
// costs approval, scaled by request and captain.
// Only one request is up at a time and the match pauses while it waits; a new one that would come
// in meanwhile is not asked at all. One left unanswered when
// its countdown runs out counts as a refusal, while one still open when the wave ends is dropped
// without costing approval. Added to the match at runtime.
public class CaptainRequests : MonoBehaviour
{
    public class Request
    {
        public Tower Tower;
        public RequestKind Kind;
        // The tower that fell, for Reinforce.
        public Tower Fallen;
        // What the captain says; names the reason they are asking.
        public string Text;
        // Real time the request goes unanswered, set once it is shown.
        public float Deadline = -1f;
        // Where a Relocate or Reinforce would take the captain, and what that leaves uncovered.
        public Vector2Int? Target;
        public int UncoveredTiles;
        public string Area;
        // Set when the request was prompted by an elite, the boss or a leak.
        public bool ThreatPresent;
    }

    const float ApprovalReward = 2f;
    const float RefusalPenalty = 3f;
    // A Yes on a special that can't fire yet waits this long for it to be able to.
    const float AutoFireTimeoutSeconds = 20f;
    // Real-time seconds the player has to answer a shown request.
    public const float ResponseSeconds = 10f;
    // At most this many requests come in a wave.
    const int MaxRequestsPerWave = 2;
    // A captain doesn't ask for the same thing again until this many waves have passed.
    const int CooldownWaves = 4;
    // Game seconds with nothing in range, while enemies are elsewhere on the field, before a captain asks to move.
    const float IdleSeconds = 20f;
    // Game seconds a full reserve sits before the captain offers to draw less.
    const float FullReserveSeconds = 15f;
    // A Relocate moves the captain this share closer to the gate, measured along the road.
    const float MoveCloserFraction = 0.1f;
    // Game seconds between checks of the conditions that have no event of their own.
    const float PollSeconds = 0.5f;
    // Chance a poll lets a captain act on their personality; polls are twice a second.
    const float TraitAskChancePerPoll = 0.05f;
    // A cautious captain asks to fall back once their health drops below this share.
    const float CautiousHealthFraction = 0.6f;

    class Watch
    {
        public float LastEnemyInRange;
        public bool SpecialWasReady;
        // Game time the reserve filled, or below zero while it isn't full.
        public float ReserveFullSince = -1f;
    }

    // In arrival order; the first is the one shown.
    readonly List<Request> pending = new();
    readonly Dictionary<Tower, Watch> watches = new();
    // The wave each captain last asked each thing in.
    readonly Dictionary<(Tower, RequestKind), int> lastAsked = new();
    MatchManager match;
    Relationships relationships;
    int wave;
    int askedThisWave;
    // Bad requests the player refused this wave, and the penalty each cost, repaid if the wave goes clean.
    readonly List<(Tower tower, float penalty)> refusedBad = new();
    // A leak or a fallen tower this wave: the refusals were not proven right.
    bool wavePoorlyDefended;
    float nextPoll;

    public IReadOnlyList<Request> Pending => pending;
    public Request Active => pending.Count > 0 ? pending[0] : null;
    public float SecondsLeft => Active == null ? 0f : Mathf.Max(0f, Active.Deadline - Time.unscaledTime);

    void Awake()
    {
        match = GetComponent<MatchManager>();
        relationships = GetComponent<Relationships>();
    }

    void OnEnable()
    {
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.WaveEnded += OnWaveEnded;
        GameEvents.TowerSold += OnTowerSold;
        GameEvents.SoldierDied += OnSoldierDied;
        GameEvents.EnemyArrived += OnEnemyArrived;
        GameEvents.EnemyLeaked += OnEnemyLeaked;
        GameEvents.EnemyLeaked += MarkWaveDirty;
        GameEvents.TowerDisabled += OnTowerDisabled;
        GameEvents.TowerDisabled += MarkWaveDirty;
    }

    void OnDisable()
    {
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveEnded -= OnWaveEnded;
        GameEvents.TowerSold -= OnTowerSold;
        GameEvents.SoldierDied -= OnSoldierDied;
        GameEvents.EnemyArrived -= OnEnemyArrived;
        GameEvents.EnemyLeaked -= OnEnemyLeaked;
        GameEvents.EnemyLeaked -= MarkWaveDirty;
        GameEvents.TowerDisabled -= OnTowerDisabled;
        GameEvents.TowerDisabled -= MarkWaveDirty;
    }

    void OnWaveStarted(int waveNumber)
    {
        wave = waveNumber;
        askedThisWave = 0;
        refusedBad.Clear();
        wavePoorlyDefended = false;
        watches.Clear();
        foreach (Tower tower in Tower.All)
            watches[tower] = new Watch { LastEnemyInRange = Time.time, SpecialWasReady = tower.IsSpecialReady };
    }

    void OnWaveEnded(int waveNumber)
    {
        // Requests still open are dismissed without a verdict; only their own countdown refuses them.
        foreach (Tower tower in Tower.All) tower.ClearRequestLocks();
        PayBackRefusals();
        foreach (Request request in pending.ToArray())
            Expire(request);
    }

    // Drops a request nobody answered in time because the wave ended, with no approval change.
    void Expire(Request request)
    {
        if (!pending.Remove(request)) return;
        ShowNext();
    }

    void OnTowerSold(Tower tower)
    {
        watches.Remove(tower);
        pending.RemoveAll(r => r.Tower == tower);
        ShowNext();
    }

    // The reserve can't pay to respawn the fallen soldier, so the captain wants a faster draw.
    void OnSoldierDied(Soldier soldier)
    {
        Tower tower = soldier.Tower;
        if (tower == null || tower.DrawRate == DrawRate.High || tower.Reserve >= soldier.RespawnCost) return;
        TryAsk(tower, RequestKind.MoreDraw, "My soldiers fell and I can't afford to bring them back. Turn my draw up to High?");
    }

    // The ready captain closest to an elite or the boss asks to unleash their special on it.
    void OnEnemyArrived(Enemy enemy) =>
        AskClosestReadyCaptain(enemy.transform.position, enemy.IsBoss ? "The boss is here" : "An elite is here");

    // An enemy got through, so the ready captain closest to where it escaped asks to cut loose.
    void OnEnemyLeaked(Enemy enemy) =>
        AskClosestReadyCaptain(enemy.transform.position, "One got past us");

    void AskClosestReadyCaptain(Vector3 position, string reason)
    {
        Tower closest = null;
        float closestDistance = float.MaxValue;
        foreach (Tower tower in Tower.All)
        {
            if (tower.IsDisabled || !tower.IsSpecialReady) continue;
            float distance = Vector2.Distance(tower.transform.position, position);
            if (distance >= closestDistance) continue;
            closest = tower;
            closestDistance = distance;
        }
        if (closest == null) return;
        TryAsk(closest, RequestKind.UseSpecial, $"{reason}. Let me use {closest.Captain.Ability.displayName} now?", threat: true);
    }

    // The working captain nearest a fallen tower that has a spot to cover it from asks to move in.
    void OnTowerDisabled(Tower fallen)
    {
        Tower helper = null;
        float helperDistance = float.MaxValue;
        foreach (Tower tower in Tower.All)
        {
            if (tower == fallen || tower.IsDisabled) continue;
            float distance = Vector2.Distance(tower.transform.position, fallen.transform.position);
            if (distance >= helperDistance || FindCellNear(tower, fallen.transform.position) == null) continue;
            helper = tower;
            helperDistance = distance;
        }
        if (helper == null) return;
        TryAsk(helper, RequestKind.Reinforce, $"{fallen.Captain.displayName} has fallen. Let me move in to cover?", fallen: fallen);
    }

    // The conditions with no event of their own are checked a couple of times a second, in game
    // time so nothing new comes in while the match is paused.
    void Update()
    {
        if (match.State == MatchState.WaveActive && Time.time >= nextPoll)
        {
            nextPoll = Time.time + PollSeconds;
            Poll();
        }

        Request active = Active;
        if (active != null && !match.IsOver && Time.unscaledTime >= active.Deadline) Answer(active, RequestAnswer.No, timedOut: true);
    }

    void Poll()
    {
        bool enemiesOnField = Enemy.Active.Count > 0;
        foreach (Tower tower in Tower.All)
        {
            if (tower.IsDisabled) continue;
            if (!watches.TryGetValue(tower, out Watch watch))
                watches[tower] = watch = new Watch { LastEnemyInRange = Time.time, SpecialWasReady = tower.IsSpecialReady };

            bool ready = tower.IsSpecialReady;
            if (ready && !watch.SpecialWasReady)
                TryAsk(tower, RequestKind.UseSpecial, $"{tower.Captain.Ability.displayName} is charged. Use it now?");
            watch.SpecialWasReady = ready;

            // A reserve that stays full is Gold doing nothing.
            if (tower.Reserve < tower.Captain.ResourceCapacity) watch.ReserveFullSince = -1f;
            else if (watch.ReserveFullSince < 0f) watch.ReserveFullSince = Time.time;
            else if (Time.time - watch.ReserveFullSince >= FullReserveSeconds && tower.DrawRate != DrawRate.Low)
                TryAsk(tower, RequestKind.LowerDraw, "My reserve is overflowing. Ease off my draw?");

            AskFromTrait(tower);

            if (!enemiesOnField || tower.FindClosestEnemy(tower.transform.position) != null)
                watch.LastEnemyInRange = Time.time;
            else if (Time.time - watch.LastEnemyInRange >= IdleSeconds && FindCellNearerSpawner(tower) != null)
                TryAsk(tower, RequestKind.Relocate, "Nothing comes this way. Let me move closer to the gate?");
        }
    }

    // A personality asks on its own cue, and the cue is a match state that makes the request a bad idea.
    void AskFromTrait(Tower tower)
    {
        if (UnityEngine.Random.value > TraitAskChancePerPoll) return;
        switch (tower.Captain.Trait)
        {
            case CaptainTrait.GlorySeeker:
                if (tower.IsSpecialReady && Enemy.Active.Count > 0 && match.SpawnsToElite >= 0)
                    TryAsk(tower, RequestKind.UseSpecial, $"Let me use {tower.Captain.Ability.displayName} on these weaklings. Glory awaits!", fromTrait: true);
                break;
            case CaptainTrait.Greedy:
                if (tower.DrawRate != DrawRate.High && RunState.Current.Gold < RequestEvaluator.GoldComfortPerDrawer)
                    TryAsk(tower, RequestKind.MoreDraw, "I'm worth a bigger cut of the Gold. High draw?", fromTrait: true);
                break;
            case CaptainTrait.Cautious:
                if (tower.Health < tower.MaxHealth * CautiousHealthFraction)
                    TryAsk(tower, RequestKind.Retreat, "It's getting too hot here. Let me fall back?", fromTrait: true);
                break;
        }
    }

    // Starts the countdown on the request now shown, holds the match while one waits and zooms
    // the camera in on the captain asking.
    void ShowNext()
    {
        Request active = Active;
        if (active != null && active.Deadline < 0f) active.Deadline = Time.unscaledTime + ResponseSeconds;
        match.SetRequestPause(active != null);
        match.FocusCamera(active?.Tower);
    }

    // Shows the request unless one is already up (nothing is queued behind it), the wave is out of
    // requests, or the captain asked for the same thing too recently.
    void TryAsk(Tower tower, RequestKind kind, string text, Tower fallen = null, bool threat = false, bool fromTrait = false)
    {
        if (match.State != MatchState.WaveActive || match.IsOver || tower.IsDisabled) return;
        if (askedThisWave >= MaxRequestsPerWave || pending.Count > 0) return;
        if (lastAsked.TryGetValue((tower, kind), out int last) && wave - last < CooldownWaves) return;
        // A captain whose draw or place was just granted is locked until the wave ends, so there is nothing to ask.
        bool movesTower = kind == RequestKind.Relocate || kind == RequestKind.Reinforce || kind == RequestKind.Retreat;
        if ((movesTower && tower.MoveLocked) || ((kind == RequestKind.MoreDraw || kind == RequestKind.LowerDraw) && tower.DrawLocked)) return;

        Vector2Int? target = kind == RequestKind.Relocate ? FindCellNearerSpawner(tower)
            : kind == RequestKind.Retreat ? FindCellNearerSpawner(tower, retreat: true)
            : kind == RequestKind.Reinforce && fallen != null ? FindCellNear(tower, fallen.transform.position) : null;

        var request = new Request { Tower = tower, Kind = kind, Text = text, Fallen = fallen, ThreatPresent = threat, Target = target };
        if (target != null) MeasureUncovered(request);
        // A steady captain only makes sound requests; a personality ask only comes when the match makes it a mistake.
        bool bad = Evaluate(request).IsBad;
        if (fromTrait ? !bad : bad && tower.Captain.Trait == CaptainTrait.Steady) return;

        lastAsked[(tower, kind)] = wave;
        askedThisWave++;
        pending.Add(request);
        ShowNext();
    }

    // The route nearest a position, and how far along it, in tiles from its gate, the position is.
    (int route, int fromGate) NearestRoad(Vector3 position) => RouteMath.Nearest(match.Grid.Routes, position.x, position.y);

    // Among free cells by the captain's own road, nearer its gate than the captain is now, and still
    // reaching at least as much road as now, the one whose road distance is closest to
    // MoveCloserFraction less than the captain's current one. Null when there is none.
    // With retreat the cell is farther from the gate instead, and the captain may reach less road.
    Vector2Int? FindCellNearerSpawner(Tower tower, bool retreat = false)
    {
        GridMap grid = match.Grid;
        if (grid.RoadCells.Count == 0) return null;
        (int route, int current) = NearestRoad(tower.transform.position);
        int currentCoverage = CountRoadInRange(tower.transform.position, tower.Range);
        float target = current * (retreat ? 1f + MoveCloserFraction : 1f - MoveCloserFraction);
        float bestError = float.MaxValue;
        Vector2Int? pick = null;
        for (int x = 0; x < grid.Size.x; x++)
        {
            for (int y = 0; y < grid.Size.y; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!match.CanMoveTo(tower, cell)) continue;
                Vector3 center = grid.CellCenter(cell);
                (int cellRoute, int distance) = NearestRoad(center);
                if (cellRoute != route) continue;
                if (retreat ? distance <= current : distance >= current || !RoadReach.KeepsCoverage(CountRoadInRange(center, tower.Range), currentCoverage)) continue;
                float error = Mathf.Abs(distance - target);
                if (error >= bestError) continue;
                bestError = error;
                pick = cell;
            }
        }
        return pick;
    }

    readonly List<Vector2> roadCenters = new();

    // How many road tiles a position reaches with the given range.
    int CountRoadInRange(Vector3 position, float range)
    {
        if (roadCenters.Count != match.Grid.RoadCells.Count)
        {
            roadCenters.Clear();
            foreach (Vector2Int road in match.Grid.RoadCells)
                roadCenters.Add(match.Grid.CellCenter(road));
        }
        return RoadReach.CountInRange(position, roadCenters, range);
    }

    // The free cell closest to a point that is nearer to it than the captain is now and still has
    // at least as much road in the captain's range as now, or null when there is none.
    Vector2Int? FindCellNear(Tower tower, Vector3 point)
    {
        GridMap grid = match.Grid;
        float best = Vector2.Distance(tower.transform.position, point);
        int currentCoverage = CountRoadInRange(tower.transform.position, tower.Range);
        Vector2Int? pick = null;
        for (int x = 0; x < grid.Size.x; x++)
        {
            for (int y = 0; y < grid.Size.y; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!match.CanMoveTo(tower, cell)) continue;
                Vector3 center = grid.CellCenter(cell);
                float distance = Vector2.Distance(center, point);
                if (distance >= best || !RoadReach.KeepsCoverage(CountRoadInRange(center, tower.Range), currentCoverage)) continue;
                best = distance;
                pick = cell;
            }
        }
        return pick;
    }

    void MoveTowardSpawner(Tower tower) => MoveFreeTo(tower, FindCellNearerSpawner(tower));

    // The move costs the captain's player nothing.
    void MoveFreeTo(Tower tower, Vector2Int? cell)
    {
        if (cell == null) return;
        tower.GrantMoveDiscount(0f);
        if (!match.MoveTower(tower, cell.Value)) tower.ClearMoveDiscount();
    }

    public void Answer(Request request, RequestAnswer answer, bool timedOut = false)
    {
        if (!pending.Remove(request)) return;
        ShowNext();
        if (request.Tower == null) return;
        Tower tower = request.Tower;
        if (answer == RequestAnswer.No)
        {
            float penalty = RefusalPenalty * RefusalWeight(request.Kind) * tower.Captain.refusalTemper;
            relationships.Change(tower, RequestRules.ApprovalChange(RequestOutcome.Refused, ApprovalReward, penalty), "was refused");
            // Only an explicit No to a bad request can be proven right; a timeout is an ordinary refusal.
            if (!timedOut && Evaluate(request).IsBad) refusedBad.Add((tower, penalty));
            return;
        }

        Grant(request);
        relationships.Change(tower, RequestRules.ApprovalChange(RequestOutcome.Granted, ApprovalReward, 0f), "got their way");
    }

    void MarkWaveDirty(Enemy _) => wavePoorlyDefended = true;
    void MarkWaveDirty(Tower _) => wavePoorlyDefended = true;

    // The wave is over: captains whose bad request you refused admit you were right if nothing leaked or fell.
    void PayBackRefusals()
    {
        foreach ((Tower tower, float penalty) in refusedBad)
        {
            float back = RequestRules.Payback(penalty, !wavePoorlyDefended);
            if (back > 0f && tower != null) relationships.Change(tower, back, "admits you were right");
        }
        refusedBad.Clear();
    }

    // Approval a Yes or a No would change by, for the request card.
    public float ApprovalIfYes => RequestRules.ApprovalChange(RequestOutcome.Granted, ApprovalReward, 0f);

    public float ApprovalIfNo(Request request) => request.Tower == null ? 0f :
        RequestRules.ApprovalChange(RequestOutcome.Refused, ApprovalReward,
            RefusalPenalty * RefusalWeight(request.Kind) * request.Tower.Captain.refusalTemper);

    // Road the move would leave out of the captain's reach, and the road it is on, for the card.
    void MeasureUncovered(Request request)
    {
        Tower tower = request.Tower;
        Vector3 to = match.Grid.CellCenter(request.Target.Value);
        CountRoadInRange(tower.transform.position, tower.Range);
        request.UncoveredTiles = RoadReach.CountLost(tower.transform.position, to, roadCenters, tower.Range);
        int route = NearestRoad(tower.transform.position).route;
        request.Area = match.Grid.Routes.Count > 1 ? $"road {route + 1}" : "the road";
    }

    // The pros and cons of a Yes right now; Gold and the other captains' draws change while it is up.
    public RequestEvaluation Evaluate(Request request)
    {
        int drawing = 0;
        foreach (Tower tower in Tower.All)
            if (tower != request.Tower && tower.DrawRate == DrawRate.High) drawing++;
        return RequestEvaluator.Evaluate(new RequestSnapshot
        {
            Kind = request.Kind,
            ApprovalYes = ApprovalIfYes,
            Gold = RunState.Current.Gold,
            DrawingCaptains = drawing,
            SpawnsToElite = match.SpawnsToElite,
            ThreatPresent = request.ThreatPresent,
            UncoveredRoadTiles = request.UncoveredTiles,
            Area = request.Area,
            ReserveGold = request.Tower != null ? request.Tower.Reserve : 0,
        });
    }

    // How much a refusal stings, by request.
    static float RefusalWeight(RequestKind kind) => kind switch
    {
        RequestKind.Relocate => 0.75f,
        _ => 1f,
    };

    void Grant(Request request)
    {
        Tower tower = request.Tower;
        switch (request.Kind)
        {
            case RequestKind.MoreDraw:
                tower.SetDrawRate(DrawRate.High, ignoreLock: true);
                tower.LockDraw();
                break;
            case RequestKind.Relocate:
                MoveTowardSpawner(tower);
                tower.LockMove();
                break;
            case RequestKind.UseSpecial:
                // Fires now, or the moment it can if nothing is in reach yet.
                if (!tower.TryUseAbility()) tower.FireAbilityWhenAble(AutoFireTimeoutSeconds);
                break;
            case RequestKind.Retreat:
                MoveFreeTo(tower, FindCellNearerSpawner(tower, retreat: true));
                tower.LockMove();
                break;
            case RequestKind.Reinforce:
                if (request.Fallen != null) MoveFreeTo(tower, FindCellNear(tower, request.Fallen.transform.position));
                tower.LockMove();
                break;
            case RequestKind.LowerDraw:
                // The captain pockets half the reserve; it leaves the match.
                tower.TrySpendReserve(tower.Reserve / 2);
                tower.SetDrawRate(DrawRate.Low, ignoreLock: true);
                tower.LockDraw();
                break;
        }
    }
}
