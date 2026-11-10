using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Full-screen run screens around a match: run setup (title and captain pick) before a run's first
// match, the world map between matches, and results after every match. Built in uGUI at runtime
// and added to the match by MatchManager, like Relationships.
public partial class RunScreens : MonoBehaviour
{
    // Nearly opaque: the project renders in linear colour, where lighter dims let the bright map
    // compete with the cards.
    static readonly Color DimColor = new(0.02f, 0.02f, 0.04f, 0.94f);
    static readonly Color CardColor = new(0.17f, 0.2f, 0.28f, 1f);
    static readonly Color PickedCardColor = new(0.27f, 0.24f, 0.2f, 1f);
    // Recruit cards.
    const float CardHeight = 112f;
    // Run setup: party slots on the left, the roster list on the right.
    const float SetupWidth = 880f;
    const float SetupBodyHeight = 420f;
    const float PartyWidth = 300f;
    // Party slots and roster cards: a party card's height, portrait and all.
    const float SlotHeight = 84f;
    const int RosterColumns = 2;
    static readonly Vector2 MapArea = new(790f, 380f);
    // The world map's buttons, in a column down the left edge; the map sits to their right.
    const float MapMenuWidth = 120f;
    const float MapMenuGap = 12f;
    static readonly Vector2 CitySize = new(100f, 40f);
    static readonly Vector2 BaseSize = new(124f, 52f);
    const float BadgeSize = 24f;
    // Keeps jittered cities inside the map area.
    const float MapMargin = 14f;
    const float CityJitterAcross = 12f;
    const float CityJitterAlong = 4f;
    // The vertical map: rows a layer apart, and the width a row's cities spread across.
    const float MapRowSpacing = 76f;
    const float MapLaneWidth = 680f;
    // World map party drawer.
    const float DrawerWidth = 300f;
    const float DrawerSlideSeconds = 0.2f;
    // Light, so the map stays readable behind an open drawer.
    const float DrawerDim = 0.18f;
    // Art drawn at whole multiples so its pixels stay square: 32 px portraits at 2x, 16 px icons at 1x.
    const float PortraitSize = 64f;
    const float IconSize = 16f;
    // Captain details window.
    const float DetailsWidth = 560f;
    const float DetailsPortraitSize = 96f;
    // Inventory and gift windows.
    // Shop shelves: three cards across the 520 px stop panel.
    const int ShopColumns = 3;
    static readonly Vector2 ShopCardSize = new(152f, 116f);
    // The captain window sits on the right edge and the gift window takes the room to its left.
    const float GiftWindowWidth = UIKit.ReferenceWidth - DetailsWidth - 3f * UIKit.Pad;
    const float GiftListHeight = 220f;
    // Darker than the accent gold, so the meter's white text stays readable on it.
    static readonly Color BondBarColor = new(0.62f, 0.45f, 0.16f, 1f);
    static readonly Color RoadColor = new(0.55f, 0.45f, 0.3f, 1f);
    static readonly Color ClearedCityColor = new(0.16f, 0.3f, 0.2f, 1f);
    static readonly Color BaseColor = new(0.55f, 0.12f, 0.16f, 1f);
    static readonly Color EliteColor = new(0.45f, 0.2f, 0.38f, 1f);
    static readonly Color StopColor = new(0.18f, 0.3f, 0.42f, 1f);
    // Roads: ahead of the player, the trail already walked, the rest, and the dashed hint of hidden ones.
    static readonly Color AheadRoadColor = new(0.96f, 0.76f, 0.36f, 1f);
    static readonly Color TrailRoadColor = new(0.4f, 0.7f, 0.45f, 1f);
    static readonly Color DimRoadColor = new(0.6f, 0.5f, 0.34f, 0.75f);
    static readonly Color FogRoadColor = new(0.66f, 0.68f, 0.73f, 0.45f);
    static readonly Color RestColor = new(0.22f, 0.5f, 0.3f, 1f);
    static readonly Color EventColor = new(0.3f, 0.34f, 0.6f, 1f);

    MatchManager match;
    Relationships relationships;
    Canvas canvas;
    RectTransform screen;
    UIFocusScope screenFocus;
    readonly List<Action> refreshers = new();
    readonly List<Captain> picked = new();
    MatchState shownFor = (MatchState)(-1);
    bool shownRecruiting;
    bool shownReleasing;

    void Awake()
    {
        match = GetComponent<MatchManager>();
        relationships = GetComponent<Relationships>();
    }

    void Start() => canvas = UIKit.CreateCanvas("Run Screens", 20);

    void LateUpdate()
    {
        if (canvas == null) return;
        MatchState wanted = match.IsBetweenMatches || match.IsOver ? match.State : MatchState.Preparing;
        // The map screen shows the soldier reward first while soldiers are on offer.
        bool recruiting = wanted == MatchState.Map && RunState.Current.SoldierOffer.Count > 0;
        // An event's recruit for a full roster asks who to release before the stop is left.
        bool releasing = wanted == MatchState.Stop && match.PendingRecruit != null;
        if (wanted != shownFor || recruiting != shownRecruiting || releasing != shownReleasing)
        {
            shownFor = wanted;
            shownRecruiting = recruiting;
            shownReleasing = releasing;
            refreshers.Clear();
            if (screen != null) Destroy(screen.gameObject);
            screen = null;
            if (wanted == MatchState.Setup) BuildSetup();
            else if (recruiting) BuildSoldierReward();
            else if (wanted == MatchState.Map) BuildMap();
            else if (releasing) BuildReleaseChoice();
            else if (wanted == MatchState.Stop) BuildStop();
            else if (match.IsOver) BuildResults();
        }
        UIKit.Refresh(refreshers);
        if (shownFor == MatchState.Map && !shownRecruiting) PadDrawerShortcuts();
    }

    // On the world map the shoulder buttons toggle the drawers: right for the party, left for the
    // inventory. Not while a captain, gift or shop window is up, which have their own focus.
    void PadDrawerShortcuts()
    {
        // The map screen always holds focus, so this reads the pad itself rather than the match's CaptainStep.
        int step = GameInput.MenuShoulderStep;
        if (step == 0 || shopOpen || screen == null) return;
        if (screen.Find("Captain Details") != null || screen.Find("Gift") != null) return;
        string id = step > 0 ? "Party" : "Inventory";
        ToggleDrawer(id);
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    RectTransform NewScreen(string name)
    {
        screen = UIKit.Stretch(UIKit.Panel(canvas.transform, name, DimColor));
        screenFocus = UIKit.Focus(screen);
        return screen;
    }
}
