using System;
using System.Collections.Generic;
using UnityEngine;

// Judges captains' likes and dislikes and changes their approval (GDD relationship system).
// Play-style tags are judged at the end of each wave and the soldier mix at the end of the match,
// along with a bonus for winning. A captain whose approval falls 20 below its best this run walks
// out at the end of the match (never mid-match) and sits out the rest of the run.
// Added to the match object at runtime by MatchManager, like CameraShake.
public class Relationships : MonoBehaviour
{
    const float TagApproval = 3f;
    const float MixApprovalPerSoldier = 2f;
    const float VictoryApproval = 5f;
    // While a captain dislikes more of its soldiers than it likes, approval gains are halved
    // (GDD: soldier-mix disapproval slows bond growth).
    const float DisapprovedMixGainMultiplier = 0.5f;
    const int MaxMessages = 5;
    const float MessageSeconds = 6f;

    // What happened to one captain during the current wave.
    class WaveRecord
    {
        public int casualties;
        public int kills;
        public bool usedAbility;
        public bool hurtBoss;
    }

    readonly Dictionary<CaptainData, WaveRecord> records = new();
    readonly List<(string text, float until)> messages = new();
    readonly Dictionary<CaptainData, float> approvalThisMatch = new();
    int waveLeaks;
    bool waveSold;
    bool waveSpent;

    // Total approval change for a captain during this match, for the results screen.
    public float ApprovalChangeThisMatch(CaptainData captain) => approvalThisMatch.TryGetValue(captain, out float change) ? change : 0f;

    // Captains who walked out at the end of this match.
    public List<CaptainData> Walkouts { get; } = new();

    public IEnumerable<string> RecentMessages
    {
        get
        {
            foreach (var message in messages)
                if (Time.unscaledTime < message.until) yield return message.text;
        }
    }

    void OnEnable()
    {
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.WaveEnded += OnWaveEnded;
        GameEvents.MatchEnded += OnMatchEnded;
        GameEvents.EnemyLeaked += OnEnemyLeaked;
        GameEvents.SoldierDied += OnSoldierDied;
        GameEvents.TowerSold += OnTowerSold;
        GameEvents.TowerPlaced += OnTowerPlaced;
        GameEvents.AbilityUsed += OnAbilityUsed;
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.BossDamaged += OnBossDamaged;
        GameEvents.GoldSpent += OnGoldSpent;
        GameEvents.ApprovalChanged += OnApprovalChanged;
    }

    void OnDisable()
    {
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveEnded -= OnWaveEnded;
        GameEvents.MatchEnded -= OnMatchEnded;
        GameEvents.EnemyLeaked -= OnEnemyLeaked;
        GameEvents.SoldierDied -= OnSoldierDied;
        GameEvents.TowerSold -= OnTowerSold;
        GameEvents.TowerPlaced -= OnTowerPlaced;
        GameEvents.AbilityUsed -= OnAbilityUsed;
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.BossDamaged -= OnBossDamaged;
        GameEvents.GoldSpent -= OnGoldSpent;
        GameEvents.ApprovalChanged -= OnApprovalChanged;
    }

    // Changes a captain's approval, halving gains while its tower's soldier mix is disapproved.
    // Every approval change in a match goes through here so the message log stays complete.
    public void Change(Tower tower, float delta, string reason)
    {
        if (delta > 0f && MixScore(tower) < 0) delta *= DisapprovedMixGainMultiplier;
        float applied = ApprovalStore.Change(tower.Captain, delta);
        if (applied == 0f) return;
        approvalThisMatch[tower.Captain] = ApprovalChangeThisMatch(tower.Captain) + applied;
        Post($"{tower.Captain.displayName} {reason} {applied:+0.#;-0.#}");
    }

    void Post(string text)
    {
        messages.Add((text, Time.unscaledTime + MessageSeconds));
        if (messages.Count > MaxMessages) messages.RemoveAt(0);
    }

    WaveRecord RecordFor(CaptainData captain)
    {
        if (!records.TryGetValue(captain, out WaveRecord record))
            records[captain] = record = new WaveRecord();
        return record;
    }

    void OnWaveStarted(int wave)
    {
        records.Clear();
        waveLeaks = 0;
        waveSold = false;
        waveSpent = false;
    }

