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
    // Half a map tile, in world units: the widget opens just clear of the captain's tile.
    const float TowerHalfWidth = 0.5f;
    static readonly string[] DrawRateLabels = { "Low", "Normal", "High" };

    readonly Canvas canvas;
    readonly float topMargin;
    readonly MatchManager match;
    readonly PlacementController placement;
    readonly RectTransform panel;
    readonly List<Action> refreshers = new();
    readonly CaptainPanelParts parts;
    string key;

    public CaptainWidget(Canvas canvas, float topMargin, MatchManager match, PlacementController placement)
    {
        this.canvas = canvas;
        this.topMargin = topMargin;
        this.match = match;
        this.placement = placement;
        parts = new CaptainPanelParts(match, placement, refreshers);
        panel = UIKit.Box(canvas.transform, "Captain Widget");
        UIKit.Place(panel, Vector2.zero, Vector2.zero, new Vector2(Width, 0f));
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
        if (tower == null) return;
        UIKit.Refresh(refreshers);
        // Size the box to this frame's text before placing it, so it never pokes off screen.
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        PlaceBeside(tower);
    }

    // To the right of the captain when the widget fits there, else to the left; when neither
    // fits, on the roomier side and pushed back on screen. Vertically centred on the captain,
    // kept between the top bar and the bottom edge.
    void PlaceBeside(Tower tower)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector2 screen = ((RectTransform)canvas.transform).rect.size;
        Vector2 centre = cam.WorldToScreenPoint(tower.transform.position) / canvas.scaleFactor;
        float edge = cam.WorldToScreenPoint(tower.transform.position + Vector3.right * TowerHalfWidth).x / canvas.scaleFactor;
        float gap = edge - centre.x + UIKit.Gap;
        Vector2 size = panel.rect.size;

        float right = centre.x + gap, left = centre.x - gap - size.x;
        float minX = UIKit.Pad, maxX = screen.x - UIKit.Pad - size.x;
        float x;
        if (right <= maxX) x = right;
        else if (left >= minX) x = left;
        else x = Mathf.Clamp(screen.x - centre.x >= centre.x ? right : left, minX, Mathf.Max(minX, maxX));

        float minY = UIKit.Pad, maxY = screen.y - topMargin - size.y;
        float y = Mathf.Clamp(centre.y - size.y / 2f, minY, Mathf.Max(minY, maxY));
        // Whole reference pixels keep the pixel-art frame crisp.
        panel.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
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
