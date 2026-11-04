using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The captains on the field, along the bottom of the match screen; the captain cycler at the
// cursor picks who is placed next. Clicking a card fires that captain's special once its meter is
// full. On the gamepad the cycler's captain is highlighted here and the special button fires
// theirs. Cards rebuild only when the field changes.
public class PartyBar
{
    public const float Height = 48f;
    const float CardWidth = 112f;
    // The 32 px portraits at 1x, so their pixels stay square.
    const float PortraitSize = 32f;
    const float TipWidth = 220f;
    // Seconds the highlighted captain's tip stays up after stepping to them.
    const float PadTipSeconds = 2.5f;
    static readonly Color CardColor = new(0.17f, 0.2f, 0.28f, 1f);

    readonly MatchManager match;
    readonly RectTransform bar;
    readonly List<Action> refreshers = new();
    string key;
    Captain highlighted;
    float tipUntil;

    public PartyBar(Transform parent, MatchManager match)
    {
        this.match = match;
        bar = UIKit.Group(parent, "Party Bar");
        UIKit.Place(bar, new Vector2(0.5f, 0f), new Vector2(0f, UIKit.Pad), new Vector2(0f, Height));
        UIKit.Layout<HorizontalLayoutGroup>(bar, 0, UIKit.Gap, fitContent: true);
    }

    public void Update()
    {
        RunState run = RunState.Current;
        UpdatePad(run);
        string newKey = Key(run);
        if (newKey != key)
        {
            key = newKey;
            refreshers.Clear();
            UIKit.Clear(bar);
            foreach (Captain captain in run.Party)
            {
                Tower tower = TowerOf(captain);
                if (tower != null) Card(captain, tower);
            }
        }
        UIKit.Refresh(refreshers);
    }

    // The cycler's captain is the highlighted one; stepping onto a placed captain shows their tip.
    void UpdatePad(RunState run)
    {
        if (match.IsOver) return;
        Captain selected = match.SelectedCaptain;
        if (selected != highlighted)
        {
            highlighted = selected;
            tipUntil = Time.unscaledTime + PadTipSeconds;
        }
        if (highlighted == null || !GameInput.UseSpecial) return;
        Tower tower = TowerOf(highlighted);
        if (tower != null) tower.TryUseAbility();
    }

    static Tower TowerOf(Captain captain) => Tower.All.Find(t => t != null && t.Captain == captain);

    // Which party captains are on the field, and in which towers.
    static string Key(RunState run)
    {
        var parts = new List<string>();
        foreach (Captain captain in run.Party)
            if (captain != null) parts.Add($"{captain.displayName}:{TowerOf(captain)?.GetHashCode()}");
        return string.Join("|", parts);
    }

    // Portrait, name, then the special's meter. The gold ring marks a special ready to fire.
    void Card(Captain captain, Tower tower)
    {
        RectTransform card = UIKit.Box(bar, captain.displayName, CardColor, blocksClicks: false);
        UIKit.Size(card, CardWidth, Height);
        UIKit.Layout<HorizontalLayoutGroup>(card, 6, 5);
        UIKit.Clickable(card, () => tower.TryUseAbility());

        RectTransform frame = UIKit.Box(card, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.Portrait, PortraitSize);

        RectTransform info = UIKit.Group(card, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 2);
        UIKit.Size(info, flexibleWidth: 1f);
        Text name = UIKit.Label(info, captain.displayName, 12);
        name.fontStyle = FontStyle.Bold;

        Func<bool> actionable;
        if (tower.HasAbility)
        {
            UIKit.LiveBar(info, () => tower.SpecialCharge,
                () => tower.IsSpecialReady ? "Ready" : $"{Mathf.FloorToInt(tower.SpecialCharge * 100f)}%", UIKit.ChargeBarColor, refreshers, 12f);
            actionable = () => tower.CanUseAbility;
        }
        else
        {
            UIKit.Label(info, "No special", 9, color: UIKit.MutedTextColor);
            actionable = () => false;
        }

        RectTransform tip = captain.Ability != null ? UIKit.HoverTip(card, captain.Ability.displayName, captain.Ability.description, TipWidth) : null;

        Image ring = UIKit.Outline(card, UIKit.AccentColor);
        ring.rectTransform.offsetMin = new Vector2(2f, 2f);
        ring.rectTransform.offsetMax = new Vector2(-2f, -2f);
        refreshers.Add(() =>
        {
            ring.enabled = actionable();
            name.color = ring.enabled ? UIKit.AccentColor : UIKit.TextColor;
        });

        // White like the menus' focus ring, and just outside the gold one.
        Image highlight = UIKit.Outline(card, Color.white);
        refreshers.Add(() =>
        {
            bool isHighlighted = GameInput.UsingGamepad && highlighted == captain;
            highlight.enabled = isHighlighted;
            if (tip != null && GameInput.UsingGamepad) tip.gameObject.SetActive(isHighlighted && Time.unscaledTime < tipUntil);
        });
        // The special button works on the highlighted captain, once their meter is full.
        if (tower.HasAbility)
            UIKit.PadBadge(card, PadGlyph.RightTrigger, () => highlighted == captain && tower.CanUseAbility && GameInput.PadInMatch, Vector2.one);
    }
}
