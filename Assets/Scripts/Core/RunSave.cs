using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// The run in progress, saved to a JSON file in persistentDataPath between matches so the player
// can quit and pick it up again from the world map. The party refers to the profile's captains by
// id; soldiers offered as a match reward, not owned yet, are saved whole. Saving the run saves
// the profile too (ProfileSave), so the two files always agree. A match in progress isn't saved:
// resuming returns to the map before it.
public static class RunSave
{
    // Saves from other versions are ignored rather than migrated.
    // 2: the location is an id instead of an index.
    const int Version = 2;

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
        public int version;
        public string location;
        public int health;
        public int maxHealth;
        public int matchesPlayed;
        // Ids of roster captains; those who walked out have left the roster and aren't kept.
        public List<string> party = new();
        public List<SoldierItem> soldierOffer = new();
        public List<CaptainApproval> bestApproval = new();
        public List<CaptainRank> startRanks = new();
        public List<GiftStack> gifts = new();
        public WorldMap map;
    }

    // Where the AI Run keeps its own run, so it never touches the player's save.
    public static string PathOverride { get; set; }

    static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "current-run.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => PathOverride = null;

    public static bool Exists => File.Exists(FilePath);

    // Saves the profile, then the run once it has a party and a map.
    public static void Save(RunState run)
    {
        if (run == null) return;
        ProfileSave.Save(run.Profile);
        if (!run.HasParty || run.Map == null) return;
        var file = new SaveFile
        {
            version = Version,
            location = run.Location,
            health = run.Health,
            maxHealth = run.MaxHealth,
            matchesPlayed = run.MatchesPlayed,
            map = run.Map,
        };
        foreach (Captain captain in run.Party) file.party.Add(captain.id);
        file.soldierOffer.AddRange(run.SoldierOffer);
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

    // Restores the saved run as RunState.Current on the given profile, finding the party in its
    // roster and reconnecting offered captains to their templates. Returns false, leaving no run,
    // if there is no save or it can't be used.
    public static bool TryResume(Profile profile, IEnumerable<GiftData> gifts = null)
    {
        if (profile == null || !Exists) return false;
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
        if (file == null || file.version != Version || file.map?.nodes == null || file.map.nodes.Count == 0 || file.health <= 0) return false;

        List<Captain> party = (file.party ?? new List<string>()).ConvertAll(profile.Find).FindAll(c => c != null);
        if (party.Count == 0) return false;

        var offer = new List<SoldierItem>();
        foreach (SoldierItem soldier in file.soldierOffer ?? new List<SoldierItem>())
            if (soldier != null && !string.IsNullOrEmpty(soldier.id) && profile.Soldiers.Find(soldier.id) == null) offer.Add(soldier);

        RunState run = RunState.Resume(profile, file.location, file.health, file.maxHealth, file.matchesPlayed);
        run.SetParty(party);
        run.SetMap(file.map);
        run.OfferSoldiers(offer);
        foreach (CaptainApproval entry in file.bestApproval ?? new List<CaptainApproval>())
            if (profile.Find(entry.captain) is { } captain) run.SetBestApproval(captain, entry.approval);
        foreach (CaptainRank entry in file.startRanks ?? new List<CaptainRank>())
            if (profile.Find(entry.captain) is { } captain) run.SetStartRank(captain, entry.rank);

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
