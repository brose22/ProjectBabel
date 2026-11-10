using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Run setup: the party slots and the captain roster to pick from.
public partial class RunScreens
{
    List<Captain> Available
    {
        get
        {
            return new List<Captain>(match.Run.SetupOffer);
        }
    }

    int PartySize => Mathf.Min(match.Data.partySize, Available.Count);

    // Party slots on the left, filled in pick order, and the roster in a scrolling list on the
    // right. Clicking a captain in the list adds or removes them; clicking a filled slot removes.
    void BuildSetup()
    {
        RectTransform root = NewScreen("Run Setup");
        RectTransform column = UIKit.Group(root, "Column");
        UIKit.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SetupWidth, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 10, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        Title(column, "PROJECT BABEL", UIKit.TextColor);
        UIKit.Label(column, $"Choose {PartySize} captains for this run", 14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform body = UIKit.Group(column, "Body");
        UIKit.Layout<HorizontalLayoutGroup>(body, 0, 10).childControlHeight = true;
        UIKit.Size(body, SetupWidth, SetupBodyHeight);
        picked.Clear();
        BuildPartyColumn(body);
        BuildRosterList(body);
    }

    void BuildPartyColumn(Transform body)
    {
        RectTransform party = UIKit.Box(body, "Party");
        // Zero, not unset, or the slots' flexible width would let the column take the spare room.
        UIKit.Size(party, PartyWidth, SetupBodyHeight, flexibleWidth: 0f);
        UIKit.Layout<VerticalLayoutGroup>(party, UIKit.Pad, 6);
        string accent = UIKit.Hex(UIKit.AccentColor);
        Text header = UIKit.LiveLabel(party, () => $"PARTY   <color={accent}>{picked.Count}/{PartySize}</color>", refreshers, 10,
            color: UIKit.AccentColor);
        header.fontStyle = FontStyle.Bold;

        // Slots only change on a pick, so they rebuild then rather than refreshing every frame.
        RectTransform slots = UIKit.Group(party, "Slots");
        UIKit.Layout<VerticalLayoutGroup>(slots, 0, 6);
        UIKit.Size(slots, flexibleWidth: 1f);
        string shown = null;
        refreshers.Add(() =>
        {
            string key = string.Join("|", picked);
            if (key == shown) return;
            shown = key;
            UIKit.Clear(slots);
            for (int i = 0; i < PartySize; i++) BuildSlot(slots, i < picked.Count ? picked[i] : null, i);
        });

        UIKit.Size(UIKit.Group(party, "Spacer"), flexibleWidth: 1f).flexibleHeight = 1f;
        Button begin = UIKit.LiveButton(party, () => picked.Count == PartySize ? "Begin run" : $"Pick {PartySize - picked.Count} more",
            () => match.Run.BeginRun(picked), refreshers, () => picked.Count == PartySize, height: 34f);
        UIKit.Size(begin, height: 34f, flexibleWidth: 1f);
        UIKit.Primary(begin, refreshers);
        UIKit.Size(UIKit.Button(party, "Main menu", match.Run.ReturnToMenu), height: 24f, flexibleWidth: 1f);
    }

    // An empty slot is an inset placeholder; a filled one is the captain's party card, which
    // removes them when clicked.
    void BuildSlot(Transform slots, Captain captain, int index)
    {
        if (captain == null)
        {
            RectTransform empty = UIKit.Box(slots, $"Slot {index + 1}", UIKit.InsetColor, blocksClicks: false);
            // Not flexible, or the label's flexible height would let the slots take the spacer's room.
            UIKit.Size(empty, height: SlotHeight, flexibleWidth: 1f).flexibleHeight = 0f;
            UIKit.Layout<VerticalLayoutGroup>(empty, 6, 0);
            Text label = UIKit.Label(empty, $"Slot {index + 1}\n<size=10>Pick a captain from the list</size>", 13,
                TextAnchor.MiddleCenter, UIKit.DisabledTextColor);
            UIKit.Size(label, flexibleWidth: 1f).flexibleHeight = 1f;
            return;
        }
        RectTransform card = UIKit.Box(slots, captain.displayName, PickedCardColor, blocksClicks: false);
        UIKit.Size(card, height: SlotHeight, flexibleWidth: 1f);
        UIKit.Clickable(card, () => picked.Remove(captain));
        PartyCardBody(card, captain, "REMOVE", UIKit.MutedTextColor, out Text name, out _);
        name.color = UIKit.AccentColor;
    }

    // The roster as party cards, two to a row.
    void BuildRosterList(Transform body)
    {
        RectTransform panel = UIKit.Box(body, "Roster");
        UIKit.Size(panel, height: SetupBodyHeight, flexibleWidth: 1f);
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 6);
        Text header = UIKit.Label(panel, $"CAPTAINS   <color={UIKit.Hex(UIKit.MutedTextColor)}>{Available.Count} available</color>", 10,
            color: UIKit.AccentColor);
        header.fontStyle = FontStyle.Bold;

        ScrollRect list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        List<Captain> available = Available;
        for (int i = 0; i < available.Count; i += RosterColumns)
        {
            RectTransform row = UIKit.Group(list.content, "Row");
            UIKit.Layout<HorizontalLayoutGroup>(row, 0, UIKit.Gap);
            UIKit.Size(row, flexibleWidth: 1f);
            for (int j = i; j < i + RosterColumns; j++)
            {
                // A blank cell keeps a lone last card at the same width as the rest.
                if (j < available.Count) BuildRosterCard(row, available[j]);
                else UIKit.Size(UIKit.Group(row, "Blank"), 0f, flexibleWidth: 1f);
            }
        }
    }

