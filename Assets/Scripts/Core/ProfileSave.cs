using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// The profile, saved to a JSON file in persistentDataPath: the roster's captains whole, the soldier
// inventory with its equips, Gold and unlocked locations. Written with every run save, and when a
// run ends, so it always agrees with the run save beside it. Written through SaveFiles, so a
// crash mid-write leaves the previous save, and a broken file falls back to its backup.
public static class ProfileSave
{
    // Bump this when the file's shape changes, and add a step to Migrations that upgrades the
    // version before it. Older saves are upgraded on load; they are never thrown away.
    const int Version = 1;

    // One step per version bump, keyed by the version it upgrades from: the "1 -> 2" step has
    // key 1. A step takes the file's JSON in the old shape and returns it in the next one (read
    // it into a class shaped like the old file, build the new one, write that back out). Steps
    // run in order, so each only needs to know the version just before its own.
    static readonly Dictionary<int, Func<string, string>> Migrations = new()
    {
        // { 1, MigrateV1ToV2 },
    };

    // Just enough of the file to tell which version wrote it.
    [Serializable]
    class VersionProbe
    {
        public int version;
    }

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

    public static bool Exists => SaveFiles.Exists(FilePath);

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
            SaveFiles.Write(FilePath, JsonUtility.ToJson(file, true));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.LogWarning($"Couldn't save the profile to {FilePath}: {e.Message}");
        }
    }

    // Restores the saved profile as Profile.Current, reconnecting captains to their templates.
    // Returns false, leaving the current profile alone, if there is no save or it can't be used.
    public static bool TryLoad(IEnumerable<CaptainData> templates)
    {
        if (!Exists) return false;
        // A profile from a later build can't be read. It is set aside under its own name before
        // anything saves over it, and the backup (older still) isn't loaded in its place.
        int newerVersion = 0;
        string newerText = null;
        SaveFile Parse(string text)
        {
            if (newerText != null) return null;
            int version = JsonUtility.FromJson<VersionProbe>(text)?.version ?? 0;
            if (SaveMigration.Check(version, Version) == SaveVersionStatus.Newer)
            {
                newerVersion = version;
                newerText = text;
                return null;
            }
            if (!SaveMigration.TryUpgrade(ref text, version, Version, Migrations)) return null;
            SaveFile parsed = JsonUtility.FromJson<SaveFile>(text);
            if (parsed != null) parsed.version = Version;
            return parsed;
        }

        SaveSource source = SaveFiles.Load(FilePath, Parse, out SaveFile file);
        if (newerText != null)
        {
            string kept = NewerVersionPath(newerVersion);
            try
            {
                File.WriteAllText(kept, newerText);
                Debug.LogWarning($"The profile at {FilePath} is from a newer version of the game ({newerVersion}, this build reads up to {Version}). It was copied to {kept} and a fresh profile is used.");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"The profile at {FilePath} is from a newer version of the game ({newerVersion}) and couldn't be copied to {kept}: {e.Message}");
            }
            return false;
        }
        if (source == SaveSource.None)
        {
            Debug.LogWarning($"Couldn't use the profile at {FilePath} or its backup, starting fresh.");
            return false;
        }
        if (source == SaveSource.Backup) Debug.LogWarning($"Couldn't use the profile at {FilePath}, loaded its backup.");

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

    // Where a profile written by a later build is kept when this build can't read it. Outside the
    // main/backup pair, so saving never overwrites it.
    static string NewerVersionPath(int version) => $"{FilePath}.v{version}{SaveFiles.BackupSuffix}";

    // Deletes the backup too, so a deleted profile can't come back from it. A profile set aside
    // from a later build is left alone.
    public static void Delete() => SaveFiles.Delete(FilePath);
}
