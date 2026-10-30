using System;
using System.Collections.Generic;
using UnityEngine;

public enum RequestKind { MoreDraw, Relocate, NewMix, UseSpecial }

public enum RequestAnswer { No, Some, Yes }

// Captains ask for things when something happens in the match (GDD: up to 2 per wave, often none):
//   MoreDraw   - a soldier fell and the reserve can't pay to bring it back.
//   Relocate   - nothing has come within the captain's range for a while. Yes only: the captain
//                moves themselves, free, to the spot nearest the enemy gate that still covers road.
//   NewMix     - the captain levelled up and a class they like isn't in every slot.
//   UseSpecial - an elite or the boss walked on, or the special just finished charging. Yes only:
//                the captain fires it by themselves, as soon as it can be used.
// The player answers No (0%), Some (50%) or Yes (100%): the answer grants that share of the request
// and of the approval reward. Refusing always costs approval, scaled by request and captain.
// Requests are shown one at a time and the match pauses while one waits; one left unanswered when
// its countdown runs out, or when the wave ends, counts as a refusal. Added to the match at runtime.
public class CaptainRequests : MonoBehaviour
{
    public class Request
    {
        public Tower Tower;
        public RequestKind Kind;
        // The soldier class asked for, for NewMix.
        public SoldierClassData WantedClass;
        // What the captain says; names the reason they are asking.
        public string Text;
        // Real time the request goes unanswered, set once it is shown.
        public float Deadline = -1f;
    }

    const float ApprovalReward = 4f;
    const float RefusalPenalty = 3f;
    // A Yes on a special that can't fire yet waits this long for it to be able to.
    const float AutoFireTimeoutSeconds = 20f;
    // Real-time seconds the player has to answer a shown request.
    public const float ResponseSeconds = 10f;
    // At most this many requests come in a wave.
    const int MaxRequestsPerWave = 2;
    // A captain doesn't ask for the same thing again until this many waves have passed.
    const int CooldownWaves = 2;
    // Game seconds with nothing in range, while enemies are elsewhere on the field, before a captain asks to move.
    const float IdleSeconds = 20f;
    // Game seconds between checks of the conditions that have no event of their own.
    const float PollSeconds = 0.5f;

    class Watch
    {
        public float LastEnemyInRange;
        public bool SpecialWasReady;
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
        GameEvents.CaptainLeveledUp += OnCaptainLeveledUp;
        GameEvents.EnemyArrived += OnEnemyArrived;
    }

    void OnDisable()
    {
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveEnded -= OnWaveEnded;
        GameEvents.TowerSold -= OnTowerSold;
        GameEvents.SoldierDied -= OnSoldierDied;
        GameEvents.CaptainLeveledUp -= OnCaptainLeveledUp;
        GameEvents.EnemyArrived -= OnEnemyArrived;
    }

    void OnWaveStarted(int waveNumber)
    {
        wave = waveNumber;
        askedThisWave = 0;
        watches.Clear();
        foreach (Tower tower in Tower.All)
            watches[tower] = new Watch { LastEnemyInRange = Time.time, SpecialWasReady = tower.IsSpecialReady };
    }

