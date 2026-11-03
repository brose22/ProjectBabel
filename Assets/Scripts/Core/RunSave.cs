using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// The run in progress, saved to a JSON file in persistentDataPath between matches so the player
// can quit and pick it up again from the world map. The run's captains are saved whole, with
// their XP and bond, and everything else refers to them by id; so do the soldier items' equips.
// A match in progress isn't saved:
// resuming returns to the map before it.
public static class RunSave
{
    [Serializable]
    class CaptainApproval
    {
        public string captain;
        public float approval;
    }

    [Serializable]
    class CaptainRank
    {
        public string captain;
        public int rank;
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
        public List<Captain> captains = new();
        public List<string> party = new();
        public List<string> departed = new();
        public List<string> recruitOffer = new();
        public List<CaptainApproval> bestApproval = new();
        public List<CaptainRank> startRanks = new();
        // Missing or empty in saves made before soldiers were items.
        public SoldierInventory soldiers;
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
            soldiers = run.Soldiers,
        };
        foreach (Captain captain in run.Party) file.party.Add(captain.id);
        foreach (Captain captain in run.Departed) file.departed.Add(captain.id);
        foreach (Captain captain in run.RecruitOffer) file.recruitOffer.Add(captain.id);
        foreach (Captain captain in run.Party.Concat(run.RecruitOffer))
            if (!file.captains.Contains(captain)) file.captains.Add(captain);
        foreach (KeyValuePair<Captain, float> entry in run.AllBestApprovals)
            file.bestApproval.Add(new CaptainApproval { captain = entry.Key.id, approval = entry.Value });
        foreach (KeyValuePair<Captain, int> entry in run.AllStartRanks)
            file.startRanks.Add(new CaptainRank { captain = entry.Key.id, rank = entry.Value });
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

    // Restores the saved run as RunState.Current, reconnecting captains to their templates.
    // Returns false, leaving no run, if there is no save or it can't be used.
    public static bool TryResume(IEnumerable<CaptainData> templates, IEnumerable<GiftData> gifts = null)
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

        var templateByName = new Dictionary<string, CaptainData>();
        foreach (CaptainData template in templates)
            if (template != null) templateByName[template.name] = template;
        var byId = new Dictionary<string, Captain>();
        foreach (Captain captain in file.captains ?? new List<Captain>())
        {
            if (captain == null || string.IsNullOrEmpty(captain.id) || !templateByName.TryGetValue(captain.templateName ?? "", out CaptainData template)) continue;
            captain.Bind(template);
            byId[captain.id] = captain;
        }
        List<Captain> Find(List<string> ids) =>
            ids.FindAll(byId.ContainsKey).ConvertAll(id => byId[id]);

        List<Captain> party = Find(file.party);
        if (party.Count == 0) return false;

        RunState run = RunState.Resume(file.health, file.maxHealth, file.gold, file.matchesPlayed);
        run.SetSoldiers(file.soldiers);
        run.SetParty(party);
        run.SetMap(file.map);
        foreach (Captain captain in Find(file.departed)) run.Depart(captain);
        run.OfferRecruits(Find(file.recruitOffer));
        foreach (CaptainApproval entry in file.bestApproval)
            if (byId.TryGetValue(entry.captain, out Captain captain)) run.SetBestApproval(captain, entry.approval);
        foreach (CaptainRank entry in file.startRanks)
            if (byId.TryGetValue(entry.captain, out Captain captain)) run.SetStartRank(captain, entry.rank);

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
