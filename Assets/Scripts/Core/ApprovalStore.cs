using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Each captain's approval and best bond rank. These carry between runs (GDD), so they are saved to
// a JSON file in persistentDataPath rather than living in RunState. Captains are keyed by asset name.
public static class ApprovalStore
{
    [Serializable]
    class Entry
    {
        public string captain;
        public float approval;
        // Bond ranks are never lost once reached, even if approval later falls.
        public int bestRank;
    }

    [Serializable]
    class SaveFile
    {
        public List<Entry> entries = new();
    }

    static Dictionary<string, Entry> entries;

    static string pathOverride;

    // Where the AI Run keeps its own bonds, so it never touches the player's. Switching drops the
    // loaded bonds so the next read comes from the new file.
    public static string PathOverride
    {
        get => pathOverride;
        set
        {
            pathOverride = value;
            entries = null;
        }
    }

    static string FilePath => pathOverride ?? Path.Combine(Application.persistentDataPath, "bonds.json");

    public static float ApprovalOf(CaptainData captain) => Get(captain).approval;

    public static int RankOf(CaptainData captain)
    {
        Entry entry = Get(captain);
        return Mathf.Max(entry.bestRank, BondMath.RankFor(entry.approval));
    }

    // Applies a change, clamped to the approval range, and returns the change actually made.
    // Gains shrink with the captain's current rank, and during a run approval can't climb past
    // the rank after the one the captain started with. Raises ApprovalChanged, and BondRankUp
    // when the change reaches a new rank.
    public static float Change(CaptainData captain, float delta)
    {
        Entry entry = Get(captain);
        float before = entry.approval;
        int rankBefore = RankOf(captain);
        if (delta > 0f)
        {
            delta *= BondMath.GainMultiplier(BondMath.RankFor(before));
            if (RunState.Current != null)
                delta = Mathf.Min(delta, Mathf.Max(0f, BondMath.RunApprovalCap(RunState.Current.StartRankOf(captain)) - before));
        }
        else if (RunState.Current != null) RunState.Current.StartRankOf(captain);
        entry.approval = BondMath.Clamp(before + delta);
        entry.bestRank = Mathf.Max(entry.bestRank, BondMath.RankFor(entry.approval));
        float applied = entry.approval - before;
        if (applied != 0f) GameEvents.RaiseApprovalChanged(captain);
        if (RankOf(captain) > rankBefore) GameEvents.RaiseBondRankUp(captain);
        return applied;
    }

    // Puts a captain's bond at exactly the given rank, outside the limits that gains work under.
    // For starting an AI run at a chosen bond.
    public static void SetRank(CaptainData captain, int rank)
    {
        Entry entry = Get(captain);
        entry.approval = BondMath.ApprovalForRank(rank);
        entry.bestRank = Mathf.Clamp(rank, 0, BondMath.MaxRank);
    }

    public static void Save()
    {
        if (entries == null) return;
        var file = new SaveFile();
        file.entries.AddRange(entries.Values);
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(file, true));
        }
        catch (IOException e)
        {
            Debug.LogWarning($"Couldn't save bonds to {FilePath}: {e.Message}");
        }
    }

    // Wipes every captain's approval and rank, in memory and on disk.
    public static void ResetAll()
    {
        entries = new Dictionary<string, Entry>();
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    static Entry Get(CaptainData captain)
    {
        EnsureLoaded();
        if (!entries.TryGetValue(captain.name, out Entry entry))
        {
            entry = new Entry { captain = captain.name };
            entries[captain.name] = entry;
        }
        return entry;
    }

    static void EnsureLoaded()
    {
        if (entries != null) return;
        entries = new Dictionary<string, Entry>();
        if (!File.Exists(FilePath)) return;
        try
        {
            var file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
            if (file?.entries == null) return;
            foreach (Entry entry in file.entries)
                if (!string.IsNullOrEmpty(entry.captain)) entries[entry.captain] = entry;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Couldn't read bonds from {FilePath}, starting fresh: {e.Message}");
        }
    }

    // Reload from disk on entering Play mode, in case the file changed while domain reload is off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        entries = null;
        pathOverride = null;
    }
}
