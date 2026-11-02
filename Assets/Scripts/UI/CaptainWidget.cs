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
    // Soldier slots per row of the diamond.
    static readonly int[] SoldierRows = { 2, 2 };
    static readonly string[] DrawRateLabels = { "Low", "Normal", "High" };

    readonly Canvas canvas;
    readonly float topMargin;
    readonly MatchManager match;
    readonly PlacementController placement;
    readonly RectTransform panel;
    readonly List<Action> refreshers = new();
    readonly CaptainPanelParts parts;
    readonly UIFocusScope focus;
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
        // Only takes gamepad focus when asked to; a selected tower shouldn't stop the tile cursor.
        focus = UIKit.Focus(panel, () => placement.WidgetFocused = false);
        focus.enabled = false;
        panel.gameObject.SetActive(false);
    }

    public void Update()
    {
        // Out of the way while the player picks where to move; right-click or Esc still cancels.
        Tower tower = placement.IsMoving ? null : placement.Selected;
        focus.enabled = tower != null && placement.WidgetFocused;
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

    // Just outside the captain's attack range, so the range circle stays visible: to the right
    // when the widget fits there, else to the left; when neither fits, on the roomier side and
    // pushed back on screen. Vertically centred on the captain, kept between the top bar and the
    // party bar.
    void PlaceBeside(Tower tower)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector2 screen = ((RectTransform)canvas.transform).rect.size;
        Vector2 centre = cam.WorldToScreenPoint(tower.transform.position) / canvas.scaleFactor;
        float edge = cam.WorldToScreenPoint(tower.transform.position + Vector3.right * tower.Range).x / canvas.scaleFactor;
        float gap = edge - centre.x + UIKit.Gap;
        Vector2 size = panel.rect.size;

        float right = centre.x + gap, left = centre.x - gap - size.x;
        float minX = UIKit.Pad, maxX = screen.x - UIKit.Pad - size.x;
        float x;
        if (right <= maxX) x = right;
        else if (left >= minX) x = left;
        else x = Mathf.Clamp(screen.x - centre.x >= centre.x ? right : left, minX, Mathf.Max(minX, maxX));

        float minY = UIKit.Pad + PartyBar.Height + UIKit.Gap, maxY = screen.y - topMargin - size.y;
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

    // The soldiers as a 2 x 2 grid. Each shows its
    // class, red while the soldier is down. Slots the bond hasn't opened yet show a dash.
    void BuildSoldiers(Tower tower)
    {
        UIKit.Header(panel, "Soldiers");
        int slot = 0;
        foreach (int count in SoldierRows)
        {
            Transform row = CaptainPanelParts.Row(panel);
            for (int i = 0; i < count && slot < RunState.MaxSoldierSlots; i++, slot++)
                BuildSoldierCell(row, tower, slot);
        }
    }

    void BuildSoldierCell(Transform row, Tower tower, int slot)
    {
        // Each soldier sits on its own inset background.
        RectTransform cell = UIKit.Box(row, "Soldier", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(cell, 3, 0);
        UIKit.Size(cell, flexibleWidth: 1f);
        if (slot >= tower.Soldiers.Count)
        {
            UIKit.Size(UIKit.Label(cell, "-", 10, TextAnchor.MiddleCenter, UIKit.DisabledTextColor), flexibleWidth: 1f);
            return;
        }
        Text soldier = UIKit.LiveLabel(cell, () => tower.Soldiers[slot].Class.displayName, refreshers, 10);
        soldier.alignment = TextAnchor.MiddleCenter;
        UIKit.Size(soldier, flexibleWidth: 1f);
        refreshers.Add(() => soldier.color = tower.Soldiers[slot].IsDead ? UIKit.BadColor : UIKit.TextColor);
    }
}
