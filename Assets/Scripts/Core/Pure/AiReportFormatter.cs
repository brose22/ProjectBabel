using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// One wave of one match, as the AI report records it.
public class AiWaveRow
{
    public int wave;
    public float seconds;
    public int kills;
    public int leaks;
    public int casualties;
    public int abilities;
    public int goldStart;
    public int goldEarned;
    public int goldBonus;
    // Reserves handed back by towers sold during the wave; not netted into goldSpent.
    public int goldRefunded;
    public int goldSpent;
    // Drained from the pool between the last wave and this one: reserve top-ups, restores, moves.
    public int goldBetween;
    public int goldEnd;
    public int healthEnd;
    public float damage;
    public Dictionary<string, int> killsByCaptain = new();
}

// One match (or stop) of a run.
public class AiMatchRow
{
    public int index;
    public int nodeId;
    public string kind = "";
    public int layer;
    public int layout;
    // "Won", "Lost" or "Stopped" for a match; "Visited" for a rest, shop or event.
    public string result = "";
    public int wavesCleared;
    public int waveCount;
    public int kills;
    public int leaks;
    public int casualties;
    public int healthStart;
    public int healthEnd;
    public int goldStart;
    public int goldEnd;
    public int towersPlaced;
    public int towersDisabled;
    public int towersRestored;
    public int towersSold;
    public List<AiWaveRow> waves = new();
}

public class AiCaptainRow
{
    public string name = "";
    public string element = "";
    public string role = "";
    public int kills;
    public float damage;
    public int abilities;
    public int level;
    public int xp;
    public float approvalStart;
    public float approvalEnd;
    public float approvalGained;
    public float approvalLost;
    public int bondRank;
    public bool departed;
}

// Hits with a damage type, counted by the matchups they got. A hit can have both an advantage in
// one matchup and a disadvantage in the other.
public class AiMatchupCounts
{
    public int hits;
    public int typeAdvantage;
    public int typeDisadvantage;
    public int elementAdvantage;
    public int elementDisadvantage;

    public void Add(float typeMultiplier, float elementMultiplier)
    {
        hits++;
        if (typeMultiplier > 1f) typeAdvantage++;
        else if (typeMultiplier < 1f) typeDisadvantage++;
        if (elementMultiplier > 1f) elementAdvantage++;
        else if (elementMultiplier < 1f) elementDisadvantage++;
    }

    public void Add(AiMatchupCounts other)
    {
        hits += other.hits;
        typeAdvantage += other.typeAdvantage;
        typeDisadvantage += other.typeDisadvantage;
        elementAdvantage += other.elementAdvantage;
        elementDisadvantage += other.elementDisadvantage;
    }
}

public class AiDecisionRow
{
    public float seconds;
    public string where = "";
    public string text = "";
}

// One whole run: everything the report says about it.
public class AiRunRecord
{
    public int index;
    public int seed;
    public List<string> startParty = new();
    // The location the run was played at, by display name; empty for single matches.
    public string location = "";
    // "Victory", "Defeat", "Stopped" or "Stalled".
    public string result = "";
    // Anything worth saying about how the run ended.
    public string note = "";
    public List<string> route = new();
    public List<AiMatchRow> matches = new();
    public List<AiCaptainRow> captains = new();
    public Dictionary<string, float> damageByElement = new();
    // Damage the party dealt by damage type; "Untyped" is burns, zones and anything else without one.
    public Dictionary<string, float> damageByType = new();
    // Matchups of the party's hits on enemies, and of enemies' hits on soldiers and towers.
    public AiMatchupCounts partyHits = new();
    public AiMatchupCounts enemyHits = new();
    public Dictionary<string, int> soldierDeathsByClass = new();
    // Gold into and out of the pool by source ("Kills", "Respawns"). Respawns are paid from the
    // towers' reserves, which were drawn from the pool; unspent reserve comes back on a win.
    public Dictionary<string, int> goldIn = new();
    public Dictionary<string, int> goldOut = new();
    public int startGold;
    // What became of the enemy base's boss ("killed in wave 12", "walked out in wave 10"); empty when never met.
    public string boss = "";
    public List<AiDecisionRow> decisions = new();
    // Approval changed by each reason ("liked Kills", "enjoyed the victory"), summed over the run.
    public Dictionary<string, float> approvalByReason = new();
    // Captains' requests and how the AI answered, counted as "MoreDraw: Yes".
    public Dictionary<string, int> requestAnswers = new();
    // The same answers counted by the asking captain's trait ("Greedy: No"), the requests that evaluated as bad,
    // how many of those were granted, and how many refusals were later proven right.
    public Dictionary<string, int> requestsByTrait = new();
    public int badRequests;
    public int badRequestsGranted;
    public int paybacks;
    public int finalHealth;
    public int maxHealth;
    public int finalGold;
    public float realSeconds;
    public float gameSeconds;

