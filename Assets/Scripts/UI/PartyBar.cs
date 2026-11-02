using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The party along the bottom of the match screen, a card per captain still with the run.
// Clicking a captain who isn't on the field makes them the next one placed; clicking one who is
// fires their special once its meter is full. The gamepad steps a highlight along the bar instead:
// landing on a captain makes them next, or moves the tile cursor to their tower, and the special
// button fires the highlighted one. Cards rebuild only when the party or the field changes.
public class PartyBar
{
    public const float Height = 48f;
    const float CardWidth = 112f;
    // The 32 px portraits at 1x, so their pixels stay square.
    const float PortraitSize = 32f;
    const float TipWidth = 220f;
    // Seconds the highlighted captain's tip stays up after stepping to them.
    const float PadTipSeconds = 2.5f;
    // How far the shoulder button icons sit from the ends of the bar.
    const float BadgeGap = 14f;
    static readonly Color CardColor = new(0.17f, 0.2f, 0.28f, 1f);

    readonly MatchManager match;
    readonly PlacementController placement;
    readonly RectTransform bar;
    readonly List<Action> refreshers = new();
    string key;
    CaptainData highlighted;
    float tipUntil;

    public PartyBar(Transform parent, MatchManager match, PlacementController placement)
    {
        this.match = match;
        this.placement = placement;
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
            foreach (CaptainData captain in run.Party)
                if (!run.HasDeparted(captain)) Card(captain, TowerOf(captain));
            // The shoulder buttons step along the bar, so they sit at its ends.
            UIKit.PadBadge(bar, PadGlyph.LeftShoulder, () => GameInput.PadInMatch, new Vector2(0f, 0.5f), Vector2.left * BadgeGap);
            UIKit.PadBadge(bar, PadGlyph.RightShoulder, () => GameInput.PadInMatch, new Vector2(1f, 0.5f), Vector2.right * BadgeGap);
        }
        UIKit.Refresh(refreshers);
    }

    void UpdatePad(RunState run)
    {
        if (match.IsOver) return;
        int step = GameInput.PartyStep;
        if (step != 0) StepHighlight(run, step);
        if (highlighted == null || !GameInput.UseSpecial) return;
        Tower tower = TowerOf(highlighted);
        if (tower != null) tower.TryUseAbility();
    }

    // The first step starts from the captain up next.
    void StepHighlight(RunState run, int step)
    {
        List<CaptainData> present = run.Party.FindAll(captain => captain != null && !run.HasDeparted(captain));
        if (present.Count == 0) return;
        int index = present.IndexOf(highlighted != null ? highlighted : match.NextCaptain);
        highlighted = present[(Mathf.Max(index, 0) + step + present.Count) % present.Count];
        tipUntil = Time.unscaledTime + PadTipSeconds;

        Tower tower = TowerOf(highlighted);
        if (tower != null) placement.JumpCursor(tower.Cell);
        else match.ChooseNextCaptain(highlighted);
    }

    static Tower TowerOf(CaptainData captain) => Tower.All.Find(t => t.Captain == captain);

    // Who is with the run and which of them are on the field.
    static string Key(RunState run)
    {
        var parts = new List<string>();
        foreach (CaptainData captain in run.Party)
            parts.Add($"{captain.displayName}:{run.HasDeparted(captain)}:{TowerOf(captain)?.GetHashCode()}");
        return string.Join("|", parts);
    }

    // Portrait, name, then the special's meter once placed or "Up next" until then. The gold ring
    // marks the card a click acts on now: the next captain to place, or a special ready to fire.
    void Card(CaptainData captain, Tower tower)
    {
        RectTransform card = UIKit.Box(bar, captain.displayName, CardColor, blocksClicks: false);
        UIKit.Size(card, CardWidth, Height);
        UIKit.Layout<HorizontalLayoutGroup>(card, 6, 5);
        if (tower != null) UIKit.Clickable(card, () => tower.TryUseAbility());
        else UIKit.Clickable(card, () => match.ChooseNextCaptain(captain));

        RectTransform frame = UIKit.Box(card, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.portrait, PortraitSize);

        RectTransform info = UIKit.Group(card, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 2);
        UIKit.Size(info, flexibleWidth: 1f);
        Text name = UIKit.Label(info, captain.displayName, 12);
        name.fontStyle = FontStyle.Bold;

        Func<bool> actionable;
        if (tower == null)
        {
            Func<bool> isNext = () => match.NextCaptain == captain && match.CanPlaceTower;
            Text status = UIKit.LiveLabel(info, () => isNext() ? "UP NEXT" : "Not placed", refreshers, 9);
            status.fontStyle = FontStyle.Bold;
            refreshers.Add(() => status.color = isNext() ? UIKit.AccentColor : UIKit.MutedTextColor);
            actionable = isNext;
        }
        else if (tower.HasAbility)
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

        RectTransform tip = captain.ability != null ? UIKit.HoverTip(card, captain.ability.displayName, captain.ability.description, TipWidth) : null;

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
        if (tower != null && tower.HasAbility)
            UIKit.PadBadge(card, PadGlyph.RightTrigger, () => highlighted == captain && tower.CanUseAbility && GameInput.PadInMatch, Vector2.one);
    }
}
