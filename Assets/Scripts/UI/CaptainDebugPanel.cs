using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The full selected-captain panel for testing, toggled with F2 and docked bottom-left: everything
// the captain widget shows plus likes and dislikes, Gold draw buttons and what each soldier adds.
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
        if (GameInput.KeyDown(Key.F2)) IsOpen = !IsOpen;

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
        Captain captain = tower.Captain;
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

    // Per slot: its label (red when the soldier is down), then the soldier in its rarity's colour
    // with its damage type, what it adds to the captain and its respawn cost. Soldiers are
    // equipped on the world map, so this only reads.
    void BuildSoldierSlots(Transform panel, Tower tower)
    {
        UIKit.Header(panel, "Soldiers");
        int open = match.SoldierCountFor(tower.Captain);
        string muted = UIKit.Hex(UIKit.MutedTextColor);
        for (int i = 0; i < RunState.MaxSoldierSlots; i++)
        {
            int slot = i;
            Soldier soldier = tower.SoldierInSlot(slot);
            Transform row = CaptainPanelParts.Row(panel);
            Text label = UIKit.LiveLabel(row,
                () => soldier != null && soldier.IsDead ? $"{CaptainPanelParts.SlotLabel(slot)}\n<size=9>down</size>" : CaptainPanelParts.SlotLabel(slot),
                panelRefreshers, 11);
            UIKit.Size(label, SlotLabelWidth);
            panelRefreshers.Add(() => label.color = soldier != null && soldier.IsDead ? UIKit.BadColor : UIKit.MutedTextColor);
            string text = soldier == null ? (slot < open ? "empty" : "locked")
                : $"{CaptainPanelParts.SoldierName(soldier.Item, soldier.Class)} <color={muted}>{soldier.Class.damageType}  " +
                  $"{CaptainPanelParts.SoldierBonus(soldier.Item, soldier.Class)}  {tower.RespawnCostOf(soldier.Class)}g</color>";
            UIKit.Size(UIKit.Label(row, text, 11, color: soldier == null ? UIKit.DisabledTextColor : UIKit.TextColor), height: 22f, flexibleWidth: 1f);
        }
    }
}
