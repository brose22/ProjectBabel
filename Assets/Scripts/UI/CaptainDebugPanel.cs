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

    readonly MatchManager match;
    readonly PlacementController placement;
    readonly RectTransform towerPanel;
    readonly List<Action> panelRefreshers = new();
    readonly CaptainPanelParts parts;
    string towerPanelKey;

    public bool IsOpen { get; private set; }

    public CaptainDebugPanel(Transform canvas, MatchManager match, PlacementController placement)
    {
        this.match = match;
        this.placement = placement;
        parts = new CaptainPanelParts(match, placement, panelRefreshers);
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

        parts.Title(panel, tower);
        parts.Condition(panel, tower);
        parts.ReserveAndXp(panel, tower);
        parts.Stats(panel, tower);
        TagLine(panel, "Likes", captain.likes, UIKit.GoodColor);
        TagLine(panel, "Dislikes", captain.dislikes, UIKit.BadColor);

        parts.Ability(panel, tower);
        BuildSoldierSlots(panel, tower);

        UIKit.Header(panel, "Gold draw");
        Transform drawRow = CaptainPanelParts.Row(panel);
        for (int i = 0; i < DrawRateLabels.Length; i++)
        {
            var rate = (DrawRate)i;
            CaptainPanelParts.Flexible(UIKit.LiveButton(drawRow, () => DrawRateLabels[(int)rate], () => tower.SetDrawRate(rate), panelRefreshers,
                selected: () => tower.DrawRate == rate, height: 22f));
        }

        parts.Actions(panel, tower);
    }

    static void TagLine(Transform panel, string caption, CaptainTag[] tags, Color color) =>
        UIKit.Label(panel, $"<color={UIKit.Hex(color)}>{caption}</color>  {string.Join(", ", tags)}", 11, color: UIKit.MutedTextColor);

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
            Transform row = CaptainPanelParts.Row(panel);
            Text label = UIKit.LiveLabel(row,
                () => tower.Soldiers[slot].IsDead ? $"{CaptainPanelParts.SlotLabel(slot)}\n<size=9>down</size>" : CaptainPanelParts.SlotLabel(slot),
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
                CaptainPanelParts.Flexible(button, 22f);
                Text text = button.GetComponentInChildren<Text>();
                text.fontSize = 11;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.rectTransform.offsetMin = new Vector2(1f, text.rectTransform.offsetMin.y);
                text.rectTransform.offsetMax = new Vector2(-1f, 0f);
            }
        }
    }
}
