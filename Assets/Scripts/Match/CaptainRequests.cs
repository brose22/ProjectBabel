using System;
using System.Collections.Generic;
using UnityEngine;

public enum RequestKind { MoreDraw, Relocate, NewMix, UseSpecial }

public enum RequestAnswer { No, Some, Yes }

// Captains ask for things when something happens in the match (GDD: up to 2 per wave, often none):
//   MoreDraw   - a soldier fell and the reserve can't pay to bring it back.
//   Relocate   - the captain is badly hurt, or nothing has come within their range for a while.
//   NewMix     - the captain levelled up and a class they like isn't in every slot.
//   UseSpecial - an elite or the boss walked on, or the special just finished charging.
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
    // A Some on a special fires it after this delay instead of now.
    const float SomeSpecialDelaySeconds = 5f;
    // Real-time seconds the player has to answer a shown request.
    public const float ResponseSeconds = 10f;
    // At most this many requests come in a wave.
    const int MaxRequestsPerWave = 2;
    // A captain doesn't ask for the same thing again until this many waves have passed.
    const int CooldownWaves = 2;
    // A captain below this share of health asks to move.
    const float HurtFraction = 0.5f;
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

            if (tower.Health / tower.MaxHealth < HurtFraction)
                TryAsk(tower, RequestKind.Relocate, "They keep hitting me here. Let me move?");

            if (!enemiesOnField || tower.FindClosestEnemy(tower.transform.position) != null)
                watch.LastEnemyInRange = Time.time;
            else if (Time.time - watch.LastEnemyInRange >= IdleSeconds)
                TryAsk(tower, RequestKind.Relocate, "Nothing comes this way. Let me move?");
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

        bool full = answer == RequestAnswer.Yes;
        Grant(request, full);
        relationships.Change(tower, full ? ApprovalReward : ApprovalReward * 0.5f, full ? "got their way" : "got some of their way");
    }

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
                // The captain's next move is free, or half price.
                tower.GrantMoveDiscount(full ? 0f : 0.5f);
                break;
            case RequestKind.NewMix:
                // All slots switch for free, or half of them (rounded up).
                int switches = full ? tower.Soldiers.Count : (tower.Soldiers.Count + 1) / 2;
                for (int slot = 0; slot < tower.Soldiers.Count && switches > 0; slot++)
                    if (tower.SetSlotClass(slot, request.WantedClass, free: true)) switches--;
                break;
            case RequestKind.UseSpecial:
                if (full) tower.TryUseAbility();
                else tower.FireAbilitySoon(SomeSpecialDelaySeconds);
                break;
        }
    }
}
