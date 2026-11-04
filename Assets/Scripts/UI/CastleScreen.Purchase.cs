using System;
using UnityEngine;
using UnityEngine.UI;

// The window a Tavern or Training Yard row opens: what the captain or soldier is, with Buy and
// Cancel, or a note that the Gold (or the roster's room) isn't there. It dims the shop under it,
// and a click beside it or the gamepad's cancel button closes it.
public partial class CastleScreen
{
    const float PurchaseWidth = 420f;

    RectTransform purchaseOverlay;
    // The shelf row that opened the window, which takes focus back when it closes, and its place
    // among the shelves, so focus lands on the same place after a purchase redraws them.
    Button purchaseRow;
    int purchaseRowIndex = -1;

    RectTransform OpenPurchaseWindow(Button row, out UIFocusScope scope)
    {
        DestroyPurchaseWindow();
        purchaseRow = row;
        purchaseRowIndex = buyButtons.IndexOf(row);
        // Closing puts focus back itself (ClosePurchaseWindow), as a purchase redraws the shelves.
        ModalWindow modal = UIKit.Modal(screen, "Purchase", PurchaseWidth, new Vector2(0.5f, 0.5f), Vector2.zero,
            ClosePurchaseWindow);
        purchaseOverlay = modal.Backdrop;
        purchaseOverlay.offsetMax = new Vector2(0f, -HudHeight);
        scope = modal.Scope;
        return modal.Window;
    }

    void DestroyPurchaseWindow()
    {
        if (purchaseOverlay != null) Destroy(purchaseOverlay.gameObject);
        purchaseOverlay = null;
    }

    void ClosePurchaseWindow()
    {
        DestroyPurchaseWindow();
        if (purchaseRow != null && screenScope != null) screenScope.Request(purchaseRow);
        purchaseRow = null;
        purchaseRowIndex = -1;
    }

    // A window that only explains why the row can't be bought, with an OK button.
    void ShowPurchaseBlocked(Button row, string title, string message)
    {
        RectTransform window = OpenPurchaseWindow(row, out UIFocusScope scope);
        Text heading = UIKit.Shadowed(UIKit.Label(window, title, UIKit.HeadingSize, TextAnchor.MiddleCenter, UIKit.BadColor));
        heading.fontStyle = FontStyle.Bold;
        UIKit.Label(window, message, 13, TextAnchor.MiddleCenter);
        Button ok = UIKit.CloseButton(window, "OK", ClosePurchaseWindow);
        scope.Default = () => ok;
    }

    void NotEnoughGold(Button row, string what, int price) =>
        ShowPurchaseBlocked(row, "NOT ENOUGH GOLD", $"{what} costs {price} Gold. You have {profile.Gold} Gold.");

    // Portrait in a frame, then the name and lines of detail beside it.
    static Transform PurchaseHeader(Transform window, Sprite portrait, string name, params string[] lines)
    {
        RectTransform top = UIKit.Group(window, "Top");
        UIKit.Layout<HorizontalLayoutGroup>(top, 0, 10).childAlignment = TextAnchor.UpperLeft;
        UIKit.Size(top, flexibleWidth: 1f);
        RectTransform frame = UIKit.Box(top, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, portrait, PortraitSize);
        RectTransform info = UIKit.Group(top, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 2);
        UIKit.Size(info, flexibleWidth: 1f);
        UIKit.Shadowed(UIKit.Label(info, name, 18)).fontStyle = FontStyle.Bold;
        foreach (string line in lines) UIKit.Label(info, line, 12);
        return top;
    }

    static RectTransform PurchaseStats(Transform window)
    {
        RectTransform stats = UIKit.Group(window, "Stats");
        UIKit.Layout<HorizontalLayoutGroup>(stats, 0, 4).childAlignment = TextAnchor.UpperCenter;
        UIKit.Size(stats, flexibleWidth: 1f);
        return stats;
    }

    // Buy and Cancel side by side; Buy takes focus.
    void PurchaseButtons(Transform window, UIFocusScope scope, string buyText, Action buy)
    {
        RectTransform row = UIKit.Group(window, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, UIKit.Gap);
        UIKit.Size(row, flexibleWidth: 1f);
        Button confirm = UIKit.Button(row, buyText, () =>
        {
            DestroyPurchaseWindow();
            purchaseRow = null;
            buy();
        }, height: UIKit.ButtonHeight);
        UIKit.Size(confirm, height: UIKit.ButtonHeight, flexibleWidth: 1f);
        UIKit.CloseButton(row, "Cancel", ClosePurchaseWindow);
        scope.Default = () => confirm;
    }

