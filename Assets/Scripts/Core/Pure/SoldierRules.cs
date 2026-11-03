using System;
using System.Collections.Generic;

// A soldier's fixed archetype (item: soldier classes). It fixes the damage type and the captain
// stat the soldier adds.
public enum SoldierClass { Knight, Archer, Mage, Rogue }

// A soldier's tier: Common white, Uncommon green, Rare purple, Epic red, Legendary gold.
public enum SoldierRarity { Common, Uncommon, Rare, Epic, Legendary }

// The captain stat an equipped soldier adds to.
public enum CaptainStat { Defense, Range, Power, Speed }

// One soldier the player owns. A plain class so it saves as JSON. It is equipped when equippedTo
// holds a captain's id, in that captain's slot; otherwise it waits in the inventory.
[Serializable]
public class SoldierItem
{
    public string id;
    public SoldierClass soldierClass;
    public SoldierRarity rarity;
    public string equippedTo = "";
    public int slot = -1;

    public bool IsEquipped => !string.IsNullOrEmpty(equippedTo);
}

// The stat a soldier adds to its captain, and rolling new soldiers.
public static class SoldierRules
{
    // Each tier adds a quarter of the Common bonus: x1, x1.25, x1.5, x1.75, x2.
    public static float RarityMultiplier(SoldierRarity rarity) => 1f + 0.25f * (int)rarity;

    public static CaptainStat StatOf(SoldierClass soldierClass) => soldierClass switch
    {
        SoldierClass.Knight => CaptainStat.Defense,
        SoldierClass.Archer => CaptainStat.Range,
        SoldierClass.Mage => CaptainStat.Power,
        _ => CaptainStat.Speed,
    };

    // baseBonus is what a Common soldier of the class adds.
    public static float Bonus(float baseBonus, SoldierRarity rarity) => baseBonus * RarityMultiplier(rarity);

    // What the soldiers add to one captain stat, given each class's Common bonus.
    public static float StatTotal(IEnumerable<SoldierItem> soldiers, CaptainStat stat, Func<SoldierClass, float> baseBonus)
    {
        float total = 0f;
        foreach (SoldierItem soldier in soldiers)
            if (soldier != null && StatOf(soldier.soldierClass) == stat)
                total += Bonus(baseBonus(soldier.soldierClass), soldier.rarity);
        return total;
    }

    // Chance of each rarity, Common first, when a soldier is rolled.
    static readonly float[] RarityWeights = { 0.5f, 0.25f, 0.14f, 0.08f, 0.03f };

    // A random soldier of any class. The same seed gives the same soldier.
    public static SoldierItem Roll(int seed)
    {
        var random = new Random(seed);
        var bytes = new byte[16];
        random.NextBytes(bytes);
        var item = new SoldierItem
        {
            id = new Guid(bytes).ToString("N"),
            soldierClass = (SoldierClass)random.Next(Enum.GetValues(typeof(SoldierClass)).Length),
        };
        double pick = random.NextDouble();
        for (int tier = 0; tier < RarityWeights.Length; tier++)
        {
            item.rarity = (SoldierRarity)tier;
            pick -= RarityWeights[tier];
            if (pick < 0) break;
        }
        return item;
    }
}

// The soldiers the player owns and which captain each is equipped to (item: soldiers). Each captain
// has SlotsPerCaptain slots, a slot holds at most one soldier, and a soldier is in at most one slot.
[Serializable]
public class SoldierInventory
{
    public const int SlotsPerCaptain = 4;

    public List<SoldierItem> items = new();

    public IReadOnlyList<SoldierItem> Items => items;

    public void Add(SoldierItem item)
    {
        if (item != null && !string.IsNullOrEmpty(item.id) && Find(item.id) == null) items.Add(item);
    }

    public SoldierItem Find(string id) => items.Find(i => i.id == id);

    public SoldierItem InSlot(string captainId, int slot) =>
        items.Find(i => i.IsEquipped && i.equippedTo == captainId && i.slot == slot);

    // The captain's soldiers in slot order.
    public List<SoldierItem> EquippedTo(string captainId)
    {
        List<SoldierItem> equipped = items.FindAll(i => i.IsEquipped && i.equippedTo == captainId);
        equipped.Sort((a, b) => a.slot.CompareTo(b.slot));
        return equipped;
    }

    public List<SoldierItem> Unequipped => items.FindAll(i => !i.IsEquipped);

    // Puts the soldier in the captain's slot, taking it from wherever it was. A soldier already in
    // that slot goes back to the inventory.
    public bool Equip(string soldierId, string captainId, int slot)
    {
        SoldierItem item = Find(soldierId);
        if (item == null || string.IsNullOrEmpty(captainId) || slot < 0 || slot >= SlotsPerCaptain) return false;
        SoldierItem occupant = InSlot(captainId, slot);
        if (occupant == item) return true;
        if (occupant != null) Unequip(occupant.id);
        item.equippedTo = captainId;
        item.slot = slot;
        return true;
    }

    public bool Unequip(string soldierId)
    {
        SoldierItem item = Find(soldierId);
        if (item == null || !item.IsEquipped) return false;
        item.equippedTo = "";
        item.slot = -1;
        return true;
    }

    // Sends all the captain's soldiers back to the inventory, as when a captain leaves.
    public void UnequipAll(string captainId)
    {
        foreach (SoldierItem item in EquippedTo(captainId)) Unequip(item.id);
    }

    // The lowest empty slot below openSlots, or -1 when they're all taken.
    public int FirstEmptySlot(string captainId, int openSlots)
    {
        for (int slot = 0; slot < Math.Min(openSlots, SlotsPerCaptain); slot++)
            if (InSlot(captainId, slot) == null) return slot;
        return -1;
    }
}
