using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Full-screen run screens around a match: run setup (title and captain pick) before a run's first
// match, the world map between matches, and results after every match. Built in uGUI at runtime
// and added to the match by MatchManager, like Relationships.
public class RunScreens : MonoBehaviour
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
    static readonly Vector2 MapArea = new(900f, 330f);
    static readonly Vector2 CitySize = new(100f, 40f);
    static readonly Vector2 BaseSize = new(124f, 52f);
    const float BadgeSize = 24f;
    // Keeps jittered cities inside the map area.
    const float MapMargin = 14f;
    const float CityJitter = 12f;
    const float CityJitterX = 4f;
    // World map party drawer.
    const float DrawerWidth = 300f;
    const float DrawerSlideSeconds = 0.2f;
    // Light, so the map stays readable behind an open drawer.
    const float DrawerDim = 0.18f;
    static readonly Vector2 DrawerTabSize = new(30f, 110f);
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
    // Opaque, so the map never shows through the window.
    static readonly Color WindowColor = new(UIKit.PanelColor.r, UIKit.PanelColor.g, UIKit.PanelColor.b, 1f);
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
        // The map screen shows recruiting first while captains are on offer.
        bool recruiting = wanted == MatchState.Map && RunState.Current.RecruitOffer.Count > 0;
        if (wanted != shownFor || recruiting != shownRecruiting)
        {
            shownFor = wanted;
            shownRecruiting = recruiting;
            refreshers.Clear();
            if (screen != null) Destroy(screen.gameObject);
            screen = null;
            if (wanted == MatchState.Setup) BuildSetup();
            else if (recruiting) BuildRecruit();
            else if (wanted == MatchState.Map) BuildMap();
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
        int step = GameInput.PartyStep;
        if (step == 0 || shopOpen || screen == null) return;
        if (screen.Find("Captain Details") != null || screen.Find("Gift") != null) return;
        string id = step > 0 ? "Party" : "Inventory";
        openDrawer = openDrawer == id ? null : id;
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

    // ---------- Setup ----------

    List<Captain> Available
    {
        get
        {
            return new List<Captain>(match.SetupOffer);
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
            () => match.BeginRun(picked), refreshers, () => picked.Count == PartySize, height: 34f);
        UIKit.Size(begin, height: 34f, flexibleWidth: 1f);
        UIKit.Primary(begin, refreshers);
        UIKit.Size(UIKit.Button(party, "Main menu", match.ReturnToMenu), height: 24f, flexibleWidth: 1f);
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

    // ---------- Recruiting ----------

    void BuildRecruit()
    {
        RunState run = RunState.Current;
        Captain chosen = null;

        RectTransform root = NewScreen("Recruit");
        RectTransform column = UIKit.Group(root, "Column");
        UIKit.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 10, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        UIKit.Label(column, "A CAPTAIN OFFERS TO JOIN", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, $"Recruit one into your roster and party, or go on without.  Party {run.ActivePartyCount}/{match.Data.maxPartySize}",
            14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform row = UIKit.Group(column, "Captains");
        var rowLayout = row.gameObject.AddComponent<GridLayoutGroup>();
        rowLayout.cellSize = new Vector2(205f, CardHeight);
        rowLayout.spacing = new Vector2(8f, 8f);
        rowLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        rowLayout.constraintCount = 1;
        rowLayout.childAlignment = TextAnchor.UpperCenter;
        int count = run.RecruitOffer.Count;
        UIKit.Size(row, count * 205f + (count - 1) * 8f, CardHeight);
        foreach (Captain captain in run.RecruitOffer)
            BuildCaptainCard(row, captain, () => chosen = captain, () => chosen == captain);

        RectTransform buttons = UIKit.Group(column, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 10, fitContent: true);
        UIKit.Primary(UIKit.LiveButton(buttons, () => chosen != null ? $"Recruit {chosen.displayName}" : "Pick a captain",
            () => match.Recruit(chosen), refreshers, () => chosen != null, width: 200f, height: 32f), refreshers);
        UIKit.Button(buttons, "No thanks", () => match.Recruit(null), 140f, 32f);
    }

    // ---------- World map ----------

    void BuildMap()
    {
        RunState run = RunState.Current;
        WorldMap map = run.Map;

        RectTransform root = NewScreen("World Map");
        RectTransform column = UIKit.Group(root, "Column");
        UIKit.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapArea.x, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 8, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        bool abandonArmed = this.abandonArmed;
        this.abandonArmed = false;

        int nextLayer = map.current < 0 ? 0 : map.nodes[map.current].layer + 1;
        string progress = nextLayer >= map.layerCount - 1 ? "The enemy base lies ahead" : $"Stop {nextLayer + 1} of {map.layerCount}";
        UIKit.Label(column, "WORLD MAP", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, $"{progress}. Choose one of the glowing stops; each stop you reach reveals the roads beyond it.",
            12, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        BuildRunStats(root, run);

        RectTransform area = UIKit.Box(column, "Map", blocksClicks: false);
        UIKit.Size(area, MapArea.x, MapArea.y);

        var choices = map.Choices();
        // Hints of hidden roads first, then roads, so cities and their marks sit on top.
        foreach (MapNode node in map.nodes)
            if (node.revealed)
                foreach (int next in node.next)
                    if (!map.nodes[next].revealed)
                        BuildFogStub(area, CityPosition(map, node), CityPosition(map, map.nodes[next]));
        foreach (MapNode node in map.nodes)
            if (node.revealed)
                foreach (int next in node.next)
                {
                    MapNode to = map.nodes[next];
                    if (!to.revealed) continue;
                    // Ahead: the roads out of where the player stands. Trail: the way already walked.
                    bool ahead = node.id == map.current;
                    bool trail = node.cleared && to.cleared;
                    BuildRoad(area, CityPosition(map, node), CityPosition(map, to), node.id * 7 + next * 13,
                        ahead ? AheadRoadColor : trail ? TrailRoadColor : DimRoadColor, ahead ? 5f : trail ? 4f : 3f);
                }
        foreach (MapNode node in map.nodes)
            if (node.revealed) BuildCity(area, map, node, choices.Exists(c => c.id == node.id));
        // The gamepad starts on a stop it can pick, not on a drawer tab.
        screenFocus.Default = () => Array.Find(area.GetComponentsInChildren<Button>(), city => city.interactable);

        BuildLegend(column);

        RectTransform buttons = UIKit.Group(column, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 10, fitContent: true);
        UIKit.Button(buttons, "Shop", () => SetShopOpen(true), 160f, 24f);
        UIKit.Button(buttons, "Main menu", match.ReturnToMenu, 160f, 24f);
        UIKit.Size(UIKit.Group(buttons, "Spacer"), 60f, 24f);
        // Abandoning loses the run, so it takes a second press.
        Button abandon = UIKit.Button(buttons, abandonArmed ? "Press again to abandon" : "Abandon run", () =>
        {
            if (abandonArmed) { match.RestartRun(); return; }
            this.abandonArmed = true;
            shownFor = (MatchState)(-1);
        }, 160f, 24f);
        if (abandonArmed) abandon.GetComponent<Image>().color = UIKit.BadColor;

        // Two drawers share the left edge, the inventory's tab under the party's. Only one is open
        // at a time; each is built over the last, so the later one's blocker covers the earlier.
        openDrawer = null;
        BuildDrawer(root, "Party", "PARTY", 0f,
            $"PARTY   <color={UIKit.Hex(UIKit.MutedTextColor)}>{run.ActivePartyCount}/{match.Data.maxPartySize}</color>",
            list =>
            {
                foreach (Captain captain in run.Party)
                    PartyCard(list.content, run, captain);
            });
        BuildDrawer(root, "Inventory", "INVENTORY", -(DrawerTabSize.y + UIKit.Gap), "INVENTORY",
            list =>
            {
                // Rebuilt when a gift is given from a captain's window.
                string shown = null;
                refreshers.Add(() =>
                {
                    string now = InventorySignature(run);
                    if (now == shown) return;
                    shown = now;
                    BuildGiftRows(list.content, run, null, null);
                });
            });
        if (shopOpen) BuildMapShop(root);
    }

    // Whether "Abandon run" has been clicked once and now waits for a second click.
    bool abandonArmed;

    // The id of the map drawer that is open, or null when none is.
    string openDrawer;

    // Whether the shop window is showing over the world map.
    bool shopOpen;

    // Opens or closes the shop over the map by rebuilding the map screen.
    void SetShopOpen(bool open)
    {
        shopOpen = open;
        shownFor = (MatchState)(-1);
    }

    void BuildMapShop(RectTransform root)
    {
        RectTransform overlay = UIKit.Stretch(UIKit.Panel(root, "Shop", DimColor));
        UIKit.Focus(overlay, () => SetShopOpen(false));
        RectTransform panel = UIKit.Box(overlay, "Panel");
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 0f));
        UIKit.Layout<VerticalLayoutGroup>(panel, 16, 8, fitContent: true).childAlignment = TextAnchor.UpperCenter;
        BuildShop(panel, () => SetShopOpen(false));
    }

    string InventorySignature(RunState run)
    {
        var parts = new List<string>();
        foreach (GiftData gift in match.Data.shopGifts)
            if (gift != null) parts.Add(run.GiftCount(gift).ToString());
        return string.Join(",", parts);
    }

    // A drawer that slides in from the left. Its tab stays on the screen edge while it's closed,
    // tabOffsetY below the middle; the tab, the Close button or a click beside the drawer toggles
    // it. fill adds the contents to the scrolling list.
    void BuildDrawer(RectTransform root, string id, string tabText, float tabOffsetY, string headerText, Action<ScrollRect> fill)
    {
        float shown = 0f;
        Action<bool> setOpen = open => openDrawer = open ? id : null;

        RectTransform blocker = UIKit.Stretch(UIKit.Panel(root, id + " Drawer Blocker", new Color(0f, 0f, 0f, DrawerDim)));
        var dismiss = blocker.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(() => setOpen(false));
        var blockerFade = blocker.gameObject.AddComponent<CanvasGroup>();

        RectTransform drawer = UIKit.Box(root, id + " Drawer");
        drawer.anchorMin = Vector2.zero;
        drawer.anchorMax = new Vector2(0f, 1f);
        drawer.pivot = new Vector2(0f, 0.5f);
        drawer.sizeDelta = new Vector2(DrawerWidth, -2f * UIKit.Pad);
        UIKit.Layout<VerticalLayoutGroup>(drawer, UIKit.Pad, 6);

        Transform title = UIKit.Group(drawer, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 6);
        Text header = UIKit.Label(title, headerText, 10, color: UIKit.AccentColor);
        header.fontStyle = FontStyle.Bold;
        UIKit.Size(header, flexibleWidth: 1f);
        UIKit.PadBadge(UIKit.Button(title, "Close", () => setOpen(false), 60f, 22f), PadGlyph.East);

        ScrollRect list = UIKit.ScrollList(drawer, "Contents", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        fill(list);
        // Focus waits for the slide, and starts on the list rather than on Close.
        UIKit.Focus(blocker, () => setOpen(false), drawer, DrawerSlideSeconds).Default =
            () => list.content.GetComponentInChildren<Button>();

        // The tab hangs off the drawer's right edge, so it rides along as the drawer slides.
        Button tab = UIKit.Button(drawer, "", () => setOpen(openDrawer != id), DrawerTabSize.x, DrawerTabSize.y);
        tab.GetComponent<LayoutElement>().ignoreLayout = true;
        UIKit.Place((RectTransform)tab.transform, new Vector2(1f, 0.5f), new Vector2(0f, tabOffsetY), DrawerTabSize);
        ((RectTransform)tab.transform).pivot = new Vector2(0f, 0.5f);
        Text tabLabel = tab.GetComponentInChildren<Text>();
        tabLabel.fontStyle = FontStyle.Bold;
        UIKit.Place(tabLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIKit.Gap / 2f), new Vector2(DrawerTabSize.y, DrawerTabSize.x));
        tabLabel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // The shoulder button that toggles this drawer, shown while a gamepad is in use.
        UIKit.PadBadge(tab, id == "Party" ? PadGlyph.RightShoulder : PadGlyph.LeftShoulder, () => GameInput.UsingGamepad,
            new Vector2(0.5f, 1f), Vector2.down * 10f);

        refreshers.Add(() =>
        {
            bool open = openDrawer == id;
            shown = Mathf.MoveTowards(shown, open ? 1f : 0f, Time.unscaledDeltaTime / DrawerSlideSeconds);
            float eased = shown * shown * (3f - 2f * shown);
            // Whole reference pixels, so the frame's texels stay crisp mid-slide.
            drawer.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(-DrawerWidth, 0f, eased)), 0f);
            blocker.gameObject.SetActive(shown > 0f);
            blockerFade.alpha = eased;
            tabLabel.text = open ? "CLOSE" : tabText;
            // While the other drawer is open its tab would sit on top of this one, so this one waits.
            drawer.gameObject.SetActive(shown > 0f || openDrawer == null);
        });
    }

    // A captain in the party drawer: the usual details, tagged with their level, or faded with a
    // note when they've walked out.
    void PartyCard(Transform list, RunState run, Captain captain)
    {
        bool departed = run.HasDeparted(captain);
        RectTransform card = UIKit.Box(list, captain.displayName, departed ? UIKit.InsetColor : CardColor, blocksClicks: false);
        UIKit.Size(card, flexibleWidth: 1f);
        UIKit.Clickable(card, () => ShowCaptainDetails(run, captain));
        int level = Mathf.Max(captain.level, LevelMath.LevelForXp(run.XpOf(captain)));
        PartyCardBody(card, captain, departed ? "WALKED OUT" : $"LV {level}", departed ? UIKit.BadColor : UIKit.AccentColor, out _, out _, live: true);
        if (departed) card.gameObject.AddComponent<CanvasGroup>().alpha = 0.6f;
    }

    // The party card's face, shared with run setup: portrait on the left; name and tag, element
    // and role icons, bond, and special on the right.
    // With live, the bond follows approval changes; only for cards built with the screen, since
    // setup rebuilds its cards from inside a refresher, which can't add to the list it is running.
    void PartyCardBody(RectTransform card, Captain captain, string tagText, Color tagColor, out Text name, out Text tag, bool live = false)
    {
        HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(card, 6, 8);
        layout.childAlignment = TextAnchor.UpperLeft;

        // The 32 px portraits at 2x, inset like a picture frame.
        RectTransform frame = UIKit.Box(card, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.Portrait, PortraitSize);

        RectTransform info = UIKit.Group(card, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 2);
        UIKit.Size(info, flexibleWidth: 1f);

        Transform title = UIKit.Group(info, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 4);
        name = UIKit.Label(title, captain.displayName, 15);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        tag = UIKit.Label(title, tagText, 9, TextAnchor.MiddleRight, tagColor);
        tag.fontStyle = FontStyle.Bold;

        ElementData element = match.Data.ElementInfo(captain.element);
        Transform tags = UIKit.Group(info, "Tags");
        UIKit.Layout<HorizontalLayoutGroup>(tags, 0, 4);
        UIKit.Icon(tags, element != null ? element.icon : null, IconSize);
        UIKit.Label(tags, element != null ? element.displayName : captain.element.ToString(), 12, color: UIKit.ElementColor(captain.element));
        UIKit.Size(UIKit.Group(tags, "Gap"), 6f);
        UIKit.Icon(tags, match.Data.RoleIcon(captain.role), IconSize);
        UIKit.Label(tags, captain.ClassName, 12);

        string muted = UIKit.Hex(UIKit.MutedTextColor);
        string Bond() => $"<color={muted}>Bond</color> {BondMath.RankName(ApprovalStore.RankOf(captain))} " +
                         $"<color={muted}>({ApprovalStore.ApprovalOf(captain):0})</color>";
        // Live, so a gift given from the details window shows on the card behind it.
        if (live) UIKit.LiveLabel(info, Bond, refreshers, 12);
        else UIKit.Label(info, Bond(), 12);
        // How far along the bond is, so a rank-up (and the soldier slot it opens) can be seen coming.
        if (live)
            UIKit.LiveBar(info, () => (ApprovalStore.ApprovalOf(captain) - BondMath.MinApproval) / (BondMath.MaxApproval - BondMath.MinApproval),
                () => "", BondBarColor, refreshers, 6f);
        Transform special = UIKit.Group(info, "Special");
        UIKit.Layout<HorizontalLayoutGroup>(special, 0, 4);
        UIKit.Icon(special, captain.Ability != null ? captain.Ability.icon : null, IconSize);
        UIKit.Label(special, captain.Ability != null ? captain.Ability.displayName : "No special", 12);
        // The equipped soldiers, live so a change from the details window shows on the card behind it.
        if (live) UIKit.LiveLabel(info, () => SoldiersLine(captain), refreshers, 11);
    }

    // The captain's soldiers in slot order, each in its rarity's colour.
    string SoldiersLine(Captain captain)
    {
        List<SoldierItem> soldiers = RunState.Current.SoldiersOf(captain);
        string muted = UIKit.Hex(UIKit.MutedTextColor);
        if (soldiers.Count == 0) return $"<color={muted}>No soldiers</color>";
        return $"<color={muted}>Soldiers</color> " +
               string.Join(", ", soldiers.ConvertAll(s => CaptainPanelParts.SoldierName(s, match.Data.ClassData(s.soldierClass))));
    }

    // A window over the map with everything about one captain. A click beside it, or Close,
    // dismisses it; rebuilding the screen does too.
    void ShowCaptainDetails(RunState run, Captain captain)
    {
        RectTransform overlay = UIKit.Stretch(UIKit.Panel(screen, "Captain Details", new Color(0f, 0f, 0f, 0.55f)));
        var dismiss = overlay.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(() => Destroy(overlay.gameObject));
        UIKit.Focus(overlay, () => Destroy(overlay.gameObject));

        RectTransform window = UIKit.Box(overlay, "Window", WindowColor);
        // Swallows clicks on the window, which would otherwise bubble up to the overlay and close it.
        window.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        UIKit.Place(window, new Vector2(1f, 0.5f), new Vector2(-UIKit.Pad, 0f), new Vector2(DetailsWidth, 0f));
        UIKit.Layout<VerticalLayoutGroup>(window, 12, 6, fitContent: true);

        int level = Mathf.Max(captain.level, LevelMath.LevelForXp(run.XpOf(captain)));
        var detailRefreshers = new List<Action>();
        DetailsHeader(window, run, captain, level, detailRefreshers);

        UIKit.Size(UIKit.Header(window, "Stats"), flexibleWidth: 1f);
        RectTransform stats = UIKit.Group(window, "Stats");
        UIKit.Layout<HorizontalLayoutGroup>(stats, 0, 8).childAlignment = TextAnchor.UpperCenter;
        UIKit.Size(stats, flexibleWidth: 1f);
        // Power, range, speed and Defense include what the soldiers add. Small lines under the
        // values: power and defense against the level-1 base, the rest as a party ranking. Range
        // follows the soldiers, so it has none.
        float power = captain.power + match.StatBonus(captain, CaptainStat.Power);
        Stat(stats, "POWER", $"{power * LevelMath.StatFactor(level):0}", level > 1 ? $"base {captain.power:0}" : PartyRank(run, captain, c => c.power));
        Stat(stats, "RANGE", $"{match.RangeFor(captain):0.#}");
        Stat(stats, "SPEED", $"{captain.speed + match.StatBonus(captain, CaptainStat.Speed):0.##}", PartyRank(run, captain, c => c.speed));
        Stat(stats, "HEALTH", $"{captain.health}", PartyRank(run, captain, c => c.health));
        float defense = DefenseMath.AtLevel(captain.defense, captain.defensePerLevel, level) + match.StatBonus(captain, CaptainStat.Defense);
        Stat(stats, "DEFENSE", $"{defense:0.#}", level > 1 ? $"base {captain.defense:0.#}" : PartyRank(run, captain, c => c.defense));
        Stat(stats, "RESERVE", $"{captain.ResourceCapacity}", PartyRank(run, captain, c => c.ResourceCapacity));
        Stat(stats, "DRAW", $"{captain.ResourceRegen:0.#}/s", PartyRank(run, captain, c => c.ResourceRegen));

        UIKit.Size(UIKit.Header(window, "Special"), flexibleWidth: 1f);
        Transform special = UIKit.Group(window, "Special");
        UIKit.Layout<HorizontalLayoutGroup>(special, 0, 6);
        UIKit.Icon(special, captain.Ability != null ? captain.Ability.icon : null, 32f);
        Text specialName = UIKit.Label(special, captain.Ability != null ? captain.Ability.displayName : "No special ability", 14);
        specialName.fontStyle = FontStyle.Bold;
        if (captain.Ability != null && !string.IsNullOrEmpty(captain.Ability.description))
            UIKit.Label(window, captain.Ability.description, 12, color: UIKit.MutedTextColor);

        UIKit.Size(UIKit.Group(window, "Gap"), height: UIKit.Gap);
        TagLine(window, "Likes", captain.likes, UIKit.GoodColor);
        TagLine(window, "Dislikes", captain.dislikes, UIKit.BadColor);

        // The stats follow the soldiers, so the window is drawn again whenever one is changed.
        BuildLoadout(window, run, captain, () =>
        {
            Destroy(overlay.gameObject);
            ShowCaptainDetails(run, captain);
        });

        UIKit.Size(UIKit.Group(window, "Gap"), height: UIKit.Gap);
        Transform buttons = UIKit.Group(window, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 6);
        UIKit.Size(buttons, flexibleWidth: 1f);
        if (!run.HasDeparted(captain))
            UIKit.Size(UIKit.Button(buttons, "Gift", () => ShowGiftWindow(run, captain, () => UIKit.Refresh(detailRefreshers))),
                height: 26f, flexibleWidth: 1f);
        Button close = UIKit.Button(buttons, "Close", () => Destroy(overlay.gameObject));
        UIKit.Size(close, height: 26f, flexibleWidth: 1f);
        UIKit.PadBadge(close, PadGlyph.East);
    }

    // ---------- Inventory and gifts ----------

    // A window over the map. A click beside it dismisses it, and so does rebuilding the screen.
    RectTransform OpenWindow(string name, float width, Vector2 anchor, Vector2 offset, float dim)
    {
        RectTransform backdrop = UIKit.Stretch(UIKit.Panel(screen, name, new Color(0f, 0f, 0f, dim)));
        var dismiss = backdrop.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(() => Destroy(backdrop.gameObject));
        UIKit.Focus(backdrop, () => Destroy(backdrop.gameObject));

        RectTransform window = UIKit.Box(backdrop, "Window", WindowColor);
        // Swallows clicks on the window, which would otherwise bubble up to the backdrop and close it.
        window.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        UIKit.Place(window, anchor, offset, new Vector2(width, 0f));
        UIKit.Layout<VerticalLayoutGroup>(window, 12, 6, fitContent: true);
        return window;
    }

    // The window for giving a captain a gift: their portrait, what they say, and the items you
    // carry, each marked liked, disliked or neutral. onGiven runs after each gift.
    void ShowGiftWindow(RunState run, Captain captain, Action onGiven)
    {
        // Beside the captain window, not over it, and no second dimming layer.
        RectTransform window = OpenWindow("Gift", GiftWindowWidth, new Vector2(1f, 0.5f),
            new Vector2(-(DetailsWidth + 2f * UIKit.Pad), 0f), 0f);
        GameObject backdrop = window.parent.gameObject;

        RectTransform top = UIKit.Group(window, "Top");
        UIKit.Layout<HorizontalLayoutGroup>(top, 0, 12).childAlignment = TextAnchor.UpperLeft;
        UIKit.Size(top, flexibleWidth: 1f);
        RectTransform frame = UIKit.Box(top, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.Portrait, DetailsPortraitSize);

        RectTransform info = UIKit.Group(top, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 4);
        UIKit.Size(info, flexibleWidth: 1f);
        UIKit.Shadowed(UIKit.Label(info, captain.displayName, 20)).fontStyle = FontStyle.Bold;
        var live = new List<Action>();
        UIKit.LiveLabel(info, () => $"Bond {BondMath.RankName(ApprovalStore.RankOf(captain))}   Approval {ApprovalStore.ApprovalOf(captain):0}",
            live, 12, color: UIKit.MutedTextColor);
        Text speech = UIKit.Label(info, $"\"{captain.GiftGreeting}\"", 14);
        UIKit.Size(speech, flexibleWidth: 1f);

        UIKit.Size(UIKit.Header(window, "Inventory"), flexibleWidth: 1f);
        ScrollRect list = UIKit.ScrollList(window, "Items", UIKit.Gap);
        UIKit.Size(list, height: GiftListHeight, flexibleWidth: 1f);
        Action<GiftData> give = null;
        give = gift =>
        {
            GiftReaction? reaction = match.GiveGift(captain, gift);
            if (reaction == null) return;
            speech.text = $"\"{captain.GiftLine(reaction.Value)}\"";
            UIKit.Refresh(live);
            onGiven?.Invoke();
            BuildGiftRows(list.content, run, captain, give);
        };
        BuildGiftRows(list.content, run, captain, give);
        UIKit.Refresh(live);
        Button close = UIKit.Button(window, "Close", () => Destroy(backdrop));
        UIKit.Size(close, height: 26f, flexibleWidth: 1f);
        UIKit.PadBadge(close, PadGlyph.East);
    }

    // One row per item carried. With a captain, each row says how they feel about the item and has
    // a Give button; without one, the rows are just a list.
    void BuildGiftRows(Transform list, RunState run, Captain captain, Action<GiftData> give)
    {
        UIKit.Clear(list);
        bool any = false;
        foreach (GiftData gift in match.Data.shopGifts)
        {
            int count = run.GiftCount(gift);
            if (gift == null || count <= 0) continue;
            any = true;
            RectTransform row = UIKit.Box(list, gift.displayName, CardColor, blocksClicks: false);
            UIKit.Size(row, flexibleWidth: 1f);
            UIKit.Layout<HorizontalLayoutGroup>(row, 6, 8).childAlignment = TextAnchor.MiddleLeft;
            UIKit.Icon(row, gift.icon, 32f);

            RectTransform text = UIKit.Group(row, "Text");
            UIKit.Layout<VerticalLayoutGroup>(text, 0, 1);
            UIKit.Size(text, flexibleWidth: 1f);
            UIKit.Label(text, count > 1 ? $"{gift.displayName}  x{count}" : gift.displayName, 14).fontStyle = FontStyle.Bold;
            if (captain == null)
            {
                if (!string.IsNullOrEmpty(gift.description)) UIKit.Label(text, gift.description, 11, color: UIKit.MutedTextColor);
                continue;
            }
            // With a captain, the second line says how they feel about it instead.
            GiftReaction reaction = captain.ReactionTo(gift);
            string change = $"{gift.ApprovalFor(reaction):+0;-0;0}";
            Color color = reaction == GiftReaction.Liked ? UIKit.GoodColor : reaction == GiftReaction.Disliked ? UIKit.BadColor : UIKit.MutedTextColor;
            string word = reaction == GiftReaction.Liked ? "Likes it" : reaction == GiftReaction.Disliked ? "Dislikes it" : "Neutral";
            UIKit.Label(text, $"{word}  {change}", 12, color: color);
            GiftData chosen = gift;
            UIKit.Button(row, "Give", () => give(chosen), 60f, 24f);
        }
        if (!any)
            UIKit.Label(list, captain == null ? "Nothing yet. Buy gifts at a shop." : "You have no gifts to give. Buy some at a shop.",
                13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
    }

    // Portrait, then name, level and XP, element (with its status), role and bond.
    void DetailsHeader(Transform window, RunState run, Captain captain, int level, List<Action> detailRefreshers)
    {
        RectTransform top = UIKit.Group(window, "Top");
        UIKit.Layout<HorizontalLayoutGroup>(top, 0, 12).childAlignment = TextAnchor.UpperLeft;
        UIKit.Size(top, flexibleWidth: 1f);
        RectTransform frame = UIKit.Box(top, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.Portrait, DetailsPortraitSize);

        RectTransform info = UIKit.Group(top, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 3);
        UIKit.Size(info, flexibleWidth: 1f);

        Transform title = UIKit.Group(info, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 6);
        Text name = UIKit.Shadowed(UIKit.Label(title, captain.displayName, 20));
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        if (run.HasDeparted(captain))
            UIKit.Label(title, "WALKED OUT", 10, TextAnchor.MiddleRight, UIKit.BadColor).fontStyle = FontStyle.Bold;

        // Nothing here changes while the window is open, so the meters are drawn once rather than
        // joining the screen's refreshers, which would outlive the window.
        // The bond meter is redrawn when a gift is given, through detailRefreshers.
        var drawOnce = detailRefreshers;
        int xp = run.XpOf(captain);
        bool maxLevel = level >= LevelMath.MaxLevel;
        int from = LevelMath.XpForLevel(level), to = LevelMath.XpForLevel(level + 1);
        UIKit.LiveBar(info, () => maxLevel ? 1f : (xp - from) / (float)Mathf.Max(1, to - from),
            () => maxLevel ? $"Level {level}  (max)" : $"Level {level}   XP {xp}/{to}", UIKit.XpBarColor, drawOnce);

        ElementData element = match.Data.ElementInfo(captain.element);
        Transform elementRow = UIKit.Group(info, "Element");
        UIKit.Layout<HorizontalLayoutGroup>(elementRow, 0, 4);
        UIKit.Icon(elementRow, element != null ? element.icon : null, IconSize);
        UIKit.Label(elementRow, element != null ? element.displayName : captain.element.ToString(), 12, color: UIKit.ElementColor(captain.element));
        if (element != null && !string.IsNullOrEmpty(element.statusName))
            UIKit.Label(elementRow, $"inflicts {element.statusName}", 12, color: UIKit.MutedTextColor);
        UIKit.Size(UIKit.Group(elementRow, "Gap"), 6f);
        UIKit.Icon(elementRow, match.Data.RoleIcon(captain.role), IconSize);
        UIKit.Label(elementRow, captain.ClassName, 12);

        UIKit.LiveBar(info, () => (ApprovalStore.ApprovalOf(captain) - BondMath.MinApproval) / (BondMath.MaxApproval - BondMath.MinApproval),
            () => $"Bond {BondMath.RankName(ApprovalStore.RankOf(captain))}   Approval {ApprovalStore.ApprovalOf(captain):0}", BondBarColor, drawOnce);
        UIKit.LiveLabel(info, () => NextRankLine(captain), drawOnce, 11, color: UIKit.MutedTextColor);
        UIKit.Refresh(drawOnce);
    }

    // What the next bond rank takes and gives, e.g. "Next: B at 65 (opens Slot 4)".
    static string NextRankLine(Captain captain)
    {
        int rank = ApprovalStore.RankOf(captain);
        if (rank >= BondMath.MaxRank) return "Bond is at its highest rank";
        int next = rank + 1;
        string reward = BondMath.SoldierCount(next) > BondMath.SoldierCount(rank)
            ? $"opens Slot {BondMath.SoldierCount(next)}"
            : "stronger status and charge";
        return $"Next: {BondMath.RankName(next)} at {BondMath.ApprovalForRank(next):0} ({reward})";
    }

    static void TagLine(Transform parent, string caption, CaptainTag[] tags, Color color)
    {
        Transform row = UIKit.Group(parent, caption);
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 8);
        Text label = UIKit.Label(row, caption.ToUpperInvariant(), 10, color: color);
        label.fontStyle = FontStyle.Bold;
        UIKit.Size(label, 64f);
        UIKit.Label(row, string.Join(", ", tags), 12);
    }

    // The soldier equipped in each slot, saved with the run and fielded when the captain's tower is
    // placed; a slot's button opens the soldier picker. Slots the captain's bond hasn't opened yet
    // say when they open. onChanged runs after a soldier is equipped or removed.
    void BuildLoadout(Transform window, RunState run, Captain captain, Action onChanged)
    {
        UIKit.Size(UIKit.Header(window, "Soldiers"), flexibleWidth: 1f);
        UIKit.Label(window, "Soldiers take this captain's element and add a stat by class: Knight Defense, Archer Range, " +
                            "Mage Power, Rogue Speed. Click a slot to change its soldier.", 10, color: UIKit.MutedTextColor);
        int open = match.SoldierCountFor(captain);
        for (int i = 0; i < RunState.MaxSoldierSlots; i++)
        {
            int slot = i;
            Transform row = UIKit.Group(window, $"Slot {slot + 1}");
            UIKit.Layout<HorizontalLayoutGroup>(row, 0, 4);
            UIKit.Size(row, flexibleWidth: 1f);
            Text label = UIKit.Label(row, $"Slot {slot + 1}", 12, color: UIKit.MutedTextColor);
            UIKit.Size(label, 64f);
            if (slot >= open)
            {
                UIKit.Size(UIKit.Label(row, $"Opens at bond {BondMath.RankName(RankOpening(slot))}", 12, color: UIKit.DisabledTextColor),
                    height: 22f, flexibleWidth: 1f);
                continue;
            }

            SoldierItem soldier = run.Soldiers.InSlot(captain.id, slot);
            Button button = UIKit.Button(row, soldier != null ? SoldierSummary(captain, soldier) : "Empty", () =>
                ShowSoldierPicker(run, captain, slot, onChanged), height: 22f);
            UIKit.Size(button, height: 22f, flexibleWidth: 1f);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = 12;
            if (soldier == null) text.color = UIKit.DisabledTextColor;
        }
    }

    // "Rare Knight  +0.75 Defense  Melee", with a word on the captain's taste for the class.
    string SoldierSummary(Captain captain, SoldierItem soldier)
    {
        SoldierClassData soldierClass = match.Data.ClassData(soldier.soldierClass);
        string summary = $"{CaptainPanelParts.SoldierName(soldier, soldierClass)}  {CaptainPanelParts.SoldierBonus(soldier, soldierClass)}";
        if (soldierClass != null) summary += $"  {soldierClass.damageType}";
        if (soldierClass == null || !Relationships.TryGetClassTag(soldierClass, out CaptainTag tag)) return summary;
        if (Array.IndexOf(captain.likes, tag) >= 0) return summary + $"  <color={UIKit.Hex(UIKit.GoodColor)}>liked</color>";
        return Array.IndexOf(captain.dislikes, tag) >= 0 ? summary + $"  <color={UIKit.Hex(UIKit.BadColor)}>disliked</color>" : summary;
    }

    // Beside the captain window: every soldier owned, those in the inventory first, rarest first.
    // Equip puts one in the slot, moving it from another captain if it was theirs, and Remove sends
    // the slot's soldier back to the inventory. Either closes the picker and runs onChanged.
    void ShowSoldierPicker(RunState run, Captain captain, int slot, Action onChanged)
    {
        RectTransform window = OpenWindow("Soldiers", GiftWindowWidth, new Vector2(1f, 0.5f),
            new Vector2(-(DetailsWidth + 2f * UIKit.Pad), 0f), 0f);
        GameObject backdrop = window.parent.gameObject;
        UIKit.Size(UIKit.Header(window, $"{captain.displayName}  Slot {slot + 1}"), flexibleWidth: 1f);

        void Change(Action change)
        {
            change();
            RunSave.Save(run);
            Destroy(backdrop);
            onChanged?.Invoke();
        }

        ScrollRect list = UIKit.ScrollList(window, "Soldiers", UIKit.Gap);
        UIKit.Size(list, height: GiftListHeight, flexibleWidth: 1f);
        var soldiers = new List<SoldierItem>(run.Soldiers.Items);
        soldiers.Sort((a, b) => a.IsEquipped != b.IsEquipped ? a.IsEquipped.CompareTo(b.IsEquipped)
            : b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : a.soldierClass.CompareTo(b.soldierClass));
        foreach (SoldierItem soldier in soldiers)
        {
            bool here = soldier.equippedTo == captain.id && soldier.slot == slot;
            RectTransform row = UIKit.Box(list.content, soldier.id, CardColor, blocksClicks: false);
            UIKit.Size(row, flexibleWidth: 1f);
            UIKit.Layout<HorizontalLayoutGroup>(row, 6, 8).childAlignment = TextAnchor.MiddleLeft;
            SoldierClassData soldierClass = match.Data.ClassData(soldier.soldierClass);
            UIKit.Icon(row, soldierClass != null ? soldierClass.portrait : null, 32f);

            RectTransform text = UIKit.Group(row, "Text");
            UIKit.Layout<VerticalLayoutGroup>(text, 0, 1);
            UIKit.Size(text, flexibleWidth: 1f);
            UIKit.Label(text, SoldierSummary(captain, soldier), 13);
            UIKit.Label(text, here ? "In this slot" : WhereIs(run, soldier), 11, color: UIKit.MutedTextColor);
            SoldierItem chosen = soldier;
            if (here) UIKit.Button(row, "Remove", () => Change(() => run.Soldiers.Unequip(chosen.id)), 70f, 24f);
            else UIKit.Button(row, "Equip", () => Change(() => run.Soldiers.Equip(chosen.id, captain.id, slot)), 70f, 24f);
        }
        if (soldiers.Count == 0)
            UIKit.Label(list.content, "You have no soldiers yet.", 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        Button close = UIKit.Button(window, "Close", () => Destroy(backdrop));
        UIKit.Size(close, height: 26f, flexibleWidth: 1f);
        UIKit.PadBadge(close, PadGlyph.East);
    }

    static string WhereIs(RunState run, SoldierItem soldier)
    {
        if (!soldier.IsEquipped) return "In the inventory";
        Captain owner = run.Profile.Find(soldier.equippedTo);
        return owner != null ? $"With {owner.displayName}, slot {soldier.slot + 1}" : "With another captain";
    }

    // The lowest bond rank whose soldier count reaches past this slot.
    int RankOpening(int slot)
    {
        for (int rank = 0; rank <= BondMath.MaxRank; rank++)
            if (Mathf.Max(match.Data.soldiersPerTower, BondMath.SoldierCount(rank)) > slot) return rank;
        return BondMath.MaxRank;
    }

    // Layers run left to right; cities in a layer are spread evenly top to bottom, nudged a little
    // (the same way every time, from the city's seed) so the map doesn't look like a grid. The
    // enemy base is pinned to the right edge, centred.
    static Vector2 CityPosition(WorldMap map, MapNode node)
    {
        bool isBase = node.id == map.BaseId;
        Vector2 size = isBase ? BaseSize : CitySize;
        float usableWidth = MapArea.x - BaseSize.x - 2f * MapMargin, usableHeight = MapArea.y - CitySize.y - 2f * MapMargin - CityJitter;
        float x = map.layerCount > 1 ? node.layer / (float)(map.layerCount - 1) : 0.5f;
        int count = map.LayerSize(node.layer);
        float y = isBase ? 0.5f : (node.lane + 0.5f) / count;
        Vector2 position = new((x - 0.5f) * usableWidth, (0.5f - y) * usableHeight);
        if (isBase) return position;
        var jitter = new System.Random(node.seed);
        position.x += ((float)jitter.NextDouble() * 2f - 1f) * CityJitterX;
        position.y += ((float)jitter.NextDouble() * 2f - 1f) * CityJitter * (count > 1 ? 1f : 0.3f);
        return position;
    }

    // A road as a gentle curve of short straight pieces. bendSeed picks which way and how far it bows.
    static void BuildRoad(RectTransform area, Vector2 from, Vector2 to, int bendSeed, Color color, float thickness, bool dashed = false)
    {
        Vector2 delta = to - from;
        Vector2 side = new Vector2(-delta.y, delta.x).normalized;
        float bend = (Mathf.Abs(bendSeed) % 5 - 2) * 6f;
        int pieces = Mathf.Max(2, Mathf.CeilToInt(delta.magnitude / (dashed ? 7f : 18f)));
        Vector2 previous = from;
        for (int i = 1; i <= pieces; i++)
        {
            float t = i / (float)pieces;
            Vector2 point = Vector2.Lerp(from, to, t) + side * (bend * 4f * t * (1f - t));
            if (!dashed || i % 2 == 1) BuildRoadPiece(area, previous, point, color, thickness);
            previous = point;
        }
    }

    static void BuildRoadPiece(RectTransform area, Vector2 from, Vector2 to, Color color, float thickness)
    {
        RectTransform road = UIKit.Panel(area, "Road", color, blocksClicks: false);
        Vector2 delta = to - from;
        // A little longer than the piece, so the joints in a curve leave no gaps.
        UIKit.Place(road, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(delta.magnitude + 1.5f, thickness));
        road.pivot = new Vector2(0.5f, 0.5f);
        road.anchoredPosition = (from + to) * 0.5f;
        road.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    // A dashed road that fades into a "?" toward a city the player has no intel on yet.
    static void BuildFogStub(RectTransform area, Vector2 from, Vector2 hidden)
    {
        Vector2 direction = (hidden - from).normalized;
        float length = Mathf.Min((hidden - from).magnitude * 0.6f, CitySize.x * 0.5f + 40f);
        Vector2 end = from + direction * length;
        BuildRoad(area, from, end, 0, FogRoadColor, 2f, dashed: true);
        Text mark = UIKit.Label(area, "?", 14, TextAnchor.MiddleCenter, FogRoadColor);
        mark.fontStyle = FontStyle.Bold;
        UIKit.Place(mark.rectTransform, new Vector2(0.5f, 0.5f), end + direction * 8f, new Vector2(20f, 20f));
    }

    // What a kind of stop is marked with: a letter in a badge, and its colour.
    static (string letter, Color color, string name) KindMark(NodeKind kind, bool isBase) =>
        isBase ? ("!", BaseColor, "Enemy base") : kind switch
        {
            NodeKind.Elite => ("E", EliteColor, "Elite battle"),
            NodeKind.Rest => ("R", RestColor, "Rest"),
            NodeKind.Event => ("?", EventColor, "Event"),
            _ => ("B", UIKit.ButtonColor, "Battle"),
        };

    // A row under the map naming each kind of stop by its badge.
    void BuildLegend(RectTransform column)
    {
        RectTransform legend = UIKit.Group(column, "Legend");
        UIKit.Layout<HorizontalLayoutGroup>(legend, 0, 14, fitContent: true).childAlignment = TextAnchor.MiddleCenter;
        foreach (NodeKind kind in new[] { NodeKind.City, NodeKind.Elite, NodeKind.Rest, NodeKind.Event })
            LegendEntry(legend, KindMark(kind, false));
        LegendEntry(legend, KindMark(NodeKind.City, true));
    }

    static void LegendEntry(Transform legend, (string letter, Color color, string name) mark)
    {
        RectTransform entry = UIKit.Group(legend, mark.name);
        UIKit.Layout<HorizontalLayoutGroup>(entry, 0, 4, fitContent: true);
        Badge(entry, mark.letter, mark.color, 16f, 10);
        UIKit.Label(entry, mark.name, 10, color: UIKit.MutedTextColor);
    }

    static void Badge(Transform parent, string letter, Color color, float size, int fontSize)
    {
        RectTransform badge = UIKit.Box(parent, "Badge", color, blocksClicks: false);
        UIKit.Size(badge, size, size);
        Text text = UIKit.Shadowed(UIKit.Label(badge, letter, fontSize, TextAnchor.MiddleCenter));
        text.fontStyle = FontStyle.Bold;
        UIKit.Stretch(text.rectTransform);
    }

    void BuildCity(RectTransform area, WorldMap map, MapNode node, bool choosable)
    {
        bool isBase = node.id == map.BaseId;
        string cityName = match.Data.layouts.Count > 0 ? match.Data.layouts[node.layout % match.Data.layouts.Count].displayName : "City";
        string name = isBase ? "Enemy base" : node.kind switch
        {
            NodeKind.Elite => cityName,
            NodeKind.Rest => "Rest",
            NodeKind.Event => "Event ?",
            _ => cityName,
        };
        bool here = node.id == map.current;
        Vector2 size = isBase ? BaseSize : CitySize;
        Vector2 position = CityPosition(map, node);

        // A second line with what matters when choosing: gates, Gold, healing.
        var mark = KindMark(node.kind, isBase);
        string info = isBase ? "Boss fight" : node.kind switch
        {
            NodeKind.Elite => $"{match.Data.eliteGoldMultiplier:0.#}x Gold" + GateCount(node, " | "),
            NodeKind.Rest => $"Heals {match.RestAmount}",
            NodeKind.Event => "Unknown",
            _ => GateCount(node, "").TrimStart(),
        };
        string text = name + (info.Length > 0 ? $"\n<size=9><color={UIKit.Hex(choosable ? new Color(0.16f, 0.1f, 0.03f, 1f) : UIKit.MutedTextColor)}>{info}</color></size>" : "");

        Button city = UIKit.Button(area, text, () => match.ChooseCity(node.id));
        Text cityLabel = city.GetComponentInChildren<Text>();
        cityLabel.fontSize = isBase ? 12 : 11;
        cityLabel.alignment = TextAnchor.MiddleLeft;
        cityLabel.rectTransform.offsetMin = new Vector2(BadgeSize + 10f, 4f);
        city.interactable = choosable;
        var image = city.GetComponent<Image>();
        image.color = choosable ? UIKit.SelectedButtonColor : node.cleared ? ClearedCityColor : isBase ? BaseColor
            : node.kind == NodeKind.Elite ? EliteColor : node.IsMatch ? UIKit.ButtonColor : StopColor;
        // Cities that can't be picked yet stay readable rather than greyed out by the button.
        if (!choosable)
        {
            ColorBlock colors = city.colors;
            colors.disabledColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            city.colors = colors;
        }
        var rect = (RectTransform)city.transform;
        UIKit.Place(rect, new Vector2(0.5f, 0.5f), position, size);
        // The kind's badge at the left edge, and the layout size can't push the label under it.
        RectTransform badge = UIKit.Box(rect, "Badge", mark.color, blocksClicks: false);
        UIKit.Place(badge, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(BadgeSize, BadgeSize));
        Text letter = UIKit.Shadowed(UIKit.Label(badge, mark.letter, 15, TextAnchor.MiddleCenter));
        letter.fontStyle = FontStyle.Bold;
        UIKit.Stretch(letter.rectTransform);

        // Rings: gold for where the player stands, pulsing gold for what can be picked, and a
        // standing ring on elite and base cities so they read as dangerous.
        Color ring = isBase ? new Color(0.9f, 0.3f, 0.3f, 1f) : node.kind == NodeKind.Elite ? new Color(0.78f, 0.5f, 0.9f, 1f) : Color.clear;
        if (here) ring = UIKit.AccentColor;
        Image outline = UIKit.Outline(rect, ring);
        if (choosable)
        {
            Color glow = UIKit.AccentColor;
            refreshers.Add(() =>
            {
                glow.a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                outline.color = glow;
            });
        }

        string flag = here ? "YOU ARE HERE" : node.cleared ? "WON" : null;
        if (flag != null)
        {
            Text tag = UIKit.Shadowed(UIKit.Label(area, flag, 9, TextAnchor.MiddleCenter, here ? UIKit.AccentColor : UIKit.GoodColor));
            tag.fontStyle = FontStyle.Bold;
            UIKit.Place(tag.rectTransform, new Vector2(0.5f, 0.5f), position + new Vector2(0f, size.y * 0.5f + 8f), new Vector2(110f, 14f));
        }
        UIKit.HoverTip(rect, name, NodeDescription(node, isBase) + GateNote(node));
    }

    // "3 gates" for a battle on more than one road, else empty; prefix goes first when there is text.
    string GateCount(MapNode node, string prefix)
    {
        if (!node.IsMatch || match.Data.layouts.Count == 0) return "";
        int gates = match.Data.layouts[node.layout % match.Data.layouts.Count].AllRoutes().Count;
        return gates > 1 ? $"{prefix}{gates} gates" : "";
    }

    // Warns about a battle fought on more than one road.
    string GateNote(MapNode node)
    {
        if (!node.IsMatch || match.Data.layouts.Count == 0) return "";
        int gates = match.Data.layouts[node.layout % match.Data.layouts.Count].AllRoutes().Count;
        return gates > 1 ? $" Enemies come from {gates} gates." : "";
    }

    string NodeDescription(MapNode node, bool isBase)
    {
        if (isBase) return "The enemy base. Beat its boss to win the run.";
        switch (node.kind)
        {
            case NodeKind.Elite:
                return $"A tougher battle: enemies from {match.Data.eliteLayerBonus} layers further on, " +
                    $"{match.Data.eliteGoldMultiplier:0.#}x Gold from kills, and a wider choice of captains to recruit on a win.";
            case NodeKind.Rest: return $"Rest instead of fighting. Heals {match.RestAmount} health.";
            case NodeKind.Event: return "No battle. Something unexpected happens.";
            default: return "A battle. Defend the city against the waves.";
        }
    }

    // ---------- Stops ----------

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
                flexibleWidth: 1f);
        }
        UIKit.PadBadge(UIKit.Button(panel, "Leave the shop", leave, 220f, 32f), PadGlyph.East);
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

    // ---------- Results ----------

    void BuildResults()
    {
        bool won = match.State == MatchState.Won;
        RunState run = RunState.Current;

        RectTransform root = NewScreen("Results");
        RectTransform panel = UIKit.Box(root, "Panel");
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(panel, 16, 6, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        bool runWon = won && run.Map != null && run.Map.IsComplete;
        Title(panel, runWon ? "The enemy base has fallen" : won ? "Victory" : "Defeat", won ? UIKit.AccentColor : UIKit.BadColor);
        string summary = runWon ? $"Run won in {run.MatchesPlayed} matches"
            : won ? $"Match {run.MatchesPlayed} won{(match.IsEliteMatch ? " (elite city)" : "")}"
            : $"The run ends after {run.MatchesPlayed} match{(run.MatchesPlayed == 1 ? "" : "es")} won";
        UIKit.Label(panel, summary, 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform stats = UIKit.Group(panel, "Stats");
        UIKit.Layout<HorizontalLayoutGroup>(stats, 0, 8, fitContent: true).padding.top = UIKit.Gap;
        Stat(stats, "WAVES", $"{match.WavesCleared}/{match.WaveCount}");
        Stat(stats, "KILLS", $"{match.Kills}");
        Stat(stats, "LEAKS", $"{match.Leaks}");
        Stat(stats, "CASUALTIES", $"{match.Casualties}");
        Stat(stats, "HEALTH", $"{run.Health}/{run.MaxHealth}");
        Stat(stats, "GOLD", $"{run.Gold}").color = UIKit.AccentColor;
        if (match.WinGoldEarned + match.ReserveRefunded > 0)
            UIKit.Label(panel, $"+{match.WinGoldEarned} Gold for the win, +{match.ReserveRefunded} returned from reserves", 12, TextAnchor.MiddleCenter, UIKit.GoodColor);
        if (match.HealthRestored > 0)
            UIKit.Label(panel, $"+{match.HealthRestored} health for the win", 12, TextAnchor.MiddleCenter, UIKit.GoodColor);

        UIKit.Size(UIKit.Header(panel, "Captains"), flexibleWidth: 1f);
        ResultColumns(ResultRow(panel, false), "", "LEVEL", "BOND", "APPROVAL", "CHANGE", 9, UIKit.MutedTextColor).preferredHeight = 14f;
        for (int i = 0; i < run.Party.Count; i++)
            CaptainResultRow(panel, run, run.Party[i], i % 2 == 0);

        UIKit.Size(UIKit.Group(panel, "Gap"), height: UIKit.Gap);
        if (won && !runWon) UIKit.Primary(UIKit.Button(panel, "Back to the map", match.ReturnToMap, 220f, 32f), refreshers);
        RectTransform buttons = UIKit.Group(panel, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 8, fitContent: true);
        Button newRun = UIKit.Button(buttons, "New run", () => match.RestartRun(), 150f, 28f);
        if (!won || runWon) UIKit.Primary(newRun, refreshers);
        UIKit.Button(buttons, "Main menu", match.ReturnToMenu, 150f, 28f);
    }

    // The run's match number, health (with a meter) and Gold, in a framed box in the top right.
    void BuildRunStats(RectTransform root, RunState run)
    {
        RectTransform box = UIKit.Box(root, "Run Stats", blocksClicks: false);
        UIKit.Place(box, Vector2.one, new Vector2(-UIKit.Pad, -UIKit.Pad), Vector2.zero);
        HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(box, 6, 6, fitContent: true);
        layout.padding.left = layout.padding.right = 10;
        // Top-aligned, so the captions line up although the health column is taller.
        layout.childAlignment = TextAnchor.UpperCenter;

        Stat(box, "MATCH", $"{run.MatchesPlayed + 1}");
        StatRule(box);
        Text health = Stat(box, "HEALTH", $"{run.Health}/{run.MaxHealth}");
        if (run.Health * 3 <= run.MaxHealth) health.color = UIKit.BadColor;
        float healthFraction = run.Health / (float)Mathf.Max(1, run.MaxHealth);
        UIKit.LiveBar(health.transform.parent, () => healthFraction, () => "", UIKit.HealthBarColor, refreshers, 8f);
        StatRule(box);
        Stat(box, "GOLD", $"{run.Gold}").color = UIKit.AccentColor;
    }

    static void StatRule(Transform parent) => UIKit.Size(UIKit.Panel(parent, "Rule", UIKit.RuleColor, blocksClicks: false), 2f, 34f);

    // A stat column; sub is a small line under the value, such as how the captain ranks in the party.
    static Text Stat(Transform parent, string caption, string value, string sub = null)
    {
        RectTransform stat = UIKit.Group(parent, caption);
        UIKit.Layout<VerticalLayoutGroup>(stat, 0, -1f).childAlignment = TextAnchor.UpperCenter;
        UIKit.Size(stat, 66f);
        UIKit.Label(stat, caption, 9, TextAnchor.MiddleCenter, UIKit.MutedTextColor).fontStyle = FontStyle.Bold;
        Text text = UIKit.Label(stat, value, 15, TextAnchor.MiddleCenter);
        if (sub != null) UIKit.Label(stat, sub, 9, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        return text;
    }

    // "#2/4": where the captain stands among the party still with the run on a stat, 1 being highest.
    static string PartyRank(RunState run, Captain captain, Func<Captain, float> stat)
    {
        List<Captain> present = run.Party.FindAll(c => c != null && !run.HasDeparted(c));
        if (!present.Contains(captain) || present.Count < 2) return null;
        float mine = stat(captain);
        int rank = 1 + present.FindAll(c => stat(c) > mine + 0.001f).Count;
        return $"#{rank}/{present.Count}";
    }

    static RectTransform ResultRow(Transform panel, bool shaded)
    {
        RectTransform row = UIKit.Panel(panel, "Row", shaded ? UIKit.InsetColor : Color.clear, blocksClicks: false);
        HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(row, 0, 8);
        layout.padding.left = layout.padding.right = UIKit.Pad;
        return row;
    }

    // The captain table's columns: name, level, bond, approval and its change.
    static LayoutElement ResultColumns(RectTransform row, string name, string level, string bond, string approval, string change,
        int size, Color color, Color? changeColor = null)
    {
        UIKit.Size(UIKit.Label(row, name, size + 1, color: color), flexibleWidth: 1f);
        UIKit.Size(UIKit.Label(row, level, size, color: color), 44f);
        UIKit.Size(UIKit.Label(row, bond, size, color: color), 56f);
        UIKit.Size(UIKit.Label(row, approval, size + 1, TextAnchor.MiddleRight, color), 54f);
        UIKit.Size(UIKit.Label(row, change, size, TextAnchor.MiddleRight, changeColor ?? color), 48f);
        return UIKit.Size(row, height: 22f, flexibleWidth: 1f);
    }

    // One table row per captain, with any walkout noted after the name.
    void CaptainResultRow(Transform panel, RunState run, Captain captain, bool shaded)
    {
        Tower tower = Tower.All.Find(t => t.Captain == captain);
        float change = relationships != null ? relationships.ApprovalChangeThisMatch(captain) : 0f;
        int level = tower != null ? tower.Level : Mathf.Max(captain.level, LevelMath.LevelForXp(run.XpOf(captain)));
        bool walkedOut = relationships != null && relationships.Walkouts.Contains(captain);
        string note = walkedOut ? $"  <color={UIKit.Hex(UIKit.BadColor)}>walked out!</color>"
            : run.HasDeparted(captain) ? $"  <color={UIKit.Hex(UIKit.MutedTextColor)}>(left earlier)</color>" : "";

        Color changeColor = change > 0f ? UIKit.GoodColor : change < 0f ? UIKit.BadColor : UIKit.MutedTextColor;
        ResultColumns(ResultRow(panel, shaded), captain.displayName + note, $"Lv {level}", BondMath.RankName(ApprovalStore.RankOf(captain)),
            $"{ApprovalStore.ApprovalOf(captain):0}", $"{change:+0.#;-0.#;0}", 12, UIKit.TextColor, changeColor);
    }
}
