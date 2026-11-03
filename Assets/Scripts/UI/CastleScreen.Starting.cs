using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The starting captains (item: starting captains), picked the first time the game is played: one
// rolled captain of each starting role, of whom the player takes StartingCaptains.Picks. Each comes
// with its soldiers; the one left behind is discarded. Later sessions go straight to the home screen.
public partial class CastleScreen
{
    List<Captain> startingOffer;
    readonly List<int> startingPicks = new();

    // A profile with no captains that has never been saved: the first play, or a new game.
    bool NeedsStartingCaptains => profile.Roster.Count == 0 && !ProfileSave.Exists;

    // The offer as cards in one row; clicking one picks or unpicks them. There's nothing to go back
    // to, so cancel does nothing here.
    void BuildStartingCaptains()
    {
        startingOffer ??= MatchManager.RollStartingCaptains(data, Random.Range(int.MinValue, int.MaxValue));
        RectTransform column = NewScreen("Starting Captains", InventoryWidth, null);
        Title(column, "CHOOSE YOUR CAPTAINS");
        int picks = StartingCaptains.PickCount(startingOffer.Count);
        UIKit.LiveLabel(column, () => $"Pick {picks} to start with. Each brings {StartingCaptains.SoldiersEach} random soldiers; " +
                                      $"the one left behind is gone.   <color={UIKit.Hex(UIKit.AccentColor)}>{startingPicks.Count}/{picks} picked</color>",
            refreshers, 14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform row = UIKit.Group(column, "Offer");
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, UIKit.Gap);
        UIKit.Size(row, InventoryWidth);
        for (int i = 0; i < startingOffer.Count; i++) StartingCard(row, i);

        UIKit.Primary(UIKit.LiveButton(column, () => StartingCaptains.IsComplete(startingPicks, startingOffer.Count)
                ? "Enter the Castle" : $"Pick {picks - startingPicks.Count} more", TakeStartingCaptains, refreshers,
            () => StartingCaptains.IsComplete(startingPicks, startingOffer.Count), width: 240f, height: 34f), refreshers);
    }

    // Picked cards get a gold ring, name and tag; the rest fade once the pick is full. Hovering or
    // focusing one shows its ability.
    void StartingCard(Transform row, int index)
    {
        Captain captain = startingOffer[index];
        RectTransform card = CaptainCard(row, captain, "PICKED", () => StartingCaptains.Toggle(startingPicks, index, startingOffer.Count),
            false, out Text name, out Text tag);
        UIKit.Size(card, 0f, CardHeight, 1f);
        if (captain.Ability != null) UIKit.HoverTip(card, captain.AbilityName, captain.AbilityDescription);
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
        MatchManager.TakeStartingCaptains(profile, picked, Random.Range(int.MinValue, int.MaxValue));
        ProfileSave.Save(profile);
        startingOffer = null;
        startingPicks.Clear();
        BuildHome();
    }
}
