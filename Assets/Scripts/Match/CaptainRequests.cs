using System;
using System.Collections.Generic;
using UnityEngine;

public enum RequestKind { MoreDraw, Relocate, NewMix, UseSpecial }

public enum RequestAnswer { No, Some, Yes }

// Captains ask for things during a wave (GDD: up to 2 per wave, often none). The player answers
// No (0%), Some (50%) or Yes (100%): the answer grants that share of the request and of the
// approval reward. Refusing always costs approval, scaled by request and captain; a request left
// unanswered when the wave ends counts as a refusal. Added to the match at runtime.
public class CaptainRequests : MonoBehaviour
{
    public class Request
    {
        public Tower Tower;
        public RequestKind Kind;
        // The soldier class asked for, for NewMix.
        public SoldierClassData WantedClass;

        public string Text => Kind switch
        {
            RequestKind.MoreDraw => "I need more Gold. Turn my draw up to High?",
            RequestKind.Relocate => "This spot isn't working. Let me move?",
            RequestKind.NewMix => $"Give me {WantedClass.displayName}s?",
            _ => $"Let me use {Tower.Captain.ability.displayName} now?",
        };
    }

    const float ApprovalReward = 4f;
    const float RefusalPenalty = 3f;
    // A Some on a special fires it after this delay instead of now.
    const float SomeSpecialDelaySeconds = 5f;
    // Requests arrive between these many seconds into the wave.
    const float EarliestRequestSeconds = 3f;
    const float LatestRequestSeconds = 15f;

    readonly List<Request> pending = new();
    readonly List<float> scheduled = new();
    MatchManager match;
    Relationships relationships;

    public IReadOnlyList<Request> Pending => pending;

    void Awake()
    {
        match = GetComponent<MatchManager>();
        relationships = GetComponent<Relationships>();
    }

    void OnEnable()
    {
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.WaveEnded += OnWaveEnded;
        GameEvents.TowerSold += DropRequestsFor;
    }

    void OnDisable()
    {
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveEnded -= OnWaveEnded;
        GameEvents.TowerSold -= DropRequestsFor;
    }

    // Half the waves bring no request, about a third one, the rest two.
    void OnWaveStarted(int wave)
    {
        scheduled.Clear();
        float roll = UnityEngine.Random.value;
        int count = roll < 0.5f ? 0 : roll < 0.85f ? 1 : 2;
        for (int i = 0; i < count; i++)
            scheduled.Add(Time.time + UnityEngine.Random.Range(EarliestRequestSeconds, LatestRequestSeconds));
    }

    void OnWaveEnded(int wave)
    {
        scheduled.Clear();
        // Unanswered requests count as refusals.
        foreach (Request request in pending.ToArray())
            Answer(request, RequestAnswer.No);
    }

    void DropRequestsFor(Tower tower) => pending.RemoveAll(r => r.Tower == tower);

    void Update()
    {
        for (int i = scheduled.Count - 1; i >= 0; i--)
        {
            if (Time.time < scheduled[i]) continue;
            scheduled.RemoveAt(i);
            TryMakeRequest();
        }
    }

    // Picks a random working captain without a pending request, and a random request it could
    // make. Relocate is always possible, so any such captain has something to ask.
    void TryMakeRequest()
    {
        var candidates = Tower.All.FindAll(t => !t.IsDisabled && !pending.Exists(r => r.Tower == t));
        if (candidates.Count == 0) return;
        Tower tower = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        var options = new List<Request> { new() { Tower = tower, Kind = RequestKind.Relocate } };
        if (tower.DrawRate != DrawRate.High) options.Add(new Request { Tower = tower, Kind = RequestKind.MoreDraw });
        SoldierClassData wanted = LikedClassMissingFrom(tower);
        if (wanted != null) options.Add(new Request { Tower = tower, Kind = RequestKind.NewMix, WantedClass = wanted });
        if (tower.IsSpecialReady) options.Add(new Request { Tower = tower, Kind = RequestKind.UseSpecial });
        pending.Add(options[UnityEngine.Random.Range(0, options.Count)]);
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
        if (!pending.Remove(request) || request.Tower == null) return;
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
