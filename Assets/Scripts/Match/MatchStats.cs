using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Per-wave numbers for tuning: time, Gold earned and spent, kills (and by which captain), leaks,
// casualties, global health and abilities used. Each wave is logged to the Console, and the match
// is written as a CSV to persistentDataPath/stats when it ends. Added to the match at runtime.
public class MatchStats : MonoBehaviour
{
    public class WaveRecord
    {
        public int Wave;
        public float Seconds;
        public int GoldStart;
        public int GoldEnd;
        public int GoldEarned;
        public int Kills;
        public int Leaks;
        public int Casualties;
        public int AbilitiesUsed;
        public int HealthEnd;
        public readonly Dictionary<string, int> KillsByCaptain = new();

        // Spent = what the pool lost beyond what came in: draws into tower reserves, moves, restores.
        public int GoldSpent => GoldStart + GoldEarned - GoldEnd;
    }

    public List<WaveRecord> Waves { get; } = new();
    WaveRecord current;
    float waveStartTime;

    void OnEnable()
    {
        GameEvents.WaveStarted += OnWaveStarted;
        GameEvents.WaveEnded += OnWaveEnded;
        GameEvents.MatchEnded += OnMatchEnded;
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.EnemyLeaked += OnEnemyLeaked;
        GameEvents.SoldierDied += OnSoldierDied;
        GameEvents.AbilityUsed += OnAbilityUsed;
    }

    void OnDisable()
    {
        GameEvents.WaveStarted -= OnWaveStarted;
        GameEvents.WaveEnded -= OnWaveEnded;
        GameEvents.MatchEnded -= OnMatchEnded;
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.EnemyLeaked -= OnEnemyLeaked;
        GameEvents.SoldierDied -= OnSoldierDied;
        GameEvents.AbilityUsed -= OnAbilityUsed;
    }

    void OnWaveStarted(int wave)
    {
        current = new WaveRecord { Wave = wave, GoldStart = RunState.Current.Gold };
        waveStartTime = Time.time;
    }

    void OnEnemyKilled(Enemy enemy)
    {
        if (current == null) return;
        current.Kills++;
        current.GoldEarned += enemy.Data.goldReward;
        string killer = enemy.KilledBy != null ? enemy.KilledBy.Captain.displayName : "other";
        current.KillsByCaptain[killer] = current.KillsByCaptain.TryGetValue(killer, out int count) ? count + 1 : 1;
    }

    void OnEnemyLeaked(Enemy enemy) { if (current != null) current.Leaks++; }
    void OnSoldierDied(Soldier soldier) { if (current != null) current.Casualties++; }
    void OnAbilityUsed(Tower tower) { if (current != null) current.AbilitiesUsed++; }

    void OnWaveEnded(int wave) => CloseWave();

    // A loss ends the match mid-wave, so the wave in progress is closed here too.
    void OnMatchEnded(bool won)
    {
        CloseWave();
        WriteCsv(won);
    }

    void CloseWave()
    {
        if (current == null) return;
        current.Seconds = Time.time - waveStartTime;
        current.GoldEnd = RunState.Current.Gold;
        current.HealthEnd = RunState.Current.Health;
        Waves.Add(current);
        Debug.Log($"[Stats] Wave {current.Wave}: {current.Seconds:0}s, kills {current.Kills}, leaks {current.Leaks}, " +
                  $"casualties {current.Casualties}, Gold +{current.GoldEarned} -{current.GoldSpent} (now {current.GoldEnd}), " +
                  $"health {current.HealthEnd}, abilities {current.AbilitiesUsed}, kills by {FormatKills(current)}");
        current = null;
    }

    static string FormatKills(WaveRecord record)
    {
        var parts = new List<string>();
        foreach (var pair in record.KillsByCaptain) parts.Add($"{pair.Key} {pair.Value}");
        return parts.Count > 0 ? string.Join(", ", parts) : "nobody";
    }

    void WriteCsv(bool won)
    {
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "stats");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"match_{DateTime.Now:yyyyMMdd_HHmmss}_{(won ? "won" : "lost")}.csv");
            var csv = new StringBuilder("wave,seconds,kills,leaks,casualties,gold_start,gold_earned,gold_spent,gold_end,health_end,abilities,kills_by\n");
            foreach (WaveRecord w in Waves)
                csv.AppendLine($"{w.Wave},{w.Seconds:0.0},{w.Kills},{w.Leaks},{w.Casualties},{w.GoldStart},{w.GoldEarned},{w.GoldSpent},{w.GoldEnd},{w.HealthEnd},{w.AbilitiesUsed},\"{FormatKills(w)}\"");
            File.WriteAllText(path, csv.ToString());
            Debug.Log($"[Stats] Match written to {path}");
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[Stats] Couldn't write match stats: {e.Message}");
        }
    }
}