    // A party card that adds or removes the captain. Picked cards get a gold ring, name and tag;
    // cards of captains who can't join (the party is full) fade out.
    void BuildRosterCard(Transform row, Captain captain)
    {
        RectTransform card = UIKit.Box(row, captain.displayName, CardColor, blocksClicks: false);
        // No preferred width, so the row splits evenly however long each name is.
        UIKit.Size(card, 0f, SlotHeight, 1f);
        UIKit.Clickable(card, () => TogglePick(captain));
        var image = card.GetComponent<Image>();
        var fade = card.gameObject.AddComponent<CanvasGroup>();
        PartyCardBody(card, captain, "IN PARTY", UIKit.AccentColor, out Text name, out Text tag);

        Image ring = UIKit.Outline(card, UIKit.AccentColor);
        ring.rectTransform.offsetMin = new Vector2(2f, 2f);
        ring.rectTransform.offsetMax = new Vector2(-2f, -2f);
        refreshers.Add(() =>
        {
            bool isPicked = picked.Contains(captain);
            image.color = isPicked ? PickedCardColor : CardColor;
            ring.enabled = isPicked;
            tag.enabled = isPicked;
            name.color = isPicked ? UIKit.AccentColor : UIKit.TextColor;
            fade.alpha = isPicked || picked.Count < PartySize ? 1f : 0.45f;
        });
    }

    static Text Title(Transform parent, string text, Color color)
    {
        Text title = UIKit.Shadowed(UIKit.Label(parent, text, 30, TextAnchor.MiddleCenter, color));
        title.fontStyle = FontStyle.Bold;
        return title;
    }

    // Name (with a tag on the right), element and role with bond, special, likes and dislikes.
    static void CaptainDetails(Transform card, Captain captain, string tagText, out Text name, out Text tag)
    {
        Transform title = UIKit.Group(card, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 4);
        name = UIKit.Label(title, captain.displayName, 15);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        tag = UIKit.Label(title, tagText, 9, TextAnchor.MiddleRight, UIKit.AccentColor);
        tag.fontStyle = FontStyle.Bold;

        int rank = ApprovalStore.RankOf(captain);
        string muted = UIKit.Hex(UIKit.MutedTextColor);
        UIKit.Label(card, $"<color={UIKit.Hex(UIKit.ElementColor(captain.element))}>{captain.element}</color> {captain.ClassName}" +
                          $"   <color={muted}>Bond</color> {BondMath.RankName(rank)} <color={muted}>({ApprovalStore.ApprovalOf(captain):0})</color>", 12);
        UIKit.Label(card, captain.Ability != null ? $"<color={muted}>Special</color>  {captain.Ability.displayName}" : "No special", 12);
        UIKit.Label(card, $"<color={UIKit.Hex(UIKit.GoodColor)}>Likes</color>  {string.Join(", ", captain.likes)}", 11,
            color: UIKit.MutedTextColor);
        UIKit.Label(card, $"<color={UIKit.Hex(UIKit.BadColor)}>Dislikes</color>  {string.Join(", ", captain.dislikes)}", 11,
            color: UIKit.MutedTextColor);
    }

    // Picked cards keep their dark face, so the text stays readable, and get a gold ring and name.
    void BuildCaptainCard(Transform parent, Captain captain, Action onClick, Func<bool> selected)
    {
        Button card = UIKit.Button(parent, "", onClick);
        UIKit.Clear(card.transform);
        var image = card.GetComponent<Image>();
        UIKit.Layout<VerticalLayoutGroup>(card, 8, 1).padding.top = 6;
        CaptainDetails(card.transform, captain, "PICKED", out Text name, out Text tag);

        Image ring = UIKit.Outline(card.transform, UIKit.AccentColor);
        ring.rectTransform.offsetMin = new Vector2(2f, 2f);
        ring.rectTransform.offsetMax = new Vector2(-2f, -2f);
        refreshers.Add(() =>
        {
            bool isPicked = selected();
            image.color = isPicked ? PickedCardColor : CardColor;
            ring.enabled = isPicked;
            tag.enabled = isPicked;
            name.color = isPicked ? UIKit.AccentColor : UIKit.TextColor;
        });
    }

    void TogglePick(Captain captain)
    {
        if (picked.Remove(captain)) return;
        if (picked.Count < PartySize) picked.Add(captain);
    }
}
