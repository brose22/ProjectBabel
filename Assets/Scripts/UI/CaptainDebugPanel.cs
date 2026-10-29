using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The full selected-captain panel for testing, toggled with F2 and docked bottom-left: everything
// the captain widget shows plus likes and dislikes, Gold draw buttons and soldier class switching.
// MatchHUD only creates it in the editor and development builds.
public class CaptainDebugPanel
{
    const float PanelWidth = 310f;
    const float SlotLabelWidth = 52f;
    static readonly string[] DrawRateLabels = { "Low", "Normal", "High" };
    static readonly SupportEffect[] StatEffects = { SupportEffect.ActSpeed, SupportEffect.Damage, SupportEffect.Defense };

    readonly MatchManager match;
    readonly PlacementController placement;
    readonly RectTransform towerPanel;
    readonly List<Action> panelRefreshers = new();
    string towerPanelKey;

    public bool IsOpen { get; private set; }

    public CaptainDebugPanel(Transform canvas, MatchManager match, PlacementController placement)
    {
        this.match = match;
        this.placement = placement;
        towerPanel = UIKit.Box(canvas, "Captain Debug Panel");
        UIKit.Place(towerPanel, new Vector2(0f, 0f), new Vector2(UIKit.Pad, UIKit.Pad), new Vector2(PanelWidth, 0f));
        UIKit.Layout<VerticalLayoutGroup>(towerPanel, UIKit.Pad, 3, fitContent: true);
        towerPanel.gameObject.SetActive(false);
    }

