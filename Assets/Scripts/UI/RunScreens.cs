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
    const float SlotHeight = 96f;
    const float RowHeight = 58f;
    static readonly Vector2 MapArea = new(860f, 330f);
    static readonly Vector2 CitySize = new(104f, 34f);
    // World map party drawer.
    const float DrawerWidth = 300f;
    const float DrawerSlideSeconds = 0.2f;
    static readonly Vector2 DrawerTabSize = new(30f, 110f);
    static readonly Color RoadColor = new(0.55f, 0.45f, 0.3f, 1f);
    static readonly Color ClearedCityColor = new(0.16f, 0.3f, 0.2f, 1f);
    static readonly Color BaseColor = new(0.55f, 0.12f, 0.16f, 1f);
    static readonly Color EliteColor = new(0.45f, 0.2f, 0.38f, 1f);
    static readonly Color StopColor = new(0.18f, 0.3f, 0.42f, 1f);

    MatchManager match;
    Relationships relationships;
    Canvas canvas;
    RectTransform screen;
    readonly List<Action> refreshers = new();
    readonly List<CaptainData> picked = new();
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
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    RectTransform NewScreen(string name)
    {
        screen = UIKit.Stretch(UIKit.Panel(canvas.transform, name, DimColor));
        return screen;
    }

    // ---------- Setup ----------

    List<CaptainData> Available
    {
        get
        {
            var available = new List<CaptainData>();
            foreach (CaptainData captain in match.Data.roster)
                if (captain != null) available.Add(captain);
            return available;
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

        var slots = new List<Button>();
        for (int i = 0; i < PartySize; i++)
        {
            int slot = i;
            Button button = UIKit.Button(party, "", () => { if (slot < picked.Count) picked.RemoveAt(slot); });
            UIKit.Size(button, height: SlotHeight, flexibleWidth: 1f);
            // Added once: a GameObject takes only one layout group, and slots are refilled often.
            UIKit.Layout<VerticalLayoutGroup>(button, 8, 1).padding.top = 6;
            slots.Add(button);
        }
        // Slots only change on a pick, so they rebuild then rather than refreshing every frame.
        string shown = null;
        refreshers.Add(() =>
        {
            string key = string.Join("|", picked);
            if (key == shown) return;
            shown = key;
            for (int i = 0; i < slots.Count; i++) FillSlot(slots[i], i < picked.Count ? picked[i] : null, i);
        });

        UIKit.Size(UIKit.Group(party, "Spacer"), flexibleWidth: 1f).flexibleHeight = 1f;
        Button begin = UIKit.LiveButton(party, () => picked.Count == PartySize ? "Begin run" : $"Pick {PartySize - picked.Count} more",
            () => match.BeginRun(picked), refreshers, () => picked.Count == PartySize, height: 34f);
        UIKit.Size(begin, height: 34f, flexibleWidth: 1f);
        UIKit.Primary(begin, refreshers);
        UIKit.Size(UIKit.Button(party, "Main menu", match.ReturnToMenu), height: 24f, flexibleWidth: 1f);
    }

    // An empty slot is an inset placeholder; a filled one shows the captain like a recruit card.
    void FillSlot(Button slot, CaptainData captain, int index)
    {
        UIKit.Clear(slot.transform);
        slot.interactable = captain != null;
        var image = slot.GetComponent<Image>();
        ColorBlock colors = slot.colors;
        // An empty slot can't be clicked, but shouldn't look broken either.
        colors.disabledColor = Color.white;
        slot.colors = colors;
        if (captain == null)
        {
            image.color = UIKit.InsetColor;
            Text empty = UIKit.Label(slot.transform, $"Slot {index + 1}\n<size=10>Pick a captain from the list</size>", 13,
                TextAnchor.MiddleCenter, UIKit.DisabledTextColor);
            UIKit.Size(empty, flexibleWidth: 1f).flexibleHeight = 1f;
            return;
        }
        image.color = PickedCardColor;
        CaptainDetails(slot.transform, captain, "CLICK TO REMOVE", out Text name, out Text tag);
        name.color = UIKit.AccentColor;
        tag.color = UIKit.MutedTextColor;
    }

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
        foreach (CaptainData captain in Available)
            BuildRosterRow(list.content, captain);
    }

    // A compact row: name, element and role, bond; then the special; then likes and dislikes.
    // Rows of captains who can't join (the party is full) fade out.
    void BuildRosterRow(Transform list, CaptainData captain)
    {
        Button row = UIKit.Button(list, "", () => TogglePick(captain));
        UIKit.Clear(row.transform);
        UIKit.Size(row, height: RowHeight, flexibleWidth: 1f);
        UIKit.Layout<VerticalLayoutGroup>(row, 8, 0).padding.top = 5;
        var image = row.GetComponent<Image>();
        var fade = row.gameObject.AddComponent<CanvasGroup>();

        string muted = UIKit.Hex(UIKit.MutedTextColor);
        Transform title = UIKit.Group(row.transform, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 8);
        Text name = UIKit.Label(title, captain.displayName, 14);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, 84f);
        UIKit.Size(UIKit.Label(title, $"<color={UIKit.Hex(UIKit.ElementColor(captain.element))}>{captain.element}</color> {captain.role}", 12),
            flexibleWidth: 1f);
        Text tag = UIKit.Label(title, "IN PARTY", 9, TextAnchor.MiddleRight, UIKit.AccentColor);
        tag.fontStyle = FontStyle.Bold;
        UIKit.Label(title, $"<color={muted}>Bond</color> {BondMath.RankName(ApprovalStore.RankOf(captain))} " +
                           $"<color={muted}>({ApprovalStore.ApprovalOf(captain):0})</color>", 12, TextAnchor.MiddleRight);

        UIKit.Label(row.transform, captain.ability != null ? $"<color={muted}>Special</color>  {captain.ability.displayName}" : "No special", 12);
        UIKit.Label(row.transform, $"<color={UIKit.Hex(UIKit.GoodColor)}>Likes</color>  {string.Join(", ", captain.likes)}      " +
                                   $"<color={UIKit.Hex(UIKit.BadColor)}>Dislikes</color>  {string.Join(", ", captain.dislikes)}", 11,
            color: UIKit.MutedTextColor).horizontalOverflow = HorizontalWrapMode.Overflow;

        Image ring = UIKit.Outline(row.transform, UIKit.AccentColor);
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
    static void CaptainDetails(Transform card, CaptainData captain, string tagText, out Text name, out Text tag)
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
        UIKit.Label(card, $"<color={UIKit.Hex(UIKit.ElementColor(captain.element))}>{captain.element}</color> {captain.role}" +
                          $"   <color={muted}>Bond</color> {BondMath.RankName(rank)} <color={muted}>({ApprovalStore.ApprovalOf(captain):0})</color>", 12);
        UIKit.Label(card, captain.ability != null ? $"<color={muted}>Special</color>  {captain.ability.displayName}" : "No special", 12);
        UIKit.Label(card, $"<color={UIKit.Hex(UIKit.GoodColor)}>Likes</color>  {string.Join(", ", captain.likes)}", 11,
            color: UIKit.MutedTextColor);
        UIKit.Label(card, $"<color={UIKit.Hex(UIKit.BadColor)}>Dislikes</color>  {string.Join(", ", captain.dislikes)}", 11,
            color: UIKit.MutedTextColor);
    }

    // Picked cards keep their dark face, so the text stays readable, and get a gold ring and name.
    void BuildCaptainCard(Transform parent, CaptainData captain, Action onClick, Func<bool> selected)
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

    void TogglePick(CaptainData captain)
    {
        if (picked.Remove(captain)) return;
        if (picked.Count < PartySize) picked.Add(captain);
    }

    // ---------- Recruiting ----------

    void BuildRecruit()
    {
        RunState run = RunState.Current;
        CaptainData chosen = null;

        RectTransform root = NewScreen("Recruit");
        RectTransform column = UIKit.Group(root, "Column");
        UIKit.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 10, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        UIKit.Label(column, "A CAPTAIN OFFERS TO JOIN", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, $"Recruit one for the rest of the run, or go on without.  Party {run.ActivePartyCount}/{match.Data.maxPartySize}",
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
        foreach (CaptainData captain in run.RecruitOffer)
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

        UIKit.Label(column, "WORLD MAP", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, "Pick the next stop. Every road leads to the enemy base; each stop reveals the roads beyond it.",
            12, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        BuildRunStats(root, run);

        RectTransform area = UIKit.Box(column, "Map", blocksClicks: false);
        UIKit.Size(area, MapArea.x, MapArea.y);

        var choices = map.Choices();
        foreach (MapNode node in map.nodes)
            if (node.revealed)
                foreach (int next in node.next)
                    if (map.nodes[next].revealed)
                        BuildRoad(area, CityPosition(map, node), CityPosition(map, map.nodes[next]));
        foreach (MapNode node in map.nodes)
            if (node.revealed) BuildCity(area, map, node, choices.Exists(c => c.id == node.id));

        RectTransform buttons = UIKit.Group(column, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 10, fitContent: true);
        UIKit.Button(buttons, "Main menu", match.ReturnToMenu, 160f, 24f);
        UIKit.Button(buttons, "Abandon run", match.RestartRun, 160f, 24f);

        BuildPartyDrawer(root, run);
    }

    // The party in a drawer that slides in from the left. Its tab stays on the screen edge while
    // it's closed; the tab, the Close button or a click beside the drawer toggles it.
    void BuildPartyDrawer(RectTransform root, RunState run)
    {
        bool open = false;
        float shown = 0f;

        RectTransform blocker = UIKit.Stretch(UIKit.Panel(root, "Drawer Blocker", new Color(0f, 0f, 0f, 0.4f)));
        var dismiss = blocker.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(() => open = false);
        var blockerFade = blocker.gameObject.AddComponent<CanvasGroup>();

        RectTransform drawer = UIKit.Box(root, "Party Drawer");
        drawer.anchorMin = Vector2.zero;
        drawer.anchorMax = new Vector2(0f, 1f);
        drawer.pivot = new Vector2(0f, 0.5f);
        drawer.sizeDelta = new Vector2(DrawerWidth, -2f * UIKit.Pad);
        UIKit.Layout<VerticalLayoutGroup>(drawer, UIKit.Pad, 6);

        Transform title = UIKit.Group(drawer, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 6);
        string muted = UIKit.Hex(UIKit.MutedTextColor);
        Text header = UIKit.Label(title, $"PARTY   <color={muted}>{run.ActivePartyCount}/{match.Data.maxPartySize}</color>", 10,
            color: UIKit.AccentColor);
        header.fontStyle = FontStyle.Bold;
        UIKit.Size(header, flexibleWidth: 1f);
        UIKit.Button(title, "Close", () => open = false, 60f, 22f);

        ScrollRect list = UIKit.ScrollList(drawer, "Captains", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        foreach (CaptainData captain in run.Party)
            PartyCard(list.content, run, captain);

        // The tab hangs off the drawer's right edge, so it rides along as the drawer slides.
        Button tab = UIKit.Button(drawer, "", () => open = !open, DrawerTabSize.x, DrawerTabSize.y);
        tab.GetComponent<LayoutElement>().ignoreLayout = true;
        UIKit.Place((RectTransform)tab.transform, new Vector2(1f, 0.5f), Vector2.zero, DrawerTabSize);
        ((RectTransform)tab.transform).pivot = new Vector2(0f, 0.5f);
        Text tabLabel = tab.GetComponentInChildren<Text>();
        tabLabel.fontStyle = FontStyle.Bold;
        UIKit.Place(tabLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, UIKit.Gap / 2f), new Vector2(DrawerTabSize.y, DrawerTabSize.x));
        tabLabel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        refreshers.Add(() =>
        {
            shown = Mathf.MoveTowards(shown, open ? 1f : 0f, Time.unscaledDeltaTime / DrawerSlideSeconds);
            float eased = shown * shown * (3f - 2f * shown);
            // Whole reference pixels, so the frame's texels stay crisp mid-slide.
            drawer.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(-DrawerWidth, 0f, eased)), 0f);
            blocker.gameObject.SetActive(shown > 0f);
            blockerFade.alpha = eased;
            tabLabel.text = open ? "CLOSE" : "PARTY";
        });
    }

    // A captain in the party drawer: the usual details, tagged with their level, or faded with a
    // note when they've walked out.
    static void PartyCard(Transform list, RunState run, CaptainData captain)
    {
        bool departed = run.HasDeparted(captain);
        RectTransform card = UIKit.Box(list, captain.displayName, departed ? UIKit.InsetColor : CardColor, blocksClicks: false);
        UIKit.Layout<VerticalLayoutGroup>(card, 8, 1).padding.top = 6;
        UIKit.Size(card, flexibleWidth: 1f);
        int level = Mathf.Max(captain.level, LevelMath.LevelForXp(run.XpOf(captain)));
        CaptainDetails(card, captain, departed ? "WALKED OUT" : $"LV {level}", out _, out Text tag);
        if (!departed) return;
        tag.color = UIKit.BadColor;
        card.gameObject.AddComponent<CanvasGroup>().alpha = 0.6f;
    }

    // Layers run left to right; cities in a layer are spread evenly top to bottom.
    static Vector2 CityPosition(WorldMap map, MapNode node)
    {
        float usableWidth = MapArea.x - CitySize.x - 20f, usableHeight = MapArea.y - CitySize.y - 20f;
        float x = map.layerCount > 1 ? node.layer / (float)(map.layerCount - 1) : 0.5f;
        int size = map.LayerSize(node.layer);
        float y = (node.lane + 0.5f) / size;
        return new Vector2((x - 0.5f) * usableWidth, (0.5f - y) * usableHeight);
    }

    static void BuildRoad(RectTransform area, Vector2 from, Vector2 to)
    {
        RectTransform road = UIKit.Panel(area, "Road", RoadColor, blocksClicks: false);
        Vector2 delta = to - from;
        UIKit.Place(road, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(delta.magnitude, 3f));
        road.pivot = new Vector2(0.5f, 0.5f);
        road.anchoredPosition = (from + to) * 0.5f;
        road.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    void BuildCity(RectTransform area, WorldMap map, MapNode node, bool choosable)
    {
        bool isBase = node.id == map.BaseId;
        string cityName = match.Data.layouts.Count > 0 ? match.Data.layouts[node.layout % match.Data.layouts.Count].displayName : "City";
        string name = isBase ? "Enemy base" : node.kind switch
        {
            NodeKind.Elite => $"Elite: {cityName}",
            NodeKind.Rest => "Rest",
            NodeKind.Shop => "Shop",
            NodeKind.Event => "Event ?",
            _ => cityName,
        };
        if (node.id == map.current) name += " (here)";
        else if (node.cleared) name += " (won)";

        Button city = UIKit.Button(area, name, () => match.ChooseCity(node.id));
        city.GetComponentInChildren<Text>().fontSize = 11;
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
        UIKit.Place((RectTransform)city.transform, new Vector2(0.5f, 0.5f), Vector2.zero, CitySize);
        ((RectTransform)city.transform).pivot = new Vector2(0.5f, 0.5f);
        ((RectTransform)city.transform).anchoredPosition = CityPosition(map, node);
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
            case NodeKind.Shop: BuildShop(panel); break;
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

    void BuildShop(RectTransform panel)
    {
        RunState run = RunState.Current;
        StopHeader(panel, "SHOP", $"Buy a gift for a captain: {match.Data.giftCost} Gold for +{match.Data.giftApproval:0} approval.");
        foreach (CaptainData captain in run.Party)
        {
            if (run.HasDeparted(captain)) continue;
            UIKit.LiveButton(panel,
                () => $"{captain.displayName}   Bond {BondMath.RankName(ApprovalStore.RankOf(captain))}   Approval {ApprovalStore.ApprovalOf(captain):0}",
                () => match.BuyGift(captain), refreshers, () => match.CanBuyGift(captain), width: 400f, height: 26f);
        }
        UIKit.Button(panel, "Leave the shop", match.LeaveStop, 220f, 32f);
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
        Button newRun = UIKit.Button(buttons, "New run", match.RestartRun, 150f, 28f);
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

    static Text Stat(Transform parent, string caption, string value)
    {
        RectTransform stat = UIKit.Group(parent, caption);
        UIKit.Layout<VerticalLayoutGroup>(stat, 0, -1f).childAlignment = TextAnchor.UpperCenter;
        UIKit.Size(stat, 66f);
        UIKit.Label(stat, caption, 9, TextAnchor.MiddleCenter, UIKit.MutedTextColor).fontStyle = FontStyle.Bold;
        return UIKit.Label(stat, value, 15, TextAnchor.MiddleCenter);
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
    void CaptainResultRow(Transform panel, RunState run, CaptainData captain, bool shaded)
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
