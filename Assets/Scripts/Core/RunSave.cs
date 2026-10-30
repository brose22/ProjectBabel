using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// The run in progress, saved to a JSON file in persistentDataPath between matches so the player
// can quit and pick it up again from the world map. Captains are keyed by asset name, like
// ApprovalStore. A match in progress isn't saved: resuming returns to the map before it.
public static class RunSave
{
    [Serializable]
    class CaptainXp
    {
        public string captain;
        public int xp;
    }

    [Serializable]
    class CaptainApproval
    {
        public string captain;
        public float approval;
    }

    // Class asset names per slot; an empty name keeps that slot's default.
    [Serializable]
    class CaptainLoadout
    {
        public string captain;
        public List<string> classes = new();
    }

    [Serializable]
    class GiftStack
    {
        public string gift;
        public int count;
    }

    [Serializable]
    class SaveFile
    {
        public int health;
        public int maxHealth;
        public int gold;
        public int matchesPlayed;
        public List<string> party = new();
        public List<string> departed = new();
        public List<string> recruitOffer = new();
        public List<CaptainXp> xp = new();
        public List<CaptainApproval> bestApproval = new();
        public List<CaptainLoadout> loadouts = new();
        public List<GiftStack> gifts = new();
        public WorldMap map;
    }

    // Where the AI Run keeps its own run, so it never touches the player's save.
    public static string PathOverride { get; set; }

    static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "run.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => PathOverride = null;

    public static bool Exists => File.Exists(FilePath);

    public static void Save(RunState run)
    {
        if (run == null || !run.HasParty || run.Map == null) return;
        var file = new SaveFile
        {
            health = run.Health,
            maxHealth = run.MaxHealth,
            gold = run.Gold,
            matchesPlayed = run.MatchesPlayed,
            map = run.Map,
        };
        foreach (CaptainData captain in run.Party) file.party.Add(captain.name);
        foreach (CaptainData captain in run.Departed) file.departed.Add(captain.name);
        foreach (CaptainData captain in run.RecruitOffer) file.recruitOffer.Add(captain.name);
        foreach (KeyValuePair<CaptainData, int> entry in run.AllXp)
            file.xp.Add(new CaptainXp { captain = entry.Key.name, xp = entry.Value });
        foreach (KeyValuePair<CaptainData, float> entry in run.AllBestApprovals)
            file.bestApproval.Add(new CaptainApproval { captain = entry.Key.name, approval = entry.Value });
        foreach (KeyValuePair<CaptainData, SoldierClassData[]> entry in run.AllLoadouts)
        {
            var loadout = new CaptainLoadout { captain = entry.Key.name };
            foreach (SoldierClassData soldierClass in entry.Value) loadout.classes.Add(soldierClass != null ? soldierClass.name : "");
            file.loadouts.Add(loadout);
        }
        foreach (KeyValuePair<GiftData, int> entry in run.AllGifts)
            file.gifts.Add(new GiftStack { gift = entry.Key.name, count = entry.Value });
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(file, true));
        }
        catch (IOException e)
        {
            Debug.LogWarning($"Couldn't save the run to {FilePath}: {e.Message}");
        }
    }

    // Restores the saved run as RunState.Current, looking captains up in the roster and soldier
    // classes in classes. Returns false, leaving no run, if there is no save or it can't be used.
    public static bool TryResume(IEnumerable<CaptainData> roster, IEnumerable<SoldierClassData> classes = null, IEnumerable<GiftData> gifts = null)
    {
        if (!Exists) return false;
        SaveFile file;
        try
        {
            file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Couldn't read the run from {FilePath}, starting fresh: {e.Message}");
            return false;
        }
        if (file?.map?.nodes == null || file.map.nodes.Count == 0 || file.health <= 0) return false;

        var byName = new Dictionary<string, CaptainData>();
        foreach (CaptainData captain in roster)
            if (captain != null) byName[captain.name] = captain;
        List<CaptainData> Find(List<string> names) =>
            names.FindAll(byName.ContainsKey).ConvertAll(name => byName[name]);

        List<CaptainData> party = Find(file.party);
        if (party.Count == 0) return false;

        RunState run = RunState.Resume(file.health, file.maxHealth, file.gold, file.matchesPlayed);
        run.SetParty(party);
        run.SetMap(file.map);
        foreach (CaptainData captain in Find(file.departed)) run.Depart(captain);
        run.OfferRecruits(Find(file.recruitOffer));
        foreach (CaptainXp entry in file.xp)
            if (byName.TryGetValue(entry.captain, out CaptainData captain)) run.AddXp(captain, entry.xp);
        foreach (CaptainApproval entry in file.bestApproval)
            if (byName.TryGetValue(entry.captain, out CaptainData captain)) run.SetBestApproval(captain, entry.approval);

        var classByName = new Dictionary<string, SoldierClassData>();
        if (classes != null)
            foreach (SoldierClassData soldierClass in classes)
                if (soldierClass != null) classByName[soldierClass.name] = soldierClass;
        foreach (CaptainLoadout entry in file.loadouts ?? new List<CaptainLoadout>())
        {
            if (!byName.TryGetValue(entry.captain, out CaptainData captain)) continue;
            for (int slot = 0; slot < entry.classes.Count; slot++)
                if (classByName.TryGetValue(entry.classes[slot], out SoldierClassData soldierClass))
                    run.SetLoadoutClass(captain, slot, soldierClass);
        }

        var giftByName = new Dictionary<string, GiftData>();
        if (gifts != null)
            foreach (GiftData gift in gifts)
                if (gift != null) giftByName[gift.name] = gift;
        foreach (GiftStack entry in file.gifts ?? new List<GiftStack>())
            if (giftByName.TryGetValue(entry.gift, out GiftData gift)) run.AddGift(gift, entry.count);
        return true;
    }

    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
