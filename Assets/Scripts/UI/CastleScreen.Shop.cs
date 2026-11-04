using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The Castle shop (item: castle shop): the Soldier Recruit tab, the Captain Recruit tab and gifts
// for the next run. World-map shops sell only gifts. The stock comes from the profile's shop seed,
// which changes when the party sets out, so it refreshes after each run.
public partial class CastleScreen
{
    enum ShopTab { Soldiers, Captains, Gifts }

    // Each shelf has its own building in the town.
    static readonly string[] ShopTitles = { "TRAINING YARD", "TAVERN", "MARKET" };

    ShopTab shopTab;
    // The stock rolled for stockSeed, kept for the visit so the same captains stay on the shelves.
    int stockSeed;
    List<SoldierItem> soldierStock;
    List<Captain> captainStock;

    // Feedback after a purchase: the shelves are rebuilt, so the scroll position and the focused row
    // (or Buy button) are carried over, a toast says what was bought and the Gold figure flashes.
    const float ToastSeconds = 2.2f;
    const float FlashSeconds = 0.9f;
    static readonly Color FlashColor = new(1f, 0.97f, 0.8f, 1f);
    readonly List<Button> buyButtons = new();

    Text toast;
    float toastAge = float.MaxValue;
    float flashAge = float.MaxValue;

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
        buyButtons.Clear();
        UIKit.Label(column, ShopTitles[(int)shopTab], UIKit.TitleSize, TextAnchor.MiddleCenter);