    void OpenSoldierPurchase(Button row, int slot)
    {
        if (slot < 0 || slot >= soldierStock.Count) return;
        SoldierItem soldier = soldierStock[slot];
        SoldierClassData soldierClass = data.ClassData(soldier.soldierClass);
        int price = CastleShop.SoldierPrice(soldier.rarity, data.soldierPrices);
        string name = $"{soldier.rarity} {(soldierClass != null ? soldierClass.displayName : soldier.soldierClass.ToString())}";
        if (profile.Gold < price)
        {
            NotEnoughGold(row, name, price);
            return;
        }
        RectTransform window = OpenPurchaseWindow(row, out UIFocusScope scope);
        UIKit.Size(UIKit.Header(window, "Recruit soldier", $"{price} Gold"), flexibleWidth: 1f);
        SoldierDetails(window, soldier);
        UIKit.Label(window, $"Goes to the inventory. You have {profile.Gold} Gold.", 11, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        PurchaseButtons(window, scope, $"Buy {price}", () => BuySoldier(slot));
    }

    // Portrait, name, what it gives its captain and its damage type, then its class's stats.
    void SoldierDetails(Transform window, SoldierItem soldier)
    {
        SoldierClassData soldierClass = data.ClassData(soldier.soldierClass);
        PurchaseHeader(window, soldierClass != null ? soldierClass.portrait : null,
            CaptainPanelParts.SoldierName(soldier, soldierClass),
            $"Gives its captain {CaptainPanelParts.SoldierBonus(soldier, soldierClass)}",
            soldierClass != null ? $"{soldierClass.damageType} damage" : "");
        if (soldierClass == null) return;
        RectTransform stats = PurchaseStats(window);
        HallStat(stats, "HEALTH", $"{soldierClass.maxHealth:0}");
        HallStat(stats, "DAMAGE", $"x{soldierClass.damageMultiplier:0.##}");
        HallStat(stats, "REACH", $"{soldierClass.reach:0.#}");
        HallStat(stats, "SPEED", $"{soldierClass.moveSpeed:0.#}");
        HallStat(stats, "RESPAWN", $"{soldierClass.respawnCost}");
    }

    // A Barracks row's window: the soldier's stats and who has it, with Unequip for an equipped one.
    void OpenSoldierInfo(Button row, SoldierItem soldier)
    {
        RectTransform window = OpenPurchaseWindow(row, out UIFocusScope scope);
        UIKit.Size(UIKit.Header(window, "Soldier"), flexibleWidth: 1f);
        SoldierDetails(window, soldier);
        UIKit.Label(window, WhereIs(soldier), 12, TextAnchor.MiddleCenter);

        RectTransform buttons = UIKit.Group(window, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, UIKit.Gap);
        UIKit.Size(buttons, flexibleWidth: 1f);
        Button unequip = null;
        if (soldier.IsEquipped)
        {
            unequip = UIKit.Button(buttons, "Unequip", () =>
            {
                DestroyPurchaseWindow();
                purchaseRow = null;
                purchaseRowIndex = -1;
                profile.Soldiers.Unequip(soldier.id);
                ProfileSave.Save(profile);
                BuildInventory();
            }, height: UIKit.ButtonHeight);
            UIKit.Size(unequip, height: UIKit.ButtonHeight, flexibleWidth: 1f);
        }
        Button close = UIKit.CloseButton(buttons, "Close", ClosePurchaseWindow);
        scope.Default = () => unequip != null ? unequip : close;
    }

    void OpenCaptainPurchase(Button row, int slot)
    {
        if (slot < 0 || slot >= captainStock.Count) return;
        Captain captain = captainStock[slot];
        int price = Mathf.Max(0, data.captainPrice);
        switch (CaptainBlock(price))
        {
            case RecruitBlock.RosterFull:
                ShowPurchaseBlocked(row, "ROSTER FULL",
                    $"Your roster is full ({Profile.MaxRoster} captains). Release a captain from the Great Hall to recruit another.");
                return;
            case RecruitBlock.NotEnoughGold:
                NotEnoughGold(row, captain.displayName, price);
                return;
        }
        RectTransform window = OpenPurchaseWindow(row, out UIFocusScope scope);
        UIKit.Size(UIKit.Header(window, "Recruit captain", $"{price} Gold"), flexibleWidth: 1f);
        int level = Level(captain);
        ElementData element = data.ElementInfo(captain.element);
        string elementName = element != null ? element.displayName : captain.element.ToString();
        PurchaseHeader(window, captain.Portrait, captain.displayName,
            $"<color={UIKit.Hex(UIKit.ElementColor(captain.element))}>{elementName}</color> {captain.ClassName}",
            $"Level {level}");
        RectTransform stats = PurchaseStats(window);
        HallStat(stats, "POWER", $"{captain.power * LevelMath.StatFactor(level):0}");
        HallStat(stats, "RANGE", $"{captain.range:0.#}");
        HallStat(stats, "SPEED", $"{captain.speed:0.##}");
        HallStat(stats, "HEALTH", $"{captain.health}");
        HallStat(stats, "DEFENSE", $"{DefenseMath.AtLevel(captain.defense, captain.defensePerLevel, level):0.#}");
        HallStat(stats, "RESERVE", $"{captain.ResourceCapacity}");

        Transform special = UIKit.Group(window, "Special");
        UIKit.Layout<HorizontalLayoutGroup>(special, 0, 6);
        UIKit.Icon(special, captain.Ability != null ? captain.Ability.icon : null, 24f);
        UIKit.Label(special, captain.Ability != null ? captain.Ability.displayName : "No special ability", 13).fontStyle = FontStyle.Bold;
        if (captain.Ability != null)
            UIKit.Label(window, captain.AbilityDescription, 11, color: UIKit.MutedTextColor);
        UIKit.Label(window, $"Joins the roster with no soldiers. You have {profile.Gold} Gold.", 11, TextAnchor.MiddleCenter,
            UIKit.MutedTextColor);
        PurchaseButtons(window, scope, $"Recruit {price}", () => BuyCaptain(slot));
    }
}
