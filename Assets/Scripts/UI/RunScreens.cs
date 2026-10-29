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
    // Short enough that three rows (the full 12-captain roster) fit on screen with the title.
    const float CardHeight = 112f;
    static readonly Vector2 MapArea = new(860f, 330f);
    static readonly Vector2 CitySize = new(104f, 34f);
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

    void BuildSetup()
    {
        RectTransform root = NewScreen("Run Setup");
        RectTransform column = UIKit.Group(root, "Column");
        UIKit.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 10, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        Title(column, "PROJECT BABEL", UIKit.TextColor);
        string accent = UIKit.Hex(UIKit.AccentColor);
        UIKit.LiveLabel(column, () => $"Choose {PartySize} captains for this run   <color={accent}>{picked.Count}/{PartySize}</color>",
            refreshers, 14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform grid = UIKit.Group(column, "Captains");
        var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(205f, CardHeight);
        gridLayout.spacing = new Vector2(8f, 8f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        int rows = Mathf.CeilToInt(Available.Count / 4f);
        UIKit.Size(grid, 4 * 205f + 3 * 8f, rows * CardHeight + (rows - 1) * 8f);

        picked.Clear();
        foreach (CaptainData captain in Available)
        {
            if (picked.Count < PartySize) picked.Add(captain);
            BuildCaptainCard(grid, captain, () => TogglePick(captain), () => picked.Contains(captain));
        }

        UIKit.Primary(UIKit.LiveButton(column, () => "Begin run", () => match.BeginRun(picked), refreshers,
            () => picked.Count == PartySize, width: 220f, height: 34f), refreshers);
        UIKit.Button(column, "Main menu", match.ReturnToMenu, 160f, 24f);
    }

    static Text Title(Transform parent, string text, Color color)
    {
        Text title = UIKit.Shadowed(UIKit.Label(parent, text, 30, TextAnchor.MiddleCenter, color));
        title.fontStyle = FontStyle.Bold;
        return title;
    }

    // Picked cards keep their dark face, so the text stays readable, and get a gold ring and name.
    void BuildCaptainCard(Transform parent, CaptainData captain, Action onClick, Func<bool> selected)
    {
        Button card = UIKit.Button(parent, "", onClick);
        UIKit.Clear(card.transform);
        var image = card.GetComponent<Image>();
        UIKit.Layout<VerticalLayoutGroup>(card, 8, 1).padding.top = 6;

        Transform title = UIKit.Group(card.transform, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 4);
        Text name = UIKit.Label(title, captain.displayName, 15);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        Text tag = UIKit.Label(title, "IN PARTY", 9, TextAnchor.MiddleRight, UIKit.AccentColor);
        tag.fontStyle = FontStyle.Bold;

        int rank = ApprovalStore.RankOf(captain);
        string muted = UIKit.Hex(UIKit.MutedTextColor);
        UIKit.Label(card.transform, $"<color={UIKit.Hex(UIKit.ElementColor(captain.element))}>{captain.element}</color> {captain.role}" +
                                    $"   <color={muted}>Bond</color> {BondMath.RankName(rank)} <color={muted}>({ApprovalStore.ApprovalOf(captain):0})</color>", 12);
        UIKit.Label(card.transform, captain.ability != null ? $"<color={muted}>Special</color>  {captain.ability.displayName}" : "No special", 12);
        UIKit.Label(card.transform, $"<color={UIKit.Hex(UIKit.GoodColor)}>Likes</color>  {string.Join(", ", captain.likes)}", 11,
            color: UIKit.MutedTextColor);
        UIKit.Label(card.transform, $"<color={UIKit.Hex(UIKit.BadColor)}>Dislikes</color>  {string.Join(", ", captain.dislikes)}", 11,
            color: UIKit.MutedTextColor);

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
        UIKit.Label(column, $"Match {run.MatchesPlayed + 1}    Health {run.Health}/{run.MaxHealth}    Gold {run.Gold}    " +
                            "Pick the next stop. Every road leads to the enemy base; each stop reveals the roads beyond it.",
            12, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

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

        // One wrapped line so a full party of 8 still fits under the map.
        var party = new List<string>();
        foreach (CaptainData captain in run.Party)
            party.Add($"{captain.displayName} ({captain.role}, Lv {Mathf.Max(captain.level, LevelMath.LevelForXp(run.XpOf(captain)))}, " +
                      $"Bond {BondMath.RankName(ApprovalStore.RankOf(captain))}{(run.HasDeparted(captain) ? ", walked out" : "")})");
        UIKit.Label(column, $"Party: {string.Join("    ", party)}", 12, TextAnchor.MiddleCenter);

        RectTransform buttons = UIKit.Group(column, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 10, fitContent: true);
        UIKit.Button(buttons, "Main menu", match.ReturnToMenu, 160f, 24f);
        UIKit.Button(buttons, "Abandon run", match.RestartRun, 160f, 24f);
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
