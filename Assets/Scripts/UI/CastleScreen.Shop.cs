using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Castle shop (item: castle shop): the Soldier Recruit tab, the Captain Recruit tab and gifts
// for the next run. World-map shops sell only gifts. The stock comes from the profile's shop seed,
// which changes when the party sets out, so it refreshes after each run.
public partial class CastleScreen
{
    enum ShopTab { Soldiers, Captains, Gifts }

    static readonly string[] ShopTabNames = { "Soldier Recruit", "Captain Recruit", "Gifts" };

    ShopTab shopTab;
    // The stock rolled for stockSeed, kept for the visit so the same captains stay on the shelves.
    int stockSeed;
    List<SoldierItem> soldierStock;
    List<Captain> captainStock;

    void RollStock()
    {
        if (soldierStock != null && stockSeed == profile.ShopSeed) return;
        stockSeed = profile.ShopSeed;
        soldierStock = CastleShop.SoldierStock(stockSeed, data.castleSoldierStock);
        captainStock = MatchManager.RollCaptains(data, profile.Roster, data.castleCaptainStock,
            CastleShop.SlotSeed(stockSeed, CastleShop.CaptainShelf, 0));
    }

    void BuildShop()
    {
        RollStock();
        RectTransform column = NewScreen("Shop", InventoryWidth, BuildHome);
        UIKit.Label(column, "SHOP", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, Summary(), 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform tabs = Row(column, "Tabs", 6);
        foreach (ShopTab each in Enum.GetValues(typeof(ShopTab)))
        {
            ShopTab chosen = each;
            UIKit.LiveButton(tabs, () => ShopTabNames[(int)chosen], () => { shopTab = chosen; BuildShop(); }, refreshers,
                selected: () => chosen == shopTab, width: 170f, height: 30f);
        }

        RectTransform panel = UIKit.Box(column, "Shelves");
        UIKit.Size(panel, InventoryWidth, InventoryBodyHeight);
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 6);
        if (shopTab == ShopTab.Soldiers) BuildSoldierShelves(panel);
        else if (shopTab == ShopTab.Captains) BuildCaptainShelves(panel);
        else BuildGiftShelves(panel);

        UIKit.PadBadge(UIKit.Button(column, "Back", BuildHome, 200f, 32f), PadGlyph.East);
    }

    // Saves the purchase and redraws the tab.
    void Bought()
    {
        ProfileSave.Save(profile);
        BuildShop();
    }

