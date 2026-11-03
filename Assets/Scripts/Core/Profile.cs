using System.Collections.Generic;
using UnityEngine;

// Everything that lasts between runs (item: profile): the roster of captains, with their level,
// XP and bond on each Captain, the soldier inventory with its equips, Gold and how many locations
// are unlocked. Lives outside any scene; ProfileSave keeps it on disk.
public class Profile
{
    public static Profile Current { get; private set; }

    public const int MaxRoster = 12;

    public List<Captain> Roster { get; } = new();
    public SoldierInventory Soldiers { get; private set; } = new();
    public int Gold { get; private set; }
    // Locations unlock in order, so the count says which: the first LocationsUnlocked of them.
    public int LocationsUnlocked { get; private set; } = 1;

    // The Castle shop's stock is rolled from this seed (see CastleShop); it changes after each run.
    public int ShopSeed { get; private set; }
    // The shelves sold this stock, by shelf index, so a bought item doesn't come back until it changes.
    public List<int> SoldSoldiers { get; } = new();
    public List<int> SoldCaptains { get; } = new();
    // Gifts bought at the Castle, by asset name, waiting to go with the party on the next run.
    public Dictionary<string, int> PackedGifts { get; } = new();

    public bool RosterFull => Roster.Count >= MaxRoster;

    public static Profile StartNew(int gold)
    {
        Current = new Profile { Gold = Mathf.Max(0, gold) };
        Current.RestockShop(Random.Range(int.MinValue, int.MaxValue));
        return Current;
    }

    // A saved profile picked up again (see ProfileSave), which adds the roster.
    public static Profile Resume(int gold, int locationsUnlocked, SoldierInventory soldiers)
    {
        Current = new Profile { Gold = Mathf.Max(0, gold), LocationsUnlocked = Mathf.Max(1, locationsUnlocked) };
        Current.SetSoldiers(soldiers);
        return Current;
    }

    public void SetSoldiers(SoldierInventory inventory) => Soldiers = inventory ?? new SoldierInventory();

    public void AddGold(int amount) => Gold += Mathf.Max(0, amount);

    public bool TrySpendGold(int amount)
    {
        if (amount > Gold) return false;
        Gold -= amount;
        return true;
    }

    // New stock for the Castle shop, with every shelf full again.
    public void RestockShop(int seed)
    {
        ShopSeed = seed;
        SoldSoldiers.Clear();
        SoldCaptains.Clear();
    }

    public void PackGift(string giftName, int count = 1)
    {
        if (string.IsNullOrEmpty(giftName) || count <= 0) return;
        PackedGifts[giftName] = PackedGift(giftName) + count;
    }

    public int PackedGift(string giftName) => giftName != null && PackedGifts.TryGetValue(giftName, out int count) ? count : 0;

    public void UnlockLocations(int count) => LocationsUnlocked = Mathf.Max(LocationsUnlocked, count);

    public Captain Find(string id) => string.IsNullOrEmpty(id) ? null : Roster.Find(c => c.id == id);

    public bool Owns(Captain captain) => captain != null && Roster.Contains(captain);

    // Adds a captain while there's room; false when the roster is full or it is already there.
    public bool TryAdd(Captain captain)
    {
        if (captain == null || Owns(captain) || RosterFull) return false;
        Roster.Add(captain);
        return true;
    }

    // Takes a captain out of the roster for good, by release or walkout. Their soldiers go back to
    // the inventory.
    public void Release(Captain captain)
    {
        if (captain == null) return;
        Roster.Remove(captain);
        Soldiers.UnequipAll(captain.id);
    }

    // A captain joining during a run. With a full roster, released (the newcomer or a captain
    // already here) goes first, and their soldiers go back to the inventory. Returns whether the
    // newcomer joined.
    public bool Join(Captain newcomer, Captain released)
    {
        bool releasing = released != null && released != newcomer && Owns(released);
        bool joined = RosterRules.Join(Roster, newcomer, released, MaxRoster);
        if (releasing && !Owns(released)) Soldiers.UnequipAll(released.id);
        return joined;
    }

    // A lost run: a share of the Gold goes, and so does every party captain still in the roster,
    // with the soldiers equipped to them. Returns the captains lost.
    public List<Captain> LoseRun(IEnumerable<Captain> party, out int goldLost, out int soldiersLost)
    {
        goldLost = RunEndRules.GoldLost(Gold);
        Gold -= goldLost;
        soldiersLost = 0;
        List<Captain> lost = RunEndRules.CaptainsLost(party, Roster);
        foreach (Captain captain in lost)
        {
            Roster.Remove(captain);
            soldiersLost += Soldiers.RemoveEquippedTo(captain.id);
        }
        return lost;
    }

    // The captain's soldiers in slot order.
    public List<SoldierItem> SoldiersOf(Captain captain) =>
        captain != null ? Soldiers.EquippedTo(captain.id) : new List<SoldierItem>();

    // Forgets the profile in memory, so it is loaded from its save (or made afresh) next.
    public static void Clear() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;
}
