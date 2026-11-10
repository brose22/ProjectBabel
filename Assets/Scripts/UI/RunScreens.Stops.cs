using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Stop screens: rest, shop, event and the release choice.
public partial class RunScreens
{
    void BuildStop()
    {
        RectTransform root = NewScreen("Stop");
        RectTransform panel = UIKit.Box(root, "Panel");
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(panel, 16, 8, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        switch (match.City.kind)
        {
            case NodeKind.Rest: BuildRest(panel); break;
            case NodeKind.Event when match.StopEvent != null: BuildEvent(panel, match.StopEvent); break;
            default: UIKit.Button(panel, "Move on", match.LeaveStop, 220f, 32f); break;
        }
    }

    // Title and description, then the run's health and Gold, kept current while shopping.
    void StopHeader(RectTransform panel, string title, string description)
    {
        RunState run = RunState.Current;
        UIKit.Label(panel, title, 26, TextAnchor.MiddleCenter);
        UIKit.Label(panel, description, 13, TextAnchor.MiddleCenter);
        UIKit.LiveLabel(panel, () => $"Health {run.Health}/{run.MaxHealth}    Gold {run.Gold}", refreshers, 13, TextAnchor.MiddleCenter,
            UIKit.MutedTextColor);
    }

    void BuildRest(RectTransform panel)
    {
        StopHeader(panel, "REST", "The army makes camp and tends its wounds.");
        RunState run = RunState.Current;
        int amount = Mathf.Min(match.RestAmount, run.MaxHealth - run.Health);
        UIKit.Button(panel, amount > 0 ? $"Rest (+{amount} health)" : "Rest (already at full health)", match.Rest, 260f, 32f);
    }

    void BuildShop(RectTransform panel, Action leave)
    {
        RunState run = RunState.Current;
        StopHeader(panel, "SHOP", "Buy gifts to carry with you. Give them to captains from the party window on the map.");
        // The shelves: a grid of cards, each with the gift's icon, name, price and how many you hold.
        RectTransform grid = UIKit.Group(panel, "Shelves");
        var cells = grid.gameObject.AddComponent<GridLayoutGroup>();
        cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cells.constraintCount = ShopColumns;
        cells.cellSize = ShopCardSize;
        cells.spacing = new Vector2(8f, 8f);
        cells.childAlignment = TextAnchor.UpperCenter;
        List<GiftData> stock = match.ShopStock;
        int rows = Mathf.Max(1, Mathf.CeilToInt(stock.Count / (float)ShopColumns));
        UIKit.Size(grid, height: rows * ShopCardSize.y + (rows - 1) * 8f, flexibleWidth: 1f);
        foreach (GiftData gift in stock)
        {
            RectTransform card = UIKit.Box(grid, gift.displayName, CardColor, blocksClicks: false);
            UIKit.Layout<VerticalLayoutGroup>(card, 6, 3).childAlignment = TextAnchor.UpperCenter;
            UIKit.Icon(card, gift.icon, 32f);
            UIKit.Label(card, gift.displayName, 13, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            UIKit.LiveLabel(card, () => $"{gift.cost} Gold   (owned {run.GiftCount(gift)})", refreshers, 11, TextAnchor.MiddleCenter,
                UIKit.MutedTextColor);
            UIKit.Size(UIKit.LiveButton(card, () => "Buy", () => match.BuyGift(gift), refreshers, () => match.CanBuyGift(gift), height: 25f),
                height: 25f, flexibleWidth: 1f);
        }
        UIKit.CloseButton(panel, "Back", leave);
    }

    void BuildEvent(RectTransform panel, EventData data)
    {
        StopHeader(panel, data.title.ToUpperInvariant(), data.text);
        foreach (EventChoice choice in data.choices)
        {
            string effects = choice.Describe();
            UIKit.LiveButton(panel, () => effects.Length > 0 ? $"{choice.label}  ({effects})" : choice.label,
                () => match.TakeEventChoice(choice), refreshers, () => match.CanTakeEventChoice(choice), width: 460f, height: 28f);
        }
    }

    // The roster is full and an event brought a newcomer: release the newcomer or a roster captain.
    // Whoever goes leaves for good, and their soldiers go back to the inventory.
    void BuildReleaseChoice()
    {
        RunState run = RunState.Current;
        Captain newcomer = match.PendingRecruit;
        Captain chosen = null;

        RectTransform root = NewScreen("Release");
        RectTransform panel = UIKit.Box(root, "Panel");
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(panel, 16, 8, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        UIKit.Label(panel, "THE ROSTER IS FULL", 24, TextAnchor.MiddleCenter);
        UIKit.Label(panel, $"{newcomer.displayName} wants to join, but the roster holds {Profile.MaxRoster}. Release the newcomer or " +
                           "a captain from the roster. They leave for good, and their soldiers go back to the inventory.",
            13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        ScrollRect list = UIKit.ScrollList(panel, "Captains", UIKit.Gap);
        UIKit.Size(list, height: 260f, flexibleWidth: 1f);
        foreach (Captain captain in new[] { newcomer }.Concat(run.Profile.Roster))
        {
            Captain each = captain;
            string note = each == newcomer ? "NEW" : run.Party.Contains(each) ? "in the party" : "";
            int level = Mathf.Max(each.level, LevelMath.LevelForXp(each.xp));
            string label = $"{each.displayName}   <color={UIKit.Hex(UIKit.MutedTextColor)}>LV {level}  {each.ClassName}  " +
                           $"{run.SoldiersOf(each).Count} soldiers  {note}</color>";
            UIKit.Size(UIKit.LiveButton(list.content, () => label, () => chosen = each, refreshers, selected: () => chosen == each,
                height: 28f), flexibleWidth: 1f);
        }

        UIKit.Primary(UIKit.LiveButton(panel, () => chosen != null ? $"Release {chosen.displayName}" : "Pick a captain to release",
            () => match.ReleaseForRecruit(chosen), refreshers, () => chosen != null, width: 260f, height: 32f), refreshers);
    }
}
