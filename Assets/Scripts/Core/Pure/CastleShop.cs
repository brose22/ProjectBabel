using System;
using System.Collections.Generic;

// Why the Captain Recruit tab won't sell a captain, if it won't.
public enum RecruitBlock { None, SoldOut, RosterFull, NotEnoughGold }

// The Castle shop's recruit tabs (item: castle shop): what is on the shelves for the profile's shop
// seed, what each costs, and whether it can be bought. The stock stays put until the seed changes
// after a run, so a shelf bought empty stays empty.
public static class CastleShop
{
    // The shelves the shop seed is split between, so each draws its own stock.
    public const int SoldierShelf = 0;
    public const int CaptainShelf = 1;
    public const int GiftShelf = 2;

    // The seed for one item on a shelf. Hashed, since System.Random streams from neighbouring seeds
    // start out alike.
    public static int SlotSeed(int shopSeed, int shelf, int slot)
    {
        unchecked
        {
            uint x = (uint)shopSeed ^ (uint)(shelf * 0x9e3779b9) ^ (uint)((slot + 1) * 0x85ebca6b);
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (int)(x & int.MaxValue);
        }
    }

    // count soldiers of any class and rarity, rolled from the shop seed.
    public static List<SoldierItem> SoldierStock(int shopSeed, int count)
    {
        var stock = new List<SoldierItem>();
        for (int slot = 0; slot < count; slot++) stock.Add(SoldierRules.Roll(SlotSeed(shopSeed, SoldierShelf, slot)));
        return stock;
    }

    // A soldier's price by rarity, Common first. A rarity past the end of the list costs the last.
    public static int SoldierPrice(SoldierRarity rarity, IReadOnlyList<int> prices)
    {
        if (prices == null || prices.Count == 0) return 0;
        return Math.Max(0, prices[Math.Min((int)rarity, prices.Count - 1)]);
    }

    public static bool CanBuySoldier(bool bought, int gold, int price) => !bought && gold >= price;

    // A full roster is the reason given first, since Gold alone wouldn't help.
    public static RecruitBlock CaptainBlock(bool bought, int rosterCount, int maxRoster, int gold, int price)
    {
        if (bought) return RecruitBlock.SoldOut;
        if (rosterCount >= maxRoster) return RecruitBlock.RosterFull;
        if (gold < price) return RecruitBlock.NotEnoughGold;
        return RecruitBlock.None;
    }
}