    void OnWaveEnded(int waveNumber)
    {
        // Unanswered requests count as refusals.
        foreach (Request request in pending.ToArray())
            Answer(request, RequestAnswer.No);
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

    // A level-up is when a captain asks for the soldiers they like.
    void OnCaptainLeveledUp(Tower tower)
    {
        SoldierClassData wanted = LikedClassMissingFrom(tower);
        if (wanted == null) return;
        TryAsk(tower, RequestKind.NewMix, $"I've grown stronger. Give me {wanted.displayName}s?", wanted);
    }

    // The ready captain closest to an elite or the boss asks to unleash their special on it.
    void OnEnemyArrived(Enemy enemy)
    {
        Tower closest = null;
        float closestDistance = float.MaxValue;
        foreach (Tower tower in Tower.All)
        {
            if (tower.IsDisabled || !tower.IsSpecialReady) continue;
            float distance = Vector2.Distance(tower.transform.position, enemy.transform.position);
            if (distance >= closestDistance) continue;
            closest = tower;
            closestDistance = distance;
        }
        if (closest == null) return;
        string who = enemy.IsBoss ? "The boss is here" : "An elite is here";
        TryAsk(closest, RequestKind.UseSpecial, $"{who}. Let me use {closest.Captain.ability.displayName} now?");
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
        if (active != null && !match.IsOver && Time.unscaledTime >= active.Deadline) Answer(active, RequestAnswer.No);
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
                TryAsk(tower, RequestKind.UseSpecial, $"{tower.Captain.ability.displayName} is charged. Use it now?");
            watch.SpecialWasReady = ready;

            if (!enemiesOnField || tower.FindClosestEnemy(tower.transform.position) != null)
                watch.LastEnemyInRange = Time.time;
            else if (Time.time - watch.LastEnemyInRange >= IdleSeconds && FindCellNearerSpawner(tower) != null)
                TryAsk(tower, RequestKind.Relocate, "Nothing comes this way. Let me move closer to the gate?");
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

    // Queues the request unless the wave is out of requests, the captain already has one waiting
    // or asked for the same thing too recently.
    void TryAsk(Tower tower, RequestKind kind, string text, SoldierClassData wanted = null)
    {
        if (match.State != MatchState.WaveActive || match.IsOver || tower.IsDisabled) return;
        if (askedThisWave >= MaxRequestsPerWave || pending.Exists(r => r.Tower == tower)) return;
        if (lastAsked.TryGetValue((tower, kind), out int last) && wave - last < CooldownWaves) return;

        lastAsked[(tower, kind)] = wave;
        askedThisWave++;
        pending.Add(new Request { Tower = tower, Kind = kind, Text = text, WantedClass = wanted });
        ShowNext();
    }

    // The free cell closest to the enemy gate that is nearer to it than the captain is now and
    // still has road in the captain's range, or null when there is none.
    Vector2Int? FindCellNearerSpawner(Tower tower)
    {
        GridMap grid = match.Grid;
        if (grid.RoadCells.Count == 0) return null;
        Vector3 spawn = grid.CellCenter(grid.RoadCells[0]);
        float best = Vector2.Distance(tower.transform.position, spawn);
        Vector2Int? pick = null;
        for (int x = 0; x < grid.Size.x; x++)
        {
            for (int y = 0; y < grid.Size.y; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!match.CanMoveTo(tower, cell)) continue;
                Vector3 center = grid.CellCenter(cell);
                float distance = Vector2.Distance(center, spawn);
                if (distance >= best || !HasRoadInRange(center, tower.Range)) continue;
                best = distance;
                pick = cell;
            }
        }
        return pick;
    }

    bool HasRoadInRange(Vector3 position, float range)
    {
        foreach (Vector2Int road in match.Grid.RoadCells)
            if (Vector2.Distance(position, match.Grid.CellCenter(road)) <= range) return true;
        return false;
    }

    // The move costs the captain's player nothing.
    void MoveTowardSpawner(Tower tower)
    {
        Vector2Int? cell = FindCellNearerSpawner(tower);
        if (cell == null) return;
        tower.GrantMoveDiscount(0f);
        if (!match.MoveTower(tower, cell.Value)) tower.ClearMoveDiscount();
    }

    // A soldier class the captain likes that isn't in every slot yet, or null.
    SoldierClassData LikedClassMissingFrom(Tower tower)
    {
        foreach (SoldierClassData soldierClass in match.Data.ChoosableClasses)
        {
            if (soldierClass == null || !Relationships.TryGetClassTag(soldierClass, out CaptainTag tag)) continue;
            if (Array.IndexOf(tower.Captain.likes, tag) < 0) continue;
            foreach (Soldier soldier in tower.Soldiers)
                if (soldier.Class != soldierClass) return soldierClass;
        }
        return null;
    }

    public void Answer(Request request, RequestAnswer answer)
    {
        if (!pending.Remove(request)) return;
        ShowNext();
        if (request.Tower == null) return;
        Tower tower = request.Tower;
        if (answer == RequestAnswer.No)
        {
            float penalty = RefusalPenalty * RefusalWeight(request.Kind) * tower.Captain.refusalTemper;
            relationships.Change(tower, -penalty, "was refused");
            return;
        }

        // A move or a special is all or nothing, so Some counts as Yes.
        if (IsYesNo(request.Kind)) answer = RequestAnswer.Yes;
        bool full = answer == RequestAnswer.Yes;
        Grant(request, full);
        relationships.Change(tower, full ? ApprovalReward : ApprovalReward * 0.5f, full ? "got their way" : "got some of their way");
    }

    // Requests that can only be answered Yes or No.
    public static bool IsYesNo(RequestKind kind) => kind is RequestKind.Relocate or RequestKind.UseSpecial;

    // How much a refusal stings, by request.
    static float RefusalWeight(RequestKind kind) => kind switch
    {
        RequestKind.Relocate => 0.75f,
        RequestKind.NewMix => 1.25f,
        _ => 1f,
    };

    // Yes grants the request in full; Some grants half of it.
    void Grant(Request request, bool full)
    {
        Tower tower = request.Tower;
        switch (request.Kind)
        {
            case RequestKind.MoreDraw:
                tower.SetDrawRate(full ? DrawRate.High : (DrawRate)Mathf.Min((int)tower.DrawRate + 1, (int)DrawRate.High));
                break;
            case RequestKind.Relocate:
                MoveTowardSpawner(tower);
                break;
            case RequestKind.NewMix:
                // All slots switch for free, or half of them (rounded up).
                int switches = full ? tower.Soldiers.Count : (tower.Soldiers.Count + 1) / 2;
                for (int slot = 0; slot < tower.Soldiers.Count && switches > 0; slot++)
                    if (tower.SetSlotClass(slot, request.WantedClass, free: true)) switches--;
                break;
            case RequestKind.UseSpecial:
                // Fires now, or the moment it can if nothing is in reach yet.
                if (!tower.TryUseAbility()) tower.FireAbilityWhenAble(AutoFireTimeoutSeconds);
                break;
        }
    }
}