        RectTransform panel = UIKit.Box(column, "Shelves");
        UIKit.Size(panel, InventoryWidth, InventoryBodyHeight);
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 6);
        if (shopTab == ShopTab.Soldiers) BuildSoldierShelves(panel);
        else if (shopTab == ShopTab.Captains) BuildCaptainShelves(panel);
        else BuildGiftShelves(panel);
    }

    // Saves the purchase and redraws the tab.
    void Bought(string message)
    {
        ProfileSave.Save(profile);
        GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        // A purchase window's row keeps its place; otherwise the focused Buy button does.
        int index = purchaseRowIndex >= 0 ? purchaseRowIndex
            : focused != null ? buyButtons.FindIndex(b => b != null && b.gameObject == focused) : -1;
        purchaseRowIndex = -1;
        ScrollRect old = screen != null ? screen.GetComponentInChildren<ScrollRect>() : null;
        float scroll = old != null ? old.verticalNormalizedPosition : 1f;
        BuildShop();
        // A sold-out shelf is gone, so the same position holds the next one.
        ScrollRect list = screen.GetComponentInChildren<ScrollRect>();
        if (list != null)
        {
            Canvas.ForceUpdateCanvases();
            list.verticalNormalizedPosition = scroll;
        }
        buyButtons.RemoveAll(b => b == null);
        if (index >= 0 && buyButtons.Count > 0)
        {
            UIFocusScope scope = screen.GetComponent<UIFocusScope>();
            if (scope != null) scope.Remembered = buyButtons[Mathf.Min(index, buyButtons.Count - 1)];
        }
        ShowToast(message);
        flashAge = 0f;
    }

    void ShowToast(string message)
    {
        if (toast == null)
        {
            toast = UIKit.Shadowed(UIKit.Label(canvas.transform, message, UIKit.HeadingSize, TextAnchor.MiddleCenter, UIKit.GoodColor));
            toast.raycastTarget = false;
            toast.fontStyle = FontStyle.Bold;
            UIKit.Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -HudHeight - 8f), new Vector2(520f, 24f));
        }
        toast.text = message;
        toast.transform.SetAsLastSibling();
        toastAge = 0f;
    }

    // Runs every frame from LateUpdate: fades the toast and settles the Gold flash.
    void ShopFeedback()
    {
        float dt = Time.unscaledDeltaTime;
        if (toast != null)
        {
            toastAge += dt;
            float fade = Mathf.Clamp01((ToastSeconds - toastAge) / 0.4f);
            toast.gameObject.SetActive(fade > 0f);
            Color c = toast.color;
            c.a = fade;
            toast.color = c;
        }
        if (hudGold != null && flashAge < FlashSeconds)
        {
            flashAge += dt;
            hudGold.color = Color.Lerp(FlashColor, UIKit.AccentColor, Mathf.Clamp01(flashAge / FlashSeconds));
        }
    }

    static void SoldOut(Transform list) =>
        UIKit.Label(list, "Sold out. New stock arrives after your next run.", 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

    string BuyLabel(int price) => profile.Gold >= price ? $"Buy {price}" : "Need Gold";

    void BuildSoldierShelves(Transform panel)
    {
        Header(panel, "SOLDIER RECRUIT", "Bought soldiers go to the inventory. New stock after each run.", below: true);
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
            Button pick = null;
            pick = UIKit.Clickable(row, () => OpenSoldierPurchase(pick, slot));
            buyButtons.Add(pick);
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
        Bought($"Bought {CaptainPanelParts.SoldierName(soldier, data.ClassData(soldier.soldierClass))}, -{price} Gold");
    }

    void BuildCaptainShelves(Transform panel)
    {
        Header(panel, "CAPTAIN RECRUIT",
            $"Captains {profile.Roster.Count}/{Profile.MaxRoster}. Recruits join the roster with no soldiers.", below: true);
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
            Captain captain = captainStock[i];
            RectTransform row = UIKit.ItemRow(list.content, captain.displayName, captain.Portrait, captain.displayName, UIKit.TextColor,
                CaptainDetail(captain, price));
            Button pick = null;
            pick = UIKit.Clickable(row, () => OpenCaptainPurchase(pick, slot));
            buyButtons.Add(pick);
            shown++;
        }
        if (shown == 0) SoldOut(list.content);
    }

    // "Fire Guardian   Lv 1   Ability   120 Gold", the element in its colour.
    string CaptainDetail(Captain captain, int price)
    {
        ElementData element = data.ElementInfo(captain.element);
        string elementName = element != null ? element.displayName : captain.element.ToString();
        string ability = captain.Ability != null ? $"   {captain.AbilityName}" : "";
        return $"<color={UIKit.Hex(UIKit.ElementColor(captain.element))}>{elementName}</color> {captain.ClassName}   Lv {Level(captain)}{ability}" +
               $"   <color={UIKit.Hex(UIKit.AccentColor)}>{price} Gold</color>";
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
        Bought($"Recruited {captainStock[slot].displayName}, -{price} Gold");
    }

    // Gifts don't sell out. They wait in the profile and go with the party when it sets out.
    void BuildGiftShelves(Transform panel)
    {
        Header(panel, "GIFTS", "Packed for your next run. Give them to captains from the party window on the map.", below: true);
        ScrollRect list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        foreach (int index in ShopStock.Pick(data.shopGifts.Count, data.shopSize, CastleShop.SlotSeed(stockSeed, CastleShop.GiftShelf, 0)))
        {
            GiftData gift = data.shopGifts[index];
            if (gift == null) continue;
            string Detail() => $"{gift.cost} Gold   (packed {profile.PackedGift(gift.name)})";
            RectTransform row = UIKit.ItemRow(list.content, gift.displayName, gift.icon, gift.displayName, UIKit.TextColor, Detail(),
                out Text detail);
            refreshers.Add(() => detail.text = Detail());
            buyButtons.Add(UIKit.LiveButton(row, () => BuyLabel(gift.cost), () => BuyGift(gift), refreshers,
                () => profile.Gold >= gift.cost, width: UIKit.ItemActionWidth, height: UIKit.ItemActionHeight));
        }
    }

    void BuyGift(GiftData gift)
    {
        if (gift == null || !profile.TrySpendGold(gift.cost)) return;
        profile.PackGift(gift.name);
        Bought($"Packed {gift.displayName}, -{gift.cost} Gold");
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