    public IEnumerable<AiMatchRow> FoughtMatches => matches.Where(m => m.result != "Visited");
    public int Kills => FoughtMatches.Sum(m => m.kills);
    public int Leaks => FoughtMatches.Sum(m => m.leaks);
    public int Casualties => FoughtMatches.Sum(m => m.casualties);
    public int WavesCleared => FoughtMatches.Sum(m => m.wavesCleared);
    public bool Won => result == "Victory";
}

// Writes AI run records as Markdown and CSV text. Pure, so the layout can be tested.
public static class AiReportFormatter
{
    static string F(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    public static string CsvField(string value)
    {
        value ??= "";
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    public const string CsvHeader = "run,seed,result,match,node,kind,layer,layout,match_result,wave,seconds,kills,leaks,casualties,abilities,damage,gold_start,gold_earned,gold_bonus,gold_refunded,gold_spent,gold_between,gold_end,health_end,kills_by";

    // One row per wave per run.
    public static string Csv(IReadOnlyList<AiRunRecord> runs)
    {
        var csv = new StringBuilder(CsvHeader).Append('\n');
        foreach (AiRunRecord run in runs)
            foreach (AiMatchRow match in run.matches)
                foreach (AiWaveRow w in match.waves)
                {
                    string kills = string.Join("; ", w.killsByCaptain.Select(p => $"{p.Key} {p.Value}"));
                    csv.Append(string.Join(",", new[]
                    {
                        run.index.ToString(), run.seed.ToString(), run.result, match.index.ToString(), match.nodeId.ToString(),
                        match.kind, match.layer.ToString(), match.layout.ToString(), match.result, w.wave.ToString(),
                        F(w.seconds), w.kills.ToString(), w.leaks.ToString(), w.casualties.ToString(), w.abilities.ToString(),
                        F(w.damage), w.goldStart.ToString(), w.goldEarned.ToString(), w.goldBonus.ToString(), w.goldRefunded.ToString(), w.goldSpent.ToString(), w.goldBetween.ToString(),
                        w.goldEnd.ToString(), w.healthEnd.ToString(), CsvField(kills),
                    })).Append('\n');
                }
        return csv.ToString();
    }

    public static string Markdown(IReadOnlyList<AiRunRecord> runs, string generated)
    {
        var md = new StringBuilder();
        md.Append("# AI Run report\n\n");
        md.Append($"Generated {generated}. {runs.Count} run{(runs.Count == 1 ? "" : "s")}; the seed of each run is listed so it can be replayed.\n\n");
        if (runs.Count > 1) AppendAggregate(md, runs);
        foreach (AiRunRecord run in runs) AppendRun(md, run);
        return md.ToString();
    }

    static void AppendAggregate(StringBuilder md, IReadOnlyList<AiRunRecord> runs)
    {
        int wins = runs.Count(r => r.Won);
        md.Append("## Summary across runs\n\n");
        string location = runs[0].location;
        if (!string.IsNullOrEmpty(location)) md.Append($"- Location: {location}\n");
        md.Append($"- Victories: {wins} of {runs.Count} ({100f * wins / runs.Count:0}%)\n");
        md.Append($"- Average matches fought: {F((float)runs.Average(r => r.FoughtMatches.Count()))}\n");
        md.Append($"- Average waves cleared: {F((float)runs.Average(r => r.WavesCleared))}\n");
        md.Append($"- Average kills / leaks / casualties: {F((float)runs.Average(r => r.Kills))} / {F((float)runs.Average(r => r.Leaks))} / {F((float)runs.Average(r => r.Casualties))}\n");
        md.Append($"- Average final health: {F((float)runs.Average(r => r.finalHealth))}\n\n");

        md.Append("| Run | Seed | Result | Matches | Waves cleared | Final health | Final Gold | Boss |\n|---|---|---|---|---|---|---|---|\n");
        foreach (AiRunRecord run in runs)
            md.Append($"| {run.index} | {run.seed} | {run.result} | {run.FoughtMatches.Count()} | {run.WavesCleared} | {run.finalHealth}/{run.maxHealth} | {run.finalGold} | {run.boss} |\n");
        md.Append('\n');

        var lostAt = runs.Where(r => r.result == "Defeat").Select(r => r.FoughtMatches.LastOrDefault())
            .Where(m => m != null).GroupBy(m => m.layer).OrderBy(g => g.Key).ToList();
        if (lostAt.Count > 0)
            md.Append("Defeats by layer: " + string.Join(", ", lostAt.Select(g => $"layer {g.Key} x{g.Count()}")) + "\n\n");

        var perCaptain = runs.SelectMany(r => r.captains).GroupBy(c => c.name).OrderByDescending(g => g.Sum(c => c.damage)).ToList();
        if (perCaptain.Count > 0)
        {
            md.Append("### Captains across runs\n\n| Captain | Runs | Kills | Damage | Specials used |\n|---|---|---|---|---|\n");
            foreach (var g in perCaptain)
                md.Append($"| {g.Key} | {g.Count()} | {g.Sum(c => c.kills)} | {F(g.Sum(c => c.damage))} | {g.Sum(c => c.abilities)} |\n");
            md.Append('\n');
        }

        var perElement = runs.SelectMany(r => r.damageByElement).GroupBy(p => p.Key).OrderByDescending(g => g.Sum(p => p.Value)).ToList();
        if (perElement.Count > 0)
        {
            md.Append("### Damage by element across runs\n\n| Element | Damage |\n|---|---|\n");
            foreach (var g in perElement) md.Append($"| {g.Key} | {F(g.Sum(p => p.Value))} |\n");
            md.Append('\n');
        }
        AppendTuning(md, runs);
    }

    static string Share(float part, float whole) => whole > 0f ? (100f * part / whole).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";

    // Damage by type, matchup hits, soldier deaths and the Gold ledger, summed (or averaged per
    // run) over the runs given.
    static void AppendTuning(StringBuilder md, IReadOnlyList<AiRunRecord> runs)
    {
        var byType = runs.SelectMany(r => r.damageByType).GroupBy(p => p.Key).OrderByDescending(g => g.Sum(p => p.Value)).ToList();
        float total = byType.Sum(g => g.Sum(p => p.Value));
        if (byType.Count > 0)
        {
            md.Append("### Damage by damage type\n\n| Type | Damage | Share |\n|---|---|---|\n");
            foreach (var g in byType) md.Append($"| {g.Key} | {F(g.Sum(p => p.Value))} | {Share(g.Sum(p => p.Value), total)} |\n");
            md.Append('\n');
        }

        var party = new AiMatchupCounts();
        var enemy = new AiMatchupCounts();
        foreach (AiRunRecord run in runs)
        {
            party.Add(run.partyHits);
            enemy.Add(run.enemyHits);
        }
        if (party.hits + enemy.hits > 0)
        {
            md.Append("### Matchup hits\n\n| Hits by | Hits | Type advantage | Type disadvantage | Element advantage | Element disadvantage |\n|---|---|---|---|---|---|\n");
            foreach (var (name, c) in new[] { ("Party", party), ("Enemies", enemy) })
                md.Append($"| {name} | {c.hits} | {Share(c.typeAdvantage, c.hits)} | {Share(c.typeDisadvantage, c.hits)} | {Share(c.elementAdvantage, c.hits)} | {Share(c.elementDisadvantage, c.hits)} |\n");
            md.Append('\n');
        }

        var deaths = runs.SelectMany(r => r.soldierDeathsByClass).GroupBy(p => p.Key).OrderByDescending(g => g.Sum(p => p.Value)).ToList();
        if (deaths.Count > 0)
        {
            md.Append("### Soldier deaths\n\n| Class | Deaths | Per run |\n|---|---|---|\n");
            foreach (var g in deaths) md.Append($"| {g.Key} | {g.Sum(p => p.Value)} | {F((float)g.Sum(p => p.Value) / runs.Count)} |\n");
            md.Append('\n');
        }

        var goldIn = runs.SelectMany(r => r.goldIn).GroupBy(p => p.Key).ToList();
        var goldOut = runs.SelectMany(r => r.goldOut).GroupBy(p => p.Key).ToList();
        if (goldIn.Count + goldOut.Count > 0)
        {
            md.Append($"### Gold per run (average of {runs.Count})\n\n| Flow | Source | Gold |\n|---|---|---|\n");
            md.Append($"| Start | | {F((float)runs.Average(r => r.startGold))} |\n");
            foreach (var g in goldIn.OrderByDescending(g => g.Sum(p => p.Value))) md.Append($"| In | {g.Key} | {F((float)g.Sum(p => p.Value) / runs.Count)} |\n");
            foreach (var g in goldOut.OrderByDescending(g => g.Sum(p => p.Value))) md.Append($"| Out | {g.Key} | {F((float)g.Sum(p => p.Value) / runs.Count)} |\n");
            md.Append($"| End | | {F((float)runs.Average(r => r.finalGold))} |\n\n");
        }

        AppendRequests(md, runs);
    }

    // Requests across runs: how often they were refused (the target is 20-40%), by kind and by trait, and how the
    // bad ones fared.
    static void AppendRequests(StringBuilder md, IReadOnlyList<AiRunRecord> runs)
    {
        int Count(IEnumerable<Dictionary<string, int>> maps, string suffix) =>
            maps.Sum(m => m.Where(p => p.Key.EndsWith(suffix)).Sum(p => p.Value));
        var byKind = runs.Select(r => r.requestAnswers).ToList();
        int granted = Count(byKind, ": Yes"), refused = Count(byKind, ": No"), asked = granted + refused;
        if (asked == 0) return;
        int bad = runs.Sum(r => r.badRequests), badGranted = runs.Sum(r => r.badRequestsGranted), paybacks = runs.Sum(r => r.paybacks);
        md.Append("### Captain requests across runs\n\n| Stat | Value |\n|---|---|\n");
        md.Append($"| Asked | {asked} ({F((float)asked / runs.Count)} per run) |\n| Granted | {granted} |\n| Refused | {refused} ({F(100f * refused / asked)}%) |\n");
        md.Append($"| Bad requests | {bad} |\n| Bad requests granted | {badGranted} ({F(bad == 0 ? 0f : 100f * badGranted / bad)}%) |\n| Refusals proven right | {paybacks} |\n\n");
        md.Append("| Request | Yes | No |\n|---|---|---|\n");
        foreach (RequestKind kind in Enum.GetValues(typeof(RequestKind)))
            md.Append($"| {kind} | {runs.Sum(r => r.requestAnswers.GetValueOrDefault($"{kind}: Yes"))} | {runs.Sum(r => r.requestAnswers.GetValueOrDefault($"{kind}: No"))} |\n");
        md.Append("\n| Trait | Yes | No |\n|---|---|---|\n");
        for (int t = 0; t < CaptainTraits.Count; t++)
        {
            string name = CaptainTraits.Name((CaptainTrait)t);
            md.Append($"| {name} | {runs.Sum(r => r.requestsByTrait.GetValueOrDefault($"{name}: Yes"))} | {runs.Sum(r => r.requestsByTrait.GetValueOrDefault($"{name}: No"))} |\n");
        }
        md.Append('\n');
    }

    static void AppendRun(StringBuilder md, AiRunRecord run)
    {
        md.Append($"## Run {run.index}: {run.result}\n\n");
        md.Append($"- Seed: {run.seed}\n");
        if (!string.IsNullOrEmpty(run.location)) md.Append($"- Location: {run.location}\n");
        if (!string.IsNullOrEmpty(run.note)) md.Append($"- Note: {run.note}\n");
        md.Append($"- Starting party: {string.Join(", ", run.startParty)}\n");
        md.Append($"- Route: {(run.route.Count > 0 ? string.Join(" > ", run.route) : "none")}\n");
        md.Append($"- Final health {run.finalHealth}/{run.maxHealth}, Gold {run.finalGold}\n");
        md.Append($"- Time: {F(run.gameSeconds)}s game, {F(run.realSeconds)}s real\n");
        md.Append($"- Totals: {run.Kills} kills, {run.Leaks} leaks, {run.Casualties} casualties, {run.WavesCleared} waves cleared\n\n");

        md.Append("### Matches\n\n| # | Node | Kind | Layer | Result | Waves | Kills | Leaks | Casualties | Health | Gold | Towers | Disabled | Restored | Sold |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (AiMatchRow m in run.matches)
            md.Append($"| {m.index} | {m.nodeId} | {m.kind} | {m.layer} | {m.result} | {m.wavesCleared}/{m.waveCount} | {m.kills} | {m.leaks} | {m.casualties} | {m.healthStart} > {m.healthEnd} | {m.goldStart} > {m.goldEnd} | {m.towersPlaced} | {m.towersDisabled} | {m.towersRestored} | {m.towersSold} |\n");
        md.Append('\n');

        md.Append("### Waves\n\n| Match | Wave | Seconds | Kills | Leaks | Casualties | Specials | Damage | Gold in | Refunded | Gold spent | Drawn between | Health | Kills by |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (AiMatchRow m in run.matches)
            foreach (AiWaveRow w in m.waves)
                md.Append($"| {m.index} | {w.wave} | {F(w.seconds)} | {w.kills} | {w.leaks} | {w.casualties} | {w.abilities} | {F(w.damage)} | {w.goldEarned + w.goldBonus} | {w.goldRefunded} | {w.goldSpent} | {w.goldBetween} | {w.healthEnd} | {string.Join(", ", w.killsByCaptain.Select(p => $"{p.Key} {p.Value}"))} |\n");
        md.Append('\n');

        md.Append("### Captains\n\n| Captain | Element | Role | Level | XP | Kills | Damage | Specials | Approval | Gained | Lost | Bond rank |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (AiCaptainRow c in run.captains)
            md.Append($"| {c.name}{(c.departed ? " (walked out)" : "")} | {c.element} | {c.role} | {c.level} | {c.xp} | {c.kills} | {F(c.damage)} | {c.abilities} | {F(c.approvalStart)} > {F(c.approvalEnd)} | +{F(c.approvalGained)} | -{F(c.approvalLost)} | {c.bondRank} |\n");
        md.Append('\n');

        if (run.damageByElement.Count > 0)
        {
            md.Append("### Damage by element\n\n| Element | Damage |\n|---|---|\n");
            foreach (var pair in run.damageByElement.OrderByDescending(p => p.Value)) md.Append($"| {pair.Key} | {F(pair.Value)} |\n");
            md.Append('\n');
        }

        AppendTuning(md, new[] { run });

        if (run.approvalByReason.Count > 0)
        {
            md.Append("### Approval by reason\n\n| Reason | Change |\n|---|---|\n");
            foreach (var pair in run.approvalByReason.OrderByDescending(p => Math.Abs(p.Value)))
                md.Append($"| {pair.Key} | {pair.Value.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture)} |\n");
            md.Append('\n');
        }

        if (run.requestAnswers.Count > 0)
        {
            md.Append("### Captain requests\n\n| Request | Count |\n|---|---|\n");
            foreach (var pair in run.requestAnswers.OrderBy(p => p.Key)) md.Append($"| {pair.Key} | {pair.Value} |\n");
            md.Append('\n');
        }

        md.Append("### Decisions\n\n");
        if (run.decisions.Count == 0) md.Append("None recorded.\n\n");
        else
        {
            foreach (AiDecisionRow d in run.decisions) md.Append($"- `{d.seconds:0}s` {d.where}: {d.text}\n");
            md.Append('\n');
        }
    }
}
