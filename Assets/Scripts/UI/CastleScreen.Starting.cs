using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The starting captains (item: starting captains), picked the first time the game is played: one
// rolled captain of each starting role, of whom the player takes StartingCaptains.Picks. Each comes
// with its soldiers; the one left behind is discarded. Later sessions go straight to the home screen.
public partial class CastleScreen
{
    const float StartingWidth = 580f;
    const float StartingCardHeight = 84f;
    List<Captain> startingOffer;
    // The soldiers each offered captain brings, rolled with the offer so the cards can show them.
    readonly Dictionary<string, List<SoldierItem>> startingSoldiers = new();
    readonly List<int> startingPicks = new();

    // A profile with no captains that has never been saved: the first play, or a new game.
    bool NeedsStartingCaptains => profile.Roster.Count == 0 && !ProfileSave.Exists;

    // The offer as cards in one row; clicking one picks or unpicks them. There's nothing to go back
    // to, so cancel does nothing here.
    void BuildStartingCaptains()
    {
        if (startingOffer == null)
        {
            startingOffer = MatchManager.RollStartingCaptains(data, Random.Range(int.MinValue, int.MaxValue));
            startingSoldiers.Clear();
            foreach (Captain offered in startingOffer)
            {
                var soldiers = new List<SoldierItem>();
                for (int i = 0; i < StartingCaptains.SoldiersEach; i++) soldiers.Add(SoldierRules.Roll(Random.Range(int.MinValue, int.MaxValue)));
                startingSoldiers[offered.id] = soldiers;
            }
        }
        RectTransform column = NewScreen("Starting Captains", StartingWidth, null);
        Title(column, "CHOOSE YOUR CAPTAINS");
        int picks = StartingCaptains.PickCount(startingOffer.Count);
        UIKit.LiveLabel(column, () => $"Pick {picks} to start with; the one left behind is gone.   <color={UIKit.Hex(UIKit.AccentColor)}>{startingPicks.Count}/{picks} picked</color>",
            refreshers, 14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform row = UIKit.Group(column, "Offer");
        UIKit.Layout<VerticalLayoutGroup>(row, 0, UIKit.Gap);
        UIKit.Size(row, StartingWidth);
        for (int i = 0; i < startingOffer.Count; i++) StartingCard(row, i);

        UIKit.Primary(UIKit.LiveButton(column, () => "Confirm", TakeStartingCaptains, refreshers,
            () => StartingCaptains.IsComplete(startingPicks, startingOffer.Count), width: 240f, height: 34f), refreshers);
    }

    // Picked cards get a gold ring, name and tag; the rest fade once the pick is full. Hovering or
    // focusing one shows its ability.
    void StartingCard(Transform row, int index)
    {
        Captain captain = startingOffer[index];
        RectTransform card = CaptainCard(row, captain, "PICKED", () => StartingCaptains.Toggle(startingPicks, index, startingOffer.Count),
            false, out Text name, out Text tag);
        UIKit.Size(card, 0f, StartingCardHeight, 1f);
        if (captain.Ability != null) UIKit.HoverTip(card, captain.AbilityName, captain.AbilityDescription);
        // Three columns: portrait and identity, base stats stacked, then the ability and soldiers. The
        // soldiers line the shared card ends with moves to the third column.
        Transform info = card.Find("Info");
        Destroy(info.GetChild(info.childCount - 1).gameObject);
        UIKit.Size(info, 120f, -1f, 0f);
        string muted = UIKit.Hex(UIKit.MutedTextColor);

        RectTransform stats = UIKit.Group(card, "Stats");
        UIKit.Layout<VerticalLayoutGroup>(stats, 0, 1);
        UIKit.Size(stats, 70f, -1f, 0f);
        UIKit.Label(stats, $"<color={muted}>Pow</color> {captain.power:0.#}", 11);
        UIKit.Label(stats, $"<color={muted}>Rng</color> {captain.range:0.#}", 11);
        UIKit.Label(stats, $"<color={muted}>Spd</color> {captain.speed:0.#}", 11);
        UIKit.Label(stats, $"<color={muted}>HP</color> {captain.health}", 11);
        UIKit.Label(stats, $"<color={muted}>Def</color> {captain.defense:0.#}", 11);

        RectTransform detail = UIKit.Group(card, "Detail");
        UIKit.Layout<VerticalLayoutGroup>(detail, 0, 2);
        UIKit.Size(detail, flexibleWidth: 1f);
        if (captain.Ability != null)
        {
            Text abilityName = UIKit.Label(detail, captain.AbilityName, 12, color: UIKit.AccentColor);
            abilityName.fontStyle = FontStyle.Bold;
            UIKit.Label(detail, captain.AbilityDescription, 11, color: UIKit.MutedTextColor);
        }
        UIKit.Label(detail, SoldiersLine(captain), 11);
        var image = card.GetComponent<Image>();
        var fade = card.gameObject.AddComponent<CanvasGroup>();
        Image ring = UIKit.Outline(card, UIKit.AccentColor);
        ring.rectTransform.offsetMin = new Vector2(2f, 2f);
        ring.rectTransform.offsetMax = new Vector2(-2f, -2f);
        refreshers.Add(() =>
        {
            bool picked = startingPicks.Contains(index);
            image.color = picked ? PickedCardColor : CardColor;
            ring.enabled = picked;
            tag.enabled = picked;
            name.color = picked ? UIKit.AccentColor : UIKit.TextColor;
            fade.alpha = picked || startingPicks.Count < StartingCaptains.PickCount(startingOffer.Count) ? 1f : 0.45f;
        });
    }

    // The picked captains join the roster with their soldiers, and the profile is saved for the
    // first time, so this screen isn't offered again.
    void TakeStartingCaptains()
    {
        if (!StartingCaptains.IsComplete(startingPicks, startingOffer.Count)) return;
        var picked = new List<Captain>();
        foreach (int index in startingPicks) picked.Add(startingOffer[index]);
        // The soldiers the cards showed are the ones the captains bring.
        foreach (Captain captain in picked)
        {
            if (!profile.TryAdd(captain)) continue;
            List<SoldierItem> soldiers = startingSoldiers[captain.id];
            for (int slot = 0; slot < soldiers.Count; slot++)
            {
                profile.Soldiers.Add(soldiers[slot]);
                profile.Soldiers.Equip(soldiers[slot].id, captain.id, slot);
            }
        }
        ProfileSave.Save(profile);
        startingOffer = null;
        startingSoldiers.Clear();
        startingPicks.Clear();
        BuildHome();
    }
}