    static void SoldOut(Transform list) =>
        UIKit.Label(list, "Sold out. New stock arrives after your next run.", 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

    void BuildSoldierShelves(Transform panel)
    {
        Header(panel, $"SOLDIER RECRUIT   <color={UIKit.Hex(UIKit.MutedTextColor)}>Bought soldiers go to the inventory. New stock after each run.</color>");
        ScrollRect list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        int shown = 0;
        for (int i = 0; i < soldierStock.Count; i++)
        {
            if (profile.SoldSoldiers.Contains(i)) continue;
            int slot = i;
            SoldierItem soldier = soldierStock[i];
            int price = CastleShop.SoldierPrice(soldier.rarity, data.soldierPrices);
            RectTransform row = SoldierRow(list.content, soldier, $"{price} Gold");
            UIKit.LiveButton(row, () => "Buy", () => BuySoldier(slot), refreshers,
                () => CastleShop.CanBuySoldier(false, profile.Gold, price), width: 80f, height: 24f);
            shown++;
        }
        if (shown == 0) SoldOut(list.content);
    }

    void BuySoldier(int slot)
    {
        if (slot < 0 || slot >= soldierStock.Count) return;
        SoldierItem soldier = soldierStock[slot];
        int price = CastleShop.SoldierPrice(soldier.rarity, data.soldierPrices);
        if (!CastleShop.CanBuySoldier(profile.SoldSoldiers.Contains(slot), profile.Gold, price) || !profile.TrySpendGold(price)) return;
        profile.Soldiers.Add(soldier);
        profile.SoldSoldiers.Add(slot);
        Bought();
    }

    void BuildCaptainShelves(Transform panel)
    {
        Header(panel, $"CAPTAIN RECRUIT   <color={UIKit.Hex(UIKit.MutedTextColor)}>Captains {profile.Roster.Count}/{Profile.MaxRoster}. " +
                      $"Recruits join the roster with no soldiers.</color>");
        // A full roster is said up front, since none of the shelves can sell.
        if (profile.RosterFull)
            UIKit.Label(panel, $"Your roster is full ({Profile.MaxRoster} captains). Release a captain from the inventory to recruit another.",
                13, TextAnchor.MiddleCenter, UIKit.BadColor);
        ScrollRect list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        int price = Mathf.Max(0, data.captainPrice);
        int shown = 0;
        for (int i = 0; i < captainStock.Count; i++)
        {
            if (profile.SoldCaptains.Contains(i)) continue;
            int slot = i;
            RectTransform row = UIKit.Group(list.content, "Shelf");
            UIKit.Layout<HorizontalLayoutGroup>(row, 0, 8).childAlignment = TextAnchor.MiddleLeft;
            UIKit.Size(row, flexibleWidth: 1f);
            RectTransform card = CaptainCard(row, captainStock[i], $"{price} GOLD", () => { }, false);
            // Only the Buy button takes clicks and focus.
            card.GetComponent<Button>().enabled = false;
            UIKit.LiveButton(row, () => CaptainBlock(price) switch
                {
                    RecruitBlock.RosterFull => "Roster full",
                    RecruitBlock.NotEnoughGold => "Need Gold",
                    _ => "Recruit",
                }, () => BuyCaptain(slot), refreshers, () => CaptainBlock(price) == RecruitBlock.None, width: 110f, height: 28f);
            shown++;
        }
        if (shown == 0) SoldOut(list.content);
    }

    RecruitBlock CaptainBlock(int price) => CastleShop.CaptainBlock(false, profile.Roster.Count, Profile.MaxRoster, profile.Gold, price);

    void BuyCaptain(int slot)
    {
        if (slot < 0 || slot >= captainStock.Count) return;
        int price = Mathf.Max(0, data.captainPrice);
        if (CastleShop.CaptainBlock(profile.SoldCaptains.Contains(slot), profile.Roster.Count, Profile.MaxRoster, profile.Gold, price)
            != RecruitBlock.None || !profile.TrySpendGold(price)) return;
        if (!profile.TryAdd(captainStock[slot]))
        {
            profile.AddGold(price);
            return;
        }
        profile.SoldCaptains.Add(slot);
        Bought();
    }

    // Gifts don't sell out. They wait in the profile and go with the party when it sets out.
    void BuildGiftShelves(Transform panel)
    {
        Header(panel, $"GIFTS   <color={UIKit.Hex(UIKit.MutedTextColor)}>Packed for your next run. Give them to captains from the party window on the map.</color>");
        ScrollRect list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        foreach (int index in ShopStock.Pick(data.shopGifts.Count, data.shopSize, CastleShop.SlotSeed(stockSeed, CastleShop.GiftShelf, 0)))
        {
            GiftData gift = data.shopGifts[index];
            if (gift == null) continue;
            RectTransform row = UIKit.Box(list.content, gift.displayName, CardColor, blocksClicks: false);
            UIKit.Size(row, flexibleWidth: 1f);
            UIKit.Layout<HorizontalLayoutGroup>(row, 6, 8).childAlignment = TextAnchor.MiddleLeft;
            UIKit.Icon(row, gift.icon, 32f);
            RectTransform text = UIKit.Group(row, "Text");
            UIKit.Layout<VerticalLayoutGroup>(text, 0, 1);
            UIKit.Size(text, flexibleWidth: 1f);
            UIKit.Label(text, gift.displayName, 13).fontStyle = FontStyle.Bold;
            UIKit.LiveLabel(text, () => $"{gift.cost} Gold   (packed {profile.PackedGift(gift.name)})", refreshers, 11,
                color: UIKit.MutedTextColor);
            UIKit.LiveButton(row, () => "Buy", () => BuyGift(gift), refreshers, () => profile.Gold >= gift.cost, width: 80f, height: 24f);
        }
    }

    void BuyGift(GiftData gift)
    {
        if (gift == null || !profile.TrySpendGold(gift.cost)) return;
        profile.PackGift(gift.name);
        Bought();
    }

    // The packed gifts go into the run's inventory, and the shop gets new stock for when the party
    // is back.
    void PackForRun(RunState run)
    {
        foreach (GiftData gift in data.shopGifts)
            if (gift != null) run.AddGift(gift, profile.PackedGift(gift.name));
        profile.PackedGifts.Clear();
        profile.RestockShop(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
    }
}
