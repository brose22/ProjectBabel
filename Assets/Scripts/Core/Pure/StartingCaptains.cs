using System;
using System.Collections.Generic;

// The starting captains (item: starting captains): the first time the game is played, one rolled
// captain of each starting role is offered, the player picks Picks of them, and each picked captain
// comes with SoldiersEach random soldiers, equipped. The captain left behind is discarded.
public static class StartingCaptains
{
    public static readonly CaptainRole[] Roles = { CaptainRole.Knight, CaptainRole.Mage, CaptainRole.Archer, CaptainRole.Healer };
    public const int Picks = 3;
    public const int SoldiersEach = 2;

    // The template index and roll seed of each offered captain, one per starting role in Roles
    // order, from the role of each template. A role with several templates uses a random one of
    // them; a role with none is left out of the offer. The same seed gives the same offer.
    public static List<(int template, int seed)> Draw(IReadOnlyList<CaptainRole> templateRoles, int seed)
    {
        var offer = new List<(int template, int seed)>();
        var random = new Random(seed);
        foreach (CaptainRole role in Roles)
        {
            var matching = new List<int>();
            for (int t = 0; t < templateRoles.Count; t++)
                if (templateRoles[t] == role) matching.Add(t);
            if (matching.Count == 0) continue;
            offer.Add((matching[random.Next(matching.Count)], random.Next()));
        }
        return offer;
    }

    // How many captains must be picked from an offer of the given size: Picks, or all of a smaller offer.
    public static int PickCount(int offerSize) => Math.Max(0, Math.Min(Picks, offerSize));

    // Picks or unpicks the offered captain at index. A captain can't be picked once the pick is
    // full. False when nothing changed.
    public static bool Toggle(List<int> picked, int index, int offerSize)
    {
        if (index < 0 || index >= offerSize) return false;
        if (picked.Remove(index)) return true;
        if (picked.Count >= PickCount(offerSize)) return false;
        picked.Add(index);
        return true;
    }

    public static bool IsComplete(IReadOnlyCollection<int> picked, int offerSize) =>
        offerSize > 0 && picked.Count == PickCount(offerSize);

    // Rolls SoldiersEach soldiers for each captain, adds them to the inventory and equips them in
    // the captain's first empty slots. Returns the soldiers given.
    public static List<SoldierItem> GiveSoldiers(SoldierInventory inventory, IEnumerable<string> captainIds, int seed)
    {
        var given = new List<SoldierItem>();
        var random = new Random(seed);
        foreach (string captainId in captainIds)
        {
            for (int i = 0; i < SoldiersEach; i++)
            {
                int slot = inventory.FirstEmptySlot(captainId, SoldierInventory.SlotsPerCaptain);
                if (slot < 0) break;
                SoldierItem soldier = SoldierRules.Roll(random.Next());
                inventory.Add(soldier);
                inventory.Equip(soldier.id, captainId, slot);
                given.Add(soldier);
            }
        }
        return given;
    }
}
