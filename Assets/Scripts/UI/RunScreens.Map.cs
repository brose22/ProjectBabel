using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The world map: the screen, abandoning a run, and the drawing of cities, roads and the legend.
public partial class RunScreens
{
    void BuildMap()
    {
        RunState run = RunState.Current;
        WorldMap map = run.Map;

        RectTransform root = NewScreen("World Map");
        RectTransform column = UIKit.Group(root, "Column");
        // Centred in the space right of the button column.
        UIKit.Place(column, new Vector2(0.5f, 0.5f), new Vector2((UIKit.Pad + MapMenuWidth + MapMenuGap) / 2f, 0f), new Vector2(MapArea.x, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 8, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        bool abandonArmed = this.abandonArmed;
        this.abandonArmed = false;

        int nextLayer = map.current < 0 ? 0 : map.nodes[map.current].layer + 1;
        string progress = nextLayer >= map.layerCount - 1 ? "The enemy base lies ahead" : $"Stop {nextLayer + 1} of {map.layerCount}";
        UIKit.Label(column, match.Location != null ? match.Location.displayName.ToUpperInvariant() : "WORLD MAP", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, $"{progress}. Choose one of the glowing stops; each stop you reach reveals the roads beyond it.",
            12, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        BuildRunStats(root, run);

        // The map scrolls up and down: the start is at the bottom, the enemy base at the top.
        RectTransform frame = UIKit.Box(column, "Map", blocksClicks: false);
        UIKit.Size(frame, MapArea.x, MapArea.y);
        var scroll = frame.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = UIKit.Stretch(UIKit.Panel(frame, "Viewport", Color.clear));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform area = UIKit.Group(viewport, "Content");
        float mapHeight = MapHeight(map);
        area.anchorMin = area.anchorMax = new Vector2(0.5f, 0f);
        area.pivot = new Vector2(0.5f, 0f);
        area.sizeDelta = new Vector2(MapArea.x, mapHeight);
        scroll.viewport = viewport;
        scroll.content = area;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 8f;
        // Follow the player: the row they stand on sits low in the view, the choices just above it.
        float focusY = (map.current < 0 ? CityPosition(map, map.nodes[0]).y : CityPosition(map, map.nodes[map.current]).y + MapRowSpacing * 0.5f)
            + mapHeight * 0.5f;
        area.anchoredPosition = new Vector2(0f, -Mathf.Clamp(focusY - MapArea.y * 0.45f, 0f, Mathf.Max(0f, mapHeight - MapArea.y)));

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

        // The button column down the left edge; Inventory and Party open the drawers, which slide
        // out over it.
        RectTransform menu = UIKit.Group(root, "Menu");
        UIKit.Place(menu, new Vector2(0f, 0.5f), new Vector2(UIKit.Pad, 0f), new Vector2(MapMenuWidth, 0f));
        UIKit.Layout<VerticalLayoutGroup>(menu, 0, UIKit.Gap, fitContent: true);
        Button MenuButton(string text, Action onClick)
        {
            Button button = UIKit.Button(menu, text, onClick, MapMenuWidth, 28f);
            UIKit.Size(button, MapMenuWidth, 28f);
            return button;
        }
        // The shoulder button that toggles each drawer, shown while a gamepad is in use.
        UIKit.PadBadge(MenuButton("Inventory", () => ToggleDrawer("Inventory")), PadGlyph.LeftShoulder, () => GameInput.UsingGamepad,
            new Vector2(1f, 0.5f), Vector2.left * 12f);
        UIKit.PadBadge(MenuButton("Party", () => ToggleDrawer("Party")), PadGlyph.RightShoulder, () => GameInput.UsingGamepad,
            new Vector2(1f, 0.5f), Vector2.left * 12f);
        Button shop = MenuButton("Shop", () => SetShopOpen(true));
        if (shopClosed) screenFocus.Default = () => shop;
        shopClosed = false;
        MenuButton("Main menu", match.ReturnToMenu);
        UIKit.Size(UIKit.Group(menu, "Spacer"), MapMenuWidth, 16f);
        // Abandoning ends the run and costs Gold and a soldier, so it takes a second press, and the
        // first one says what it costs.
        Button abandon = MenuButton(abandonArmed ? "Confirm abandon" : "Abandon run", () =>
        {
            if (abandonArmed) { Abandon(run); return; }
            this.abandonArmed = true;
            shownFor = (MatchState)(-1);
        });
        if (abandonArmed)
        {
            abandon.GetComponent<Image>().color = UIKit.BadColor;
            Text warning = UIKit.Label(menu, $"You will lose {RunEndRules.AbandonGoldPercent}% of your Gold " +
                $"({RunEndRules.AbandonGoldLost(run.Gold)}) and one soldier of your choice. Your captains are kept.",
                11, TextAnchor.UpperLeft, UIKit.BadColor);
            UIKit.Size(warning, MapMenuWidth);
            // Rebuilding disarms. The gamepad lands on Cancel, so a stray confirm press doesn't abandon.
            Button cancel = MenuButton("Cancel", () => shownFor = (MatchState)(-1));
            screenFocus.Default = () => cancel;
        }

        // Two drawers share the left edge. Only one is open at a time; each is built over the
        // last, so the later one's blocker covers the earlier.
        openDrawer = null;
        drawerOpened.Clear();
        BuildDrawer(root, "Party",
            $"PARTY   <color={UIKit.Hex(UIKit.MutedTextColor)}>{run.ActivePartyCount}/{match.Data.maxPartySize}</color>",
            list =>
            {
                var cards = new List<(Captain, GameObject)>();
                foreach (Captain captain in run.Party)
                    cards.Add((captain, PartyCard(list.content, run, captain).gameObject));
                Text empty = UIKit.Label(list.content, "Nothing matches these filters.", 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
                void Fill()
                {
                    bool any = false;
                    foreach (var (captain, card) in cards)
                    {
                        bool shown = partyDrawerFilter.Matches(ListFilterBar.ValuesOf(captain));
                        card.SetActive(shown);
                        any |= shown;
                    }
                    empty.gameObject.SetActive(!any);
                }
                ListFilterBar.Captains(list.transform.parent, partyDrawerFilter, Fill).SetSiblingIndex(list.transform.GetSiblingIndex());
                drawerOpened["Party"] = () => { partyDrawerFilter.Reset(); Fill(); };
                Fill();
            });
        BuildDrawer(root, "Inventory", "INVENTORY",
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
        if (abandonPicking) BuildAbandonPicker(root, run);
    }

    // The confirmed abandon: the Gold goes at once, then the player picks the soldier to lose.
    // With no soldiers there is nothing to pick, and the run ends straight away.
    void Abandon(RunState run)
    {
        run.Profile.AbandonRun();
        ProfileSave.Save(run.Profile);
        if (run.Soldiers.Items.Count == 0) { match.ReturnToCastle(); return; }
        abandonPicking = true;
        abandonFilter.Reset();
        shownFor = (MatchState)(-1);
    }

    // Over the map, with no way back: every soldier owned, commonest first, each with a Lose button.
    // The chosen one is unequipped and removed, and the run ends at the Castle.
    void BuildAbandonPicker(RectTransform root, RunState run)
    {
        // No way back: the backdrop and the cancel button do nothing.
        ModalWindow modal = UIKit.Modal(root, "Abandon", GiftWindowWidth, new Vector2(0.5f, 0.5f), Vector2.zero,
            onCancel: () => { }, backdrop: DimColor);
        UIFocusScope focus = modal.Scope;
        RectTransform panel = modal.Window;
        UIKit.Size(UIKit.Header(panel, "Choose a soldier to lose"), flexibleWidth: 1f);
        Text note = UIKit.Label(panel, "Abandoning the run costs one soldier. They leave for good, even if a captain has them.",
            12, color: UIKit.MutedTextColor);
        UIKit.Size(note, flexibleWidth: 1f);

        ScrollRect list = null;
        Button first = null;
        void Fill()
        {
            UIKit.Clear(list.content);
            first = null;
            var soldiers = abandonFilter.Apply(run.Soldiers.Items, s => SoldierValues(run, s));
            soldiers.Sort((a, b) => b.rarity != a.rarity ? a.rarity.CompareTo(b.rarity) : a.soldierClass.CompareTo(b.soldierClass));
            foreach (SoldierItem soldier in soldiers)
            {
                RectTransform row = UIKit.Box(list.content, soldier.id, CardColor, blocksClicks: false);
                UIKit.Size(row, flexibleWidth: 1f);
                UIKit.Layout<HorizontalLayoutGroup>(row, 6, 8).childAlignment = TextAnchor.MiddleLeft;
                SoldierClassData soldierClass = match.Data.ClassData(soldier.soldierClass);
                UIKit.Icon(row, soldierClass != null ? soldierClass.portrait : null, 32f);
    
                RectTransform text = UIKit.Group(row, "Text");
                UIKit.Layout<VerticalLayoutGroup>(text, 0, 1);
                UIKit.Size(text, flexibleWidth: 1f);
                UIKit.Label(text, $"{CaptainPanelParts.SoldierName(soldier, soldierClass)}  {CaptainPanelParts.SoldierBonus(soldier, soldierClass)}", 13);
                UIKit.Label(text, WhereIs(run, soldier), 11, color: UIKit.MutedTextColor);
                string chosen = soldier.id;
                Button lose = UIKit.Button(row, "Lose", () =>
                {
                    run.Profile.LoseSoldier(chosen);
                    abandonPicking = false;
                    match.ReturnToCastle();
                }, 70f, 24f);
                first ??= lose;
            }
            if (soldiers.Count == 0) ListFilterBar.Empty(list.content, run.Soldiers.Items.Count == 0, "You have no soldiers.");
        }
        ListFilterBar.Soldiers(panel, abandonFilter, Fill);
        list = UIKit.ScrollList(panel, "Soldiers", UIKit.Gap);
        UIKit.Size(list, height: GiftListHeight, flexibleWidth: 1f);
        Fill();
        // The gamepad starts on the first soldier's button, the commonest one.
        focus.Default = () => first;
    }

    // Whether "Abandon run" has been clicked once and now waits for a second click.
    bool abandonArmed;

    // Whether the run was abandoned and the player is picking the soldier to lose.
    bool abandonPicking;

    // The scrolling map's height: one row per layer, never shorter than the view.
    public static float MapHeight(WorldMap map) =>
        Mathf.Max(MapArea.y, Mathf.Max(0, map.layerCount - 1) * MapRowSpacing + (CitySize.y + BaseSize.y) * 0.5f + 2f * MapMargin);

    // Layers run bottom to top, one row each; cities in a row are spread evenly left to right,
    // nudged a little (the same way every time, from the city's seed) so the map doesn't look like
    // a grid. The enemy base is the top row, centred. Positions are from the map's centre.
    public static Vector2 CityPosition(WorldMap map, MapNode node)
    {
        bool isBase = node.id == map.BaseId;
        float height = MapHeight(map);
        float graphHeight = Mathf.Max(0, map.layerCount - 1) * MapRowSpacing + (CitySize.y + BaseSize.y) * 0.5f;
        // From the bottom of the map; a short graph is centred in the view.
        float bottom = (height - graphHeight) * 0.5f + CitySize.y * 0.5f;
        float y = bottom + node.layer * MapRowSpacing;
        int count = map.LayerSize(node.layer);
        float x = isBase ? 0.5f : (node.lane + 0.5f) / count;
        Vector2 position = new((x - 0.5f) * MapLaneWidth, y - height * 0.5f);
        if (isBase) return position;
        var jitter = new System.Random(node.seed);
        position.y += ((float)jitter.NextDouble() * 2f - 1f) * CityJitterAlong;
        position.x += ((float)jitter.NextDouble() * 2f - 1f) * CityJitterAcross * (count > 1 ? 1f : 0.3f);
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
            // On a dark tab, since the roads ahead run up through it.
            Vector2 tagSize = new(here ? 78f : 34f, 13f);
            RectTransform tab = UIKit.Panel(area, "Flag", new Color(0.05f, 0.05f, 0.07f, 0.85f), blocksClicks: false);
            UIKit.Place(tab, new Vector2(0.5f, 0.5f), position + new Vector2(0f, size.y * 0.5f + 7f), tagSize);
            Text tag = UIKit.Shadowed(UIKit.Label(tab, flag, 9, TextAnchor.MiddleCenter, here ? UIKit.AccentColor : UIKit.GoodColor));
            tag.fontStyle = FontStyle.Bold;
            UIKit.Stretch(tag.rectTransform);
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
}
