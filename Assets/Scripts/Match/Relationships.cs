using System;
using System.Collections.Generic;
using UnityEngine;

// One approval change, for the HUD to show: who, why ("liked Kills") and by how much.
public readonly struct ApprovalNotice
{
    public readonly Captain captain;
    public readonly string reason;
    public readonly float amount;

    public ApprovalNotice(Captain captain, string reason, float amount)
    {
        this.captain = captain;
        this.reason = reason;
        this.amount = amount;
    }
}

// One captain level-up, for the HUD to show. Levels gained at once make a single notice.
public readonly struct LevelNotice
{
    public readonly Captain captain;
    public readonly int previousLevel;
    public readonly int level;

    public LevelNotice(Captain captain, int previousLevel, int level)
    {
        this.captain = captain;
        this.previousLevel = previousLevel;
        this.level = level;
    }
}

// Judges captains' likes and dislikes and changes their approval (GDD relationship system).
// Play-style tags are judged at the end of each wave and the soldier mix at the end of the match,
// along with a bonus for winning. A captain whose approval falls 20 below its best this run walks
// out at the end of the match (never mid-match) and sits out the rest of the run.
// Added to the match object at runtime by MatchManager, like CameraShake.
public class Relationships : MonoBehaviour
{
    // Likes pay less than dislikes cost, so approval climbs slowly but a neglected captain still sours.
    const float TagLikeApproval = 1.5f;
    const float TagDislikeApproval = 3f;
    const float CasualtiesDislikeApproval = 1.5f;
    const float MixApprovalPerSoldier = 2f;
    const float VictoryApproval = 5f;
    // While a captain dislikes more of its soldiers than it likes, approval gains are halved
    // (GDD: soldier-mix disapproval slows bond growth).
    const float DisapprovedMixGainMultiplier = 0.5f;

    // What happened to one captain during the current wave.
    class WaveRecord
    {
        public int casualties;
        public int kills;
        public bool usedAbility;
        public bool hurtBoss;
    }

    readonly Dictionary<Captain, WaveRecord> records = new();
    readonly Queue<ApprovalNotice> notices = new();
    readonly Queue<LevelNotice> levelNotices = new();
    readonly Dictionary<Captain, float> approvalThisMatch = new();
    int waveLeaks;
    bool waveSold;
    bool waveSpent;

    // Total approval change for a captain during this match, for the results screen.
    public float ApprovalChangeThisMatch(Captain captain) => approvalThisMatch.TryGetValue(captain, out float change) ? change : 0f;

    // Captains who walked out at the end of this match.
    public List<Captain> Walkouts { get; } = new();

    // Takes the next approval change the HUD hasn't shown yet.
    public bool TryTakeNotice(out ApprovalNotice notice) => notices.TryDequeue(out notice);

    // Takes the next level-up the HUD hasn't shown yet.
    public bool TryTakeLevelNotice(out LevelNotice notice) => levelNotices.TryDequeue(out notice);

    void OnCaptainLeveledUp(Tower tower, int previousLevel) =>
        levelNotices.Enqueue(new LevelNotice(tower.Captain, previousLevel, tower.Level));

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
        GameEvents.CaptainLeveledUp += OnCaptainLeveledUp;
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
        GameEvents.CaptainLeveledUp -= OnCaptainLeveledUp;
    }

    // Changes a captain's approval, halving gains while its tower's soldier mix is disapproved.
    // Every approval change in a match goes through here so the message log stays complete.
    public void Change(Tower tower, float delta, string reason)
    {
        if (delta > 0f && MixScore(tower) < 0) delta *= DisapprovedMixGainMultiplier;
        float applied = ApprovalStore.Change(tower.Captain, delta);
        if (applied == 0f) return;
        approvalThisMatch[tower.Captain] = ApprovalChangeThisMatch(tower.Captain) + applied;
        var notice = new ApprovalNotice(tower.Captain, reason, applied);
        notices.Enqueue(notice);
        GameEvents.RaiseApprovalNoticed(notice);
    }

    WaveRecord RecordFor(Captain captain)
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
    void OnApprovalChanged(Captain captain) => RunState.Current?.BestApprovalThisRun(captain);

    // Each play-style tag a captain likes or dislikes is judged once per wave.
    void OnWaveEnded(int wave)
    {
        int topKills = 0;
        int partyCasualties = 0;
        foreach (Tower tower in Tower.All)
        {
            WaveRecord counted = RecordFor(tower.Captain);
            topKills = Mathf.Max(topKills, counted.kills);
            partyCasualties += counted.casualties;
        }

        foreach (Tower tower in Tower.All)
        {
            WaveRecord record = RecordFor(tower.Captain);
            JudgeTag(tower, CaptainTag.Leaks, waveLeaks > 0);
            JudgeTag(tower, CaptainTag.Casualties, record.casualties > 0);
            JudgeTag(tower, CaptainTag.Selling, waveSold);
            JudgeTag(tower, CaptainTag.Ability, record.usedAbility);
            JudgeTag(tower, CaptainTag.Flawless, BondMath.IsFlawlessWave(record.kills, partyCasualties, waveLeaks));
            JudgeTag(tower, CaptainTag.Spending, waveSpent);
            // Kills goes to whoever took the most kills this wave; ties share it.
            JudgeTag(tower, CaptainTag.Kills, topKills > 0 && record.kills == topKills);
            JudgeTag(tower, CaptainTag.Boss, record.hurtBoss);
        }
    }

    void JudgeTag(Tower tower, CaptainTag tag, bool happened)
    {
        if (!happened) return;
        if (Array.IndexOf(tower.Captain.likes, tag) >= 0) Change(tower, TagLikeApproval, $"liked {tag}");
        if (Array.IndexOf(tower.Captain.dislikes, tag) >= 0) Change(tower, -DislikeApproval(tag), $"disliked {tag}");
    }

    // Soldiers die in most waves, so a captain who dislikes Casualties loses less per wave.
    static float DislikeApproval(CaptainTag tag) => tag == CaptainTag.Casualties ? CasualtiesDislikeApproval : TagDislikeApproval;

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
            Captain captain = tower.Captain;
            if (!BondMath.HasWalkedOut(RunState.Current.BestApprovalThisRun(captain), ApprovalStore.ApprovalOf(captain)))
                continue;
            RunState.Current.Depart(captain);
            Walkouts.Add(captain);
        }
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
}
