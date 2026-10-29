using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The selected captain's widget: condition, bars, stats, special, soldiers and Gold draw (both
// read-only) and move/sell. Soldiers are set on the map and Gold draw by captain requests;
// CaptainDebugPanel has the controls for both.
public class CaptainWidget
{
    const float Width = 250f;
    const float SlotLabelWidth = 52f;
    static readonly string[] DrawRateLabels = { "Low", "Normal", "High" };

    readonly MatchManager match;
    readonly PlacementController placement;
    readonly RectTransform panel;
    readonly List<Action> refreshers = new();
    readonly CaptainPanelParts parts;
    string key;

    public CaptainWidget(Transform canvas, MatchManager match, PlacementController placement)
    {
        this.match = match;
        this.placement = placement;
        parts = new CaptainPanelParts(match, placement, refreshers);
        panel = UIKit.Box(canvas, "Captain Widget");
        UIKit.Place(panel, new Vector2(0f, 0f), new Vector2(UIKit.Pad, UIKit.Pad), new Vector2(Width, 0f));
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 3, fitContent: true);
        panel.gameObject.SetActive(false);
    }

    public void Update()
    {
        Tower tower = placement.Selected;
        string newKey = Key(tower);
        if (newKey != key)
        {
            key = newKey;
            refreshers.Clear();
            UIKit.Clear(panel);
            panel.gameObject.SetActive(tower != null);
            if (tower != null) Build(tower);
        }
        if (tower != null) UIKit.Refresh(refreshers);
    }

    // The widget's structure depends on these; anything else changes through refreshers.
    string Key(Tower tower)
    {
        if (tower == null) return "";
        return $"{tower.GetHashCode()}|{tower.Soldiers.Count}|{tower.IsDisabled}|{match.CanRestore(tower)}|{placement.IsMoving}";
    }

    void Build(Tower tower)
    {
        parts.Title(panel, tower);
        parts.Condition(panel, tower);
        parts.ReserveAndXp(panel, tower);
        parts.Stats(panel, tower);
        parts.Ability(panel, tower);
        BuildSoldiers(tower);
        string accent = UIKit.Hex(UIKit.AccentColor);
        UIKit.LiveLabel(panel, () => $"Gold draw  <color={accent}>{DrawRateLabels[(int)tower.DrawRate]}</color>",
            refreshers, 12, color: UIKit.MutedTextColor);
        parts.Actions(panel, tower);
    }

    // A line per slot with its soldier's class, red while the soldier is down. Slots the bond
    // hasn't opened yet say so.
    void BuildSoldiers(Tower tower)
    {
        UIKit.Header(panel, "Soldiers");
        for (int i = 0; i < RunState.MaxSoldierSlots; i++)
        {
            int slot = i;
            Transform row = CaptainPanelParts.Row(panel);
            UIKit.Size(UIKit.Label(row, CaptainPanelParts.SlotLabel(slot), 11, color: UIKit.MutedTextColor), SlotLabelWidth);
            if (slot >= tower.Soldiers.Count)
            {
                UIKit.Label(row, "Locked", 12, color: UIKit.DisabledTextColor);
                continue;
            }
            Text soldier = UIKit.LiveLabel(row,
                () => tower.Soldiers[slot].IsDead ? $"{tower.Soldiers[slot].Class.displayName}  <size=10>down</size>" : tower.Soldiers[slot].Class.displayName,
                refreshers, 12);
            UIKit.Size(soldier, flexibleWidth: 1f);
            refreshers.Add(() => soldier.color = tower.Soldiers[slot].IsDead ? UIKit.BadColor : UIKit.TextColor);
        }
    }
}