    public void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f2Key.wasPressedThisFrame) IsOpen = !IsOpen;

        Tower tower = IsOpen ? placement.Selected : null;
        string key = TowerPanelKey(tower);
        if (key != towerPanelKey)
        {
            towerPanelKey = key;
            panelRefreshers.Clear();
            UIKit.Clear(towerPanel);
            towerPanel.gameObject.SetActive(tower != null);
            if (tower != null) BuildTowerPanel(tower);
        }
        if (tower != null) UIKit.Refresh(panelRefreshers);
    }

    // The panel's structure depends on these; anything else changes through refreshers.
    string TowerPanelKey(Tower tower)
    {
        if (tower == null) return "";
        return $"{tower.GetHashCode()}|{tower.Soldiers.Count}|{tower.IsDisabled}|{match.CanRestore(tower)}|{placement.IsMoving}";
    }

    void BuildTowerPanel(Tower tower)
    {
        CaptainData captain = tower.Captain;
        Transform panel = towerPanel;

        Transform title = Row(panel);
        Text name = UIKit.Label(title, captain.displayName, 16);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        UIKit.LiveLabel(title, () => $"Lv {tower.Level}", panelRefreshers, 14, TextAnchor.MiddleRight, UIKit.AccentColor);
        string element = $"<color={UIKit.Hex(UIKit.ElementColor(captain.element))}>{captain.element}</color>";
        UIKit.LiveLabel(panel, () => $"{element} {captain.role}   ·   Bond {BondMath.RankName(tower.BondRank)} ({tower.Approval:0})",
            panelRefreshers, 12, color: UIKit.MutedTextColor);

        BuildCondition(panel, tower);
        UIKit.LiveBar(panel, () => tower.Reserve / (float)Mathf.Max(1, captain.resourceCapacity),
            () => $"Reserve {tower.Reserve}/{captain.resourceCapacity}", UIKit.ReserveBarColor, panelRefreshers);
        UIKit.LiveBar(panel, () => tower.IsMaxLevel ? 1f : tower.Xp / (float)Mathf.Max(1, tower.XpForNextLevel),
            () => tower.IsMaxLevel ? $"XP {tower.Xp} (max level)" : $"XP {tower.Xp}/{tower.XpForNextLevel}", UIKit.XpBarColor, panelRefreshers);

        UIKit.LiveLabel(panel, () => $"Power {tower.SoldierPower:0}    Range {captain.range:0.#}    Speed {captain.speed:0.##}", panelRefreshers, 12);
        Text boosts = UIKit.LiveLabel(panel, () => BoostLabel(tower), panelRefreshers, 12);
        panelRefreshers.Add(() => boosts.color = HasBoosts(tower) ? UIKit.GoodColor : UIKit.MutedTextColor);
        TagLine(panel, "Likes", captain.likes, UIKit.GoodColor);
        TagLine(panel, "Dislikes", captain.dislikes, UIKit.BadColor);

        BuildAbility(panel, tower);
        BuildSoldierSlots(panel, tower);

        UIKit.Header(panel, "Gold draw");
        Transform drawRow = Row(panel);
        for (int i = 0; i < DrawRateLabels.Length; i++)
        {
            var rate = (DrawRate)i;
            Flexible(UIKit.LiveButton(drawRow, () => DrawRateLabels[(int)rate], () => tower.SetDrawRate(rate), panelRefreshers,
                selected: () => tower.DrawRate == rate, height: 22f));
        }

        UIKit.Size(UIKit.Group(panel, "Gap"), height: 2f);
        Transform actions = Row(panel);
        if (placement.IsMoving)
            Flexible(UIKit.Button(actions, "Cancel move", placement.CancelMove));
        else
            Flexible(UIKit.LiveButton(actions, () => match.MoveCost(tower) > 0 ? $"Move ({match.MoveCost(tower)} Gold)" : "Move (free)",
                placement.BeginMove, panelRefreshers, () => match.CanMove(tower) && match.CanAffordMove(tower)));
        Flexible(UIKit.Button(actions, "Sell (returns reserve)", placement.SellSelected));
    }

    // A row that spans the panel, for buttons that share its width.
    static Transform Row(Transform parent)
    {
        RectTransform row = UIKit.Group(parent, "Row");
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 3);
        UIKit.Size(row, flexibleWidth: 1f);
        return row;
    }

    static void Flexible(Button button, float height = 24f) => UIKit.Size(button, height: height, flexibleWidth: 1f);

    static void TagLine(Transform panel, string caption, CaptainTag[] tags, Color color) =>
        UIKit.Label(panel, $"<color={UIKit.Hex(color)}>{caption}</color>  {string.Join(", ", tags)}", 11, color: UIKit.MutedTextColor);

    void BuildCondition(Transform panel, Tower tower)
    {
        if (!tower.IsDisabled)
        {
            UIKit.LiveBar(panel, () => tower.Health / tower.MaxHealth,
                () => $"Health {Mathf.CeilToInt(tower.Health)}/{Mathf.CeilToInt(tower.MaxHealth)}", UIKit.HealthBarColor, panelRefreshers);
            return;
        }
        if (!match.CanRestore(tower))
        {
            UIKit.Label(panel, $"DISABLED  (restorable after wave {tower.RestorableAfterWave})", 12, color: UIKit.BadColor).fontStyle = FontStyle.Bold;
            return;
        }
        Transform row = Row(panel);
        Text disabled = UIKit.Label(row, "DISABLED", 12, color: UIKit.BadColor);
        disabled.fontStyle = FontStyle.Bold;
        UIKit.Size(disabled, flexibleWidth: 1f);
        UIKit.LiveButton(row, () => $"Restore ({match.RestoreCost(tower)} Gold)", () => match.TryRestoreTower(tower), panelRefreshers,
            () => RunState.Current.Gold >= match.RestoreCost(tower), width: 160f, height: 22f);
    }

    // Name and Use button, a charge meter, then what the special does.
    void BuildAbility(Transform panel, Tower tower)
    {
        UIKit.Header(panel, "Special");
        AbilityData ability = tower.Captain.ability;
        if (ability == null)
        {
            UIKit.Label(panel, "No special ability", 12, color: UIKit.MutedTextColor);
            return;
        }
        Transform row = Row(panel);
        Text name = UIKit.Label(row, ability.displayName, 13);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        UIKit.LiveButton(row, () => "Use (Q)", () => tower.TryUseAbility(), panelRefreshers, () => tower.CanUseAbility,
            selected: () => tower.CanUseAbility, width: 80f, height: 22f);
        UIKit.LiveBar(panel, () => tower.SpecialCharge,
            () => tower.IsSpecialReady ? "Ready" : $"Charge {Mathf.FloorToInt(tower.SpecialCharge * 100f)}%", UIKit.ChargeBarColor, panelRefreshers, 12f);
        if (!string.IsNullOrEmpty(ability.description))
            UIKit.Label(panel, ability.description, 11, color: UIKit.MutedTextColor);
    }

    // Slots 3 and 4 open at bond ranks B and S (GDD).
    static string SlotLabel(int slot) => slot switch
    {
        2 => "Slot 3 (B)",
        3 => "Slot 4 (S)",
        _ => $"Slot {slot + 1}",
    };

    // Per slot: its label (red when the soldier is down), then a button per class. The current
    // class is gold; others show the reserve cost of switching in gold and grey out when the
    // reserve can't cover it or the tower is disabled.
    void BuildSoldierSlots(Transform panel, Tower tower)
    {
        UIKit.Header(panel, "Soldiers");
        var classes = match.Data.ChoosableClasses;
        string cost = UIKit.Hex(UIKit.AccentColor), unaffordable = UIKit.Hex(UIKit.DisabledTextColor);
        for (int i = 0; i < tower.Soldiers.Count; i++)
        {
            int slot = i;
            Transform row = Row(panel);
            Text label = UIKit.LiveLabel(row, () => tower.Soldiers[slot].IsDead ? $"{SlotLabel(slot)}\n<size=9>down</size>" : SlotLabel(slot),
                panelRefreshers, 11);
            UIKit.Size(label, SlotLabelWidth);
            panelRefreshers.Add(() => label.color = tower.Soldiers[slot].IsDead ? UIKit.BadColor : UIKit.MutedTextColor);
            foreach (SoldierClassData soldierClass in classes)
            {
                if (soldierClass == null) continue;
                SoldierClassData option = soldierClass;
                bool current() => tower.Soldiers[slot].Class == option;
                bool affordable() => !tower.IsDisabled && tower.Reserve >= tower.RespawnCostOf(option);
                Button button = UIKit.LiveButton(row,
                    () => current() ? option.displayName
                        : $"{option.displayName} <size=9><color={(affordable() ? cost : unaffordable)}>{tower.RespawnCostOf(option)}g</color></size>",
                    () => tower.SetSlotClass(slot, option), panelRefreshers, () => current() || affordable(), current, height: 22f);
                Flexible(button, 22f);
                Text text = button.GetComponentInChildren<Text>();
                text.fontSize = 11;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.rectTransform.offsetMin = new Vector2(1f, text.rectTransform.offsetMin.y);
                text.rectTransform.offsetMax = new Vector2(-1f, 0f);
            }
        }
    }

    static bool HasBoosts(Tower tower)
    {
        foreach (SupportEffect effect in StatEffects)
            if (tower.Boost(effect) > 0f) return true;
        return false;
    }

    // Boosts every tower has from Buffer specials cast this wave.
    static string BoostLabel(Tower tower)
    {
        var parts = new List<string>();
        foreach (SupportEffect effect in StatEffects)
        {
            float boost = tower.Boost(effect);
            if (boost > 0f) parts.Add($"{effect} +{boost * 100f:0}%");
        }
        return parts.Count == 0 ? "Wave boosts: none" : "Wave boosts: " + string.Join(", ", parts);
    }
}
