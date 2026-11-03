using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// The profile, saved to a JSON file in persistentDataPath: the roster's captains whole, the soldier
// inventory with its equips, Gold and unlocked locations. Written with every run save, and when a
// run ends, so it always agrees with the run save beside it.
public static class ProfileSave
{
    // Saves from other versions are ignored rather than migrated.
    const int Version = 1;

    [Serializable]
    class SaveFile
    {
        public int version;
        public int gold;
        public int locationsUnlocked = 1;
        public List<Captain> roster = new();
        public SoldierInventory soldiers;
        public int shopSeed;
        public List<int> soldSoldiers = new();
        public List<int> soldCaptains = new();
        public List<PackedGift> packedGifts = new();
    }

    [Serializable]
    class PackedGift
    {
        public string gift;
        public int count;
    }

    // Where the AI Run keeps its own profile, so it never touches the player's.
    public static string PathOverride { get; set; }

    static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "profile.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => PathOverride = null;

    public static bool Exists => File.Exists(FilePath);

    public static void Save(Profile profile)
    {
        if (profile == null) return;
        var file = new SaveFile
        {
            version = Version,
            gold = profile.Gold,
            locationsUnlocked = profile.LocationsUnlocked,
            soldiers = profile.Soldiers,
            shopSeed = profile.ShopSeed,
        };
        file.roster.AddRange(profile.Roster);
        file.soldSoldiers.AddRange(profile.SoldSoldiers);
        file.soldCaptains.AddRange(profile.SoldCaptains);
        foreach (KeyValuePair<string, int> entry in profile.PackedGifts)
            file.packedGifts.Add(new PackedGift { gift = entry.Key, count = entry.Value });
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(file, true));
        }
        catch (IOException e)
        {
            Debug.LogWarning($"Couldn't save the profile to {FilePath}: {e.Message}");
        }
    }

    // Restores the saved profile as Profile.Current, reconnecting captains to their templates.
    // Returns false, leaving the current profile alone, if there is no save or it can't be used.
    public static bool TryLoad(IEnumerable<CaptainData> templates)
    {
        if (!Exists) return false;
        SaveFile file;
        try
        {
            file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Couldn't read the profile from {FilePath}, starting fresh: {e.Message}");
            return false;
        }
        if (file == null || file.version != Version) return false;

        var templateByName = new Dictionary<string, CaptainData>();
        foreach (CaptainData template in templates)
            if (template != null) templateByName[template.name] = template;
        Profile profile = Profile.Resume(file.gold, file.locationsUnlocked, file.soldiers);
        profile.RestockShop(file.shopSeed);
        profile.SoldSoldiers.AddRange(file.soldSoldiers ?? new List<int>());
        profile.SoldCaptains.AddRange(file.soldCaptains ?? new List<int>());
        foreach (PackedGift entry in file.packedGifts ?? new List<PackedGift>())
            if (entry != null) profile.PackGift(entry.gift, entry.count);
        foreach (Captain captain in file.roster ?? new List<Captain>())
        {
            if (captain == null || string.IsNullOrEmpty(captain.id) || !templateByName.TryGetValue(captain.templateName ?? "", out CaptainData template)) continue;
            captain.Bind(template);
            profile.TryAdd(captain);
        }
        // Soldiers equipped to a captain who didn't load go back to the inventory.
        foreach (SoldierItem soldier in profile.Soldiers.Items)
            if (soldier.IsEquipped && profile.Find(soldier.equippedTo) == null) profile.Soldiers.Unequip(soldier.id);
        return true;
    }

    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
