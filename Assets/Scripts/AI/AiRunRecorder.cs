using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Collects what an AI run does into AiRunRecord rows: damage from GameEvents, wave numbers from
// MatchStats, and the AI's own decisions. Owned by AiPlayer, which outlives each match scene.
public class AiRunRecorder
{
    public AiRunRecord Run { get; private set; }
    public AiMatchRow CurrentMatch { get; private set; }

    readonly Dictionary<string, float> damageByCaptain = new();
    readonly Dictionary<string, int> abilitiesByCaptain = new();
    readonly Dictionary<int, float> damageByWave = new();
    readonly Dictionary<string, float> approvalStartByCaptain = new();
    readonly Dictionary<string, float> approvalGainedByCaptain = new();
    readonly Dictionary<string, float> approvalLostByCaptain = new();
    int currentWave;
    float untypedDamage;
    int nextMatchIndex;
    float runStartReal;
    float runStartGame;
    bool subscribed;

    public string LastDecision { get; private set; } = "";

    public void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        GameEvents.DamageDealt += OnDamage;
        GameEvents.AbilityUsed += OnAbility;
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.TowerPlaced += OnTowerPlaced;
        GameEvents.TowerDisabled += OnTowerDisabled;
        GameEvents.TowerRestored += OnTowerRestored;
        GameEvents.TowerSold += OnTowerSold;
        GameEvents.ApprovalNoticed += OnApprovalNoticed;
        GameEvents.MatchupHit += OnMatchupHit;
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.EnemyLeaked += OnEnemyLeaked;
        GameEvents.SoldierDied += OnSoldierDied;
        GameEvents.SoldierRespawned += OnSoldierRespawned;
    }

    public void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        GameEvents.DamageDealt -= OnDamage;
        GameEvents.AbilityUsed -= OnAbility;
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.TowerPlaced -= OnTowerPlaced;
        GameEvents.TowerDisabled -= OnTowerDisabled;
        GameEvents.TowerRestored -= OnTowerRestored;
        GameEvents.TowerSold -= OnTowerSold;
        GameEvents.ApprovalNoticed -= OnApprovalNoticed;
        GameEvents.MatchupHit -= OnMatchupHit;
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.EnemyLeaked -= OnEnemyLeaked;
        GameEvents.SoldierDied -= OnSoldierDied;
        GameEvents.SoldierRespawned -= OnSoldierRespawned;
    }

    void OnDamage(Tower source, float amount, Element? element)
    {
        string elementName = element.HasValue ? element.Value.ToString() : "Other";
        Run.damageByElement[elementName] = Run.damageByElement.TryGetValue(elementName, out float byElement) ? byElement + amount : amount;
        if (source != null)
        {
            string name = source.Captain.displayName;
            damageByCaptain[name] = damageByCaptain.TryGetValue(name, out float byCaptain) ? byCaptain + amount : amount;
        }
        damageByWave[currentWave] = damageByWave.TryGetValue(currentWave, out float byWave) ? byWave + amount : amount;
        untypedDamage += amount;
    }

    // Typed hits on enemies are moved out of the untyped total as they land, so it ends as burns and zones.
    void OnMatchupHit(bool byParty, DamageType type, float amount, float typeMultiplier, float elementMultiplier)
    {
        if (Run == null) return;
        if (!byParty)
        {
            Run.enemyHits.Add(typeMultiplier, elementMultiplier);
            return;
        }
        Run.partyHits.Add(typeMultiplier, elementMultiplier);
        string key = type.ToString();
        Run.damageByType[key] = Run.damageByType.TryGetValue(key, out float sum) ? sum + amount : amount;
        untypedDamage -= amount;
    }

    void OnEnemyKilled(Enemy enemy)
    {
        if (Run != null && enemy.IsBoss) Run.boss = $"killed in wave {currentWave}";
    }

    void OnEnemyLeaked(Enemy enemy)
    {
        if (Run != null && enemy.IsBoss) Run.boss = $"walked out in wave {currentWave}";
    }

    void OnSoldierDied(Soldier soldier)
    {
        if (Run == null) return;
        string key = soldier.Class.soldierClass.ToString();
        Run.soldierDeathsByClass[key] = Run.soldierDeathsByClass.TryGetValue(key, out int count) ? count + 1 : 1;
    }

    void OnSoldierRespawned(Soldier soldier, int cost) => NoteGold(false, "Respawns (from reserves)", cost);

    // Gold into (income) or out of the pool, by source, for the run's ledger.
    public void NoteGold(bool income, string source, int amount)
    {
        if (Run == null || amount == 0) return;
        var ledger = income ? Run.goldIn : Run.goldOut;
        ledger[source] = ledger.TryGetValue(source, out int sum) ? sum + amount : amount;
    }

    void OnAbility(Tower tower)
    {
        string name = tower.Captain.displayName;
        abilitiesByCaptain[name] = abilitiesByCaptain.TryGetValue(name, out int count) ? count + 1 : 1;
    }

    void OnWaveStarted(int wave) => currentWave = wave;

    void OnTowerPlaced(Tower tower)
    {
        if (CurrentMatch != null) CurrentMatch.towersPlaced++;
    }

    void OnTowerDisabled(Tower tower)
    {
        if (CurrentMatch != null) CurrentMatch.towersDisabled++;
    }

    void OnTowerRestored(Tower tower)
    {
        if (CurrentMatch != null) CurrentMatch.towersRestored++;
    }

    void OnTowerSold(Tower tower)
    {
        if (CurrentMatch != null) CurrentMatch.towersSold++;
    }

    void OnApprovalNoticed(ApprovalNotice notice)
    {
        if (Run == null) return;
        Run.approvalByReason[notice.reason] = Run.approvalByReason.TryGetValue(notice.reason, out float total) ? total + notice.amount : notice.amount;
        var bucket = notice.amount > 0f ? approvalGainedByCaptain : approvalLostByCaptain;
        string name = notice.captain.displayName;
        bucket[name] = bucket.TryGetValue(name, out float sum) ? sum + Mathf.Abs(notice.amount) : Mathf.Abs(notice.amount);
    }

    // A captain's request and the answer the AI gave it.
    public void NoteRequest(RequestKind kind, RequestAnswer answer)
    {
        if (Run == null) return;
        string key = $"{kind}: {answer}";
        Run.requestAnswers[key] = Run.requestAnswers.TryGetValue(key, out int count) ? count + 1 : 1;
    }

    public void BeginRun(int index, int seed, IEnumerable<Captain> party, float realNow, float gameNow, string location = "")
    {
        Run = new AiRunRecord { index = index, seed = seed, location = location, startGold = RunState.Current?.Gold ?? 0 };
        untypedDamage = 0f;
        damageByCaptain.Clear();
        abilitiesByCaptain.Clear();
        approvalStartByCaptain.Clear();
        approvalGainedByCaptain.Clear();
        approvalLostByCaptain.Clear();
        foreach (Captain captain in party)
        {
            Run.startParty.Add(captain.displayName);
            approvalStartByCaptain[captain.displayName] = ApprovalStore.ApprovalOf(captain);
        }
        CurrentMatch = null;
        nextMatchIndex = 1;
        runStartReal = realNow;
        runStartGame = gameNow;
        LastDecision = "";
    }

    public void Decide(float gameTime, string where, string text)
    {
        if (Run == null) return;
        Run.decisions.Add(new AiDecisionRow { seconds = gameTime - runStartGame, where = where, text = text });
        LastDecision = $"{where}: {text}";
    }

    public void NoteRoute(NodeKind kind) => Run.route.Add(kind.ToString());

    // Opens the row for the match being played, once its scene is ready.
    public void BeginMatch(MatchManager match)
    {
        RunState run = RunState.Current;
        MapNode city = match.City;
        CurrentMatch = new AiMatchRow
        {
            index = nextMatchIndex++,
            nodeId = city.id,
            kind = city.kind.ToString(),
            layer = city.layer,
            layout = city.layout,
            waveCount = match.WaveCount,
            healthStart = run.Health,
            goldStart = run.Gold,
        };
        damageByWave.Clear();
        currentWave = 0;
        Run.matches.Add(CurrentMatch);
    }

    // Closes the open match row from the match's own counters and MatchStats' wave records.
    public void EndMatch(MatchManager match, string result)
    {
        AiMatchRow row = CurrentMatch;
        if (row == null) return;
        row.result = result;
        row.wavesCleared = match.WavesCleared;
        row.kills = match.Kills;
        row.leaks = match.Leaks;
        row.casualties = match.Casualties;
        row.healthEnd = RunState.Current.Health;
        row.goldEnd = RunState.Current.Gold;
        if (match.TryGetComponent(out MatchStats stats))
        {
            foreach (MatchStats.WaveRecord w in stats.Waves)
            {
                row.waves.Add(new AiWaveRow
                {
                    wave = w.Wave,
                    seconds = w.Seconds,
                    kills = w.Kills,
                    leaks = w.Leaks,
                    casualties = w.Casualties,
                    abilities = w.AbilitiesUsed,
                    goldStart = w.GoldStart,
                    goldEarned = w.GoldEarned,
                    goldBonus = w.GoldBonus,
                    goldSpent = w.GoldSpent,
                    goldBetween = w.GoldBetween,
                    goldEnd = w.GoldEnd,
                    healthEnd = w.HealthEnd,
                    damage = damageByWave.TryGetValue(w.Wave, out float damage) ? damage : 0f,
                    killsByCaptain = new Dictionary<string, int>(w.KillsByCaptain),
                });
                NoteGold(true, "Kills", w.GoldEarned);
                NoteGold(true, "Wave clears", w.GoldBonus);
            }
        }
        if (result == "Won")
        {
            NoteGold(true, "Win bonus", match.WinGoldEarned);
            NoteGold(true, "Reserve returned on wins", match.ReserveRefunded);
        }
        CurrentMatch = null;
    }

    // A rest, shop or event passed through.
    public void RecordStop(MatchManager match)
    {
        RunState run = RunState.Current;
        Run.matches.Add(new AiMatchRow
        {
            index = nextMatchIndex++,
            nodeId = match.City.id,
            kind = match.City.kind.ToString(),
            layer = match.City.layer,
            layout = match.City.layout,
            result = "Visited",
            healthStart = run.Health,
            healthEnd = run.Health,
            goldStart = run.Gold,
            goldEnd = run.Gold,
        });
    }

    public void FinishRun(string result, float realNow, float gameNow)
    {
        if (Run == null) return;
        Run.result = result;
        if (untypedDamage > 0.5f) Run.damageByType["Untyped"] = untypedDamage;
        RunState run = RunState.Current;
        if (run != null)
        {
            Run.finalHealth = run.Health;
            Run.maxHealth = run.MaxHealth;
            Run.finalGold = run.Gold;
            // Captains who walked out have left the party, so they're added back for the report.
            foreach (Captain captain in run.Party.Concat(run.Departed.Where(c => !run.Party.Contains(c))))
            {
                int kills = 0;
                foreach (AiMatchRow match in Run.matches)
                    foreach (AiWaveRow wave in match.waves)
                        if (wave.killsByCaptain.TryGetValue(captain.displayName, out int k)) kills += k;
                int xp = run.XpOf(captain);
                Run.captains.Add(new AiCaptainRow
                {
                    name = captain.displayName,
                    element = captain.element.ToString(),
                    role = captain.ClassName,
                    kills = kills,
                    damage = damageByCaptain.TryGetValue(captain.displayName, out float damage) ? damage : 0f,
                    abilities = abilitiesByCaptain.TryGetValue(captain.displayName, out int abilities) ? abilities : 0,
                    level = Mathf.Max(captain.level, LevelMath.LevelForXp(xp)),
                    xp = xp,
                    approvalStart = approvalStartByCaptain.TryGetValue(captain.displayName, out float start) ? start : 0f,
                    approvalEnd = ApprovalStore.ApprovalOf(captain),
                    approvalGained = approvalGainedByCaptain.TryGetValue(captain.displayName, out float gained) ? gained : 0f,
                    approvalLost = approvalLostByCaptain.TryGetValue(captain.displayName, out float lost) ? lost : 0f,
                    bondRank = ApprovalStore.RankOf(captain),
                    departed = run.HasDeparted(captain),
                });
            }
        }
        Run.realSeconds = realNow - runStartReal;
        Run.gameSeconds = gameNow - runStartGame;
    }
}
