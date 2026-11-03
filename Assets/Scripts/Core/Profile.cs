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

    public bool RosterFull => Roster.Count >= MaxRoster;

    public static Profile StartNew(int gold)
    {
        Current = new Profile { Gold = Mathf.Max(0, gold) };
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

    // The captain's soldiers in slot order.
    public List<SoldierItem> SoldiersOf(Captain captain) =>
        captain != null ? Soldiers.EquippedTo(captain.id) : new List<SoldierItem>();

    // Forgets the profile in memory, so it is loaded from its save (or made afresh) next.
    public static void Clear() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;
}