    void OnEnemyLeaked(Enemy enemy) => waveLeaks++;
    void OnTowerSold(Tower tower) => waveSold = true;
    void OnSoldierDied(Soldier soldier) => RecordFor(soldier.Tower.Captain).casualties++;
    void OnGoldSpent() => waveSpent = true;
    void OnBossDamaged(Tower tower) => RecordFor(tower.Captain).hurtBoss = true;

    void OnEnemyKilled(Enemy enemy)
    {
        if (enemy.KilledBy != null) RecordFor(enemy.KilledBy.Captain).kills++;
    }
    void OnAbilityUsed(Tower tower) => RecordFor(tower.Captain).usedAbility = true;

    // Starts the walkout baseline from the captain's approval when it first takes the field.
    void OnTowerPlaced(Tower tower) => RunState.Current.BestApprovalThisRun(tower.Captain);
    void OnApprovalChanged(CaptainData captain) => RunState.Current?.BestApprovalThisRun(captain);

    // Each play-style tag a captain likes or dislikes is judged once per wave.
    void OnWaveEnded(int wave)
    {
        int topKills = 0;
        foreach (Tower tower in Tower.All)
            topKills = Mathf.Max(topKills, RecordFor(tower.Captain).kills);

        foreach (Tower tower in Tower.All)
        {
            WaveRecord record = RecordFor(tower.Captain);
            JudgeTag(tower, CaptainTag.Leaks, waveLeaks > 0);
            JudgeTag(tower, CaptainTag.Casualties, record.casualties > 0);
            JudgeTag(tower, CaptainTag.Selling, waveSold);
            JudgeTag(tower, CaptainTag.Ability, record.usedAbility);
            JudgeTag(tower, CaptainTag.Flawless, waveLeaks == 0 && record.casualties == 0);
            JudgeTag(tower, CaptainTag.Spending, waveSpent);
            // Kills goes to whoever took the most kills this wave; ties share it.
            JudgeTag(tower, CaptainTag.Kills, topKills > 0 && record.kills == topKills);
            JudgeTag(tower, CaptainTag.Boss, record.hurtBoss);
        }
        ApprovalStore.Save();
    }

    void JudgeTag(Tower tower, CaptainTag tag, bool happened)
    {
        if (!happened) return;
        if (Array.IndexOf(tower.Captain.likes, tag) >= 0) Change(tower, TagApproval, $"liked {tag}");
        if (Array.IndexOf(tower.Captain.dislikes, tag) >= 0) Change(tower, -TagApproval, $"disliked {tag}");
    }

    void OnMatchEnded(bool won)
    {
        foreach (Tower tower in Tower.All)
        {
            int mix = MixScore(tower);
            if (mix != 0) Change(tower, mix * MixApprovalPerSoldier, mix > 0 ? "liked the soldier mix" : "disliked the soldier mix");
            if (won) Change(tower, VictoryApproval, "enjoyed the victory");
        }

        Walkouts.Clear();
        foreach (Tower tower in Tower.All)
        {
            CaptainData captain = tower.Captain;
            if (!BondMath.HasWalkedOut(RunState.Current.BestApprovalThisRun(captain), ApprovalStore.ApprovalOf(captain)))
                continue;
            RunState.Current.Depart(captain);
            Walkouts.Add(captain);
        }
        ApprovalStore.Save();
    }

    // Liked soldiers in the tower's mix minus disliked ones.
    static int MixScore(Tower tower)
    {
        int score = 0;
        foreach (Soldier soldier in tower.Soldiers)
        {
            if (!TryGetClassTag(soldier.Class, out CaptainTag tag)) continue;
            if (Array.IndexOf(tower.Captain.likes, tag) >= 0) score++;
            if (Array.IndexOf(tower.Captain.dislikes, tag) >= 0) score--;
        }
        return score;
    }

    // Soldier classes map to tags by name: Knight -> Knights, Archer -> Archers, and so on.
    public static bool TryGetClassTag(SoldierClassData soldierClass, out CaptainTag tag) =>
        Enum.TryParse(soldierClass.displayName + "s", out tag) && tag <= CaptainTag.Rogues;

    void OnApplicationQuit() => ApprovalStore.Save();
}
