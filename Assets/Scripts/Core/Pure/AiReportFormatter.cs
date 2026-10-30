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
    public int goldSpent;
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
    public float approvalEnd;
    public bool departed;
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
    // "Victory", "Defeat", "Stopped" or "Stalled".
    public string result = "";
    public List<string> route = new();
    public List<AiMatchRow> matches = new();
    public List<AiCaptainRow> captains = new();
    public Dictionary<string, float> damageByElement = new();
    public List<AiDecisionRow> decisions = new();
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

    public const string CsvHeader = "run,seed,result,match,node,kind,layer,layout,match_result,wave,seconds,kills,leaks,casualties,abilities,damage,gold_start,gold_earned,gold_bonus,gold_spent,gold_end,health_end,kills_by";

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
                        F(w.damage), w.goldStart.ToString(), w.goldEarned.ToString(), w.goldBonus.ToString(), w.goldSpent.ToString(),
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
        md.Append($"- Victories: {wins} of {runs.Count} ({100f * wins / runs.Count:0}%)\n");
        md.Append($"- Average matches fought: {F((float)runs.Average(r => r.FoughtMatches.Count()))}\n");
        md.Append($"- Average waves cleared: {F((float)runs.Average(r => r.WavesCleared))}\n");
        md.Append($"- Average kills / leaks / casualties: {F((float)runs.Average(r => r.Kills))} / {F((float)runs.Average(r => r.Leaks))} / {F((float)runs.Average(r => r.Casualties))}\n");
        md.Append($"- Average final health: {F((float)runs.Average(r => r.finalHealth))}\n\n");

        md.Append("| Run | Seed | Result | Matches | Waves cleared | Final health | Final Gold |\n|---|---|---|---|---|---|---|\n");
        foreach (AiRunRecord run in runs)
            md.Append($"| {run.index} | {run.seed} | {run.result} | {run.FoughtMatches.Count()} | {run.WavesCleared} | {run.finalHealth}/{run.maxHealth} | {run.finalGold} |\n");
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
    }

    static void AppendRun(StringBuilder md, AiRunRecord run)
    {
        md.Append($"## Run {run.index}: {run.result}\n\n");
        md.Append($"- Seed: {run.seed}\n");
        md.Append($"- Starting party: {string.Join(", ", run.startParty)}\n");
        md.Append($"- Route: {(run.route.Count > 0 ? string.Join(" > ", run.route) : "none")}\n");
        md.Append($"- Final health {run.finalHealth}/{run.maxHealth}, Gold {run.finalGold}\n");
        md.Append($"- Time: {F(run.gameSeconds)}s game, {F(run.realSeconds)}s real\n");
        md.Append($"- Totals: {run.Kills} kills, {run.Leaks} leaks, {run.Casualties} casualties, {run.WavesCleared} waves cleared\n\n");

        md.Append("### Matches\n\n| # | Node | Kind | Layer | Result | Waves | Kills | Leaks | Casualties | Health | Gold | Towers |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (AiMatchRow m in run.matches)
            md.Append($"| {m.index} | {m.nodeId} | {m.kind} | {m.layer} | {m.result} | {m.wavesCleared}/{m.waveCount} | {m.kills} | {m.leaks} | {m.casualties} | {m.healthStart} > {m.healthEnd} | {m.goldStart} > {m.goldEnd} | {m.towersPlaced} |\n");
        md.Append('\n');

        md.Append("### Waves\n\n| Match | Wave | Seconds | Kills | Leaks | Casualties | Specials | Damage | Gold in | Gold spent | Health | Kills by |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        foreach (AiMatchRow m in run.matches)
            foreach (AiWaveRow w in m.waves)
                md.Append($"| {m.index} | {w.wave} | {F(w.seconds)} | {w.kills} | {w.leaks} | {w.casualties} | {w.abilities} | {F(w.damage)} | {w.goldEarned + w.goldBonus} | {w.goldSpent} | {w.healthEnd} | {string.Join(", ", w.killsByCaptain.Select(p => $"{p.Key} {p.Value}"))} |\n");
        md.Append('\n');

        md.Append("### Captains\n\n| Captain | Element | Role | Level | XP | Kills | Damage | Specials | Approval |\n|---|---|---|---|---|---|---|---|---|\n");
        foreach (AiCaptainRow c in run.captains)
            md.Append($"| {c.name}{(c.departed ? " (walked out)" : "")} | {c.element} | {c.role} | {c.level} | {c.xp} | {c.kills} | {F(c.damage)} | {c.abilities} | {F(c.approvalEnd)} |\n");
        md.Append('\n');

        if (run.damageByElement.Count > 0)
        {
            md.Append("### Damage by element\n\n| Element | Damage |\n|---|---|\n");
            foreach (var pair in run.damageByElement.OrderByDescending(p => p.Value)) md.Append($"| {pair.Key} | {F(pair.Value)} |\n");
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
