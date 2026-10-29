using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Full-screen run screens around a match: run setup (title and captain pick) before a run's first
// match, the world map between matches, and results after every match. Built in uGUI at runtime
// and added to the match by MatchManager, like Relationships.
public class RunScreens : MonoBehaviour
{
    static readonly Color DimColor = new(0.02f, 0.02f, 0.04f, 0.82f);
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

        UIKit.Label(column, "PROJECT BABEL", 30, TextAnchor.MiddleCenter);
        UIKit.LiveLabel(column, () => $"Choose {PartySize} captains for this run  ({picked.Count}/{PartySize})", refreshers,
            14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

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

        UIKit.LiveButton(column, () => "Begin run", () => match.BeginRun(picked), refreshers,
            () => picked.Count == PartySize, width: 220f, height: 34f);
    }

    void BuildCaptainCard(Transform parent, CaptainData captain, Action onClick, Func<bool> selected)
    {
        Button card = UIKit.Button(parent, "", onClick);
        UIKit.Clear(card.transform);
        var image = card.GetComponent<Image>();
        refreshers.Add(() => image.color = selected() ? UIKit.SelectedButtonColor : UIKit.ButtonColor);
        UIKit.Layout<VerticalLayoutGroup>(card, 6, 1);

        int rank = ApprovalStore.RankOf(captain);
        UIKit.Label(card.transform, captain.displayName, 15);
        UIKit.Label(card.transform, $"{captain.element} {captain.role}   Bond {BondMath.RankName(rank)} ({ApprovalStore.ApprovalOf(captain):0})", 12);
        UIKit.Label(card.transform, captain.ability != null ? $"Special: {captain.ability.displayName}" : "No special", 12);
        UIKit.Label(card.transform, $"Likes: {string.Join(", ", captain.likes)}", 11);
        UIKit.Label(card.transform, $"Dislikes: {string.Join(", ", captain.dislikes)}", 11);
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
        UIKit.LiveButton(buttons, () => chosen != null ? $"Recruit {chosen.displayName}" : "Pick a captain",
            () => match.Recruit(chosen), refreshers, () => chosen != null, width: 200f, height: 32f);
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

        RectTransform area = UIKit.Panel(column, "Map", UIKit.PanelColor, blocksClicks: false);
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

        UIKit.Button(column, "Abandon run", match.RestartRun, 160f, 24f);
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

    // ---------- Results ----------

    void BuildResults()
    {
        bool won = match.State == MatchState.Won;
        RunState run = RunState.Current;

        RectTransform root = NewScreen("Results");
        RectTransform panel = UIKit.Panel(root, "Panel", UIKit.PanelColor);
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(panel, 16, 6, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        bool runWon = won && run.Map != null && run.Map.IsComplete;
        UIKit.Label(panel, runWon ? "The enemy base has fallen" : won ? "Victory" : "Defeat", 30, TextAnchor.MiddleCenter);
        string summary = runWon ? $"Run won in {run.MatchesPlayed} matches"
            : won ? $"Match {run.MatchesPlayed} won{(match.IsEliteMatch ? " (elite city)" : "")}"
            : $"The run ends after {run.MatchesPlayed} match{(run.MatchesPlayed == 1 ? "" : "es")} won";
        UIKit.Label(panel, summary, 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        UIKit.Label(panel, $"Waves {match.WavesCleared}/{match.WaveCount}    Kills {match.Kills}    Leaks {match.Leaks}    Casualties {match.Casualties}",
            13, TextAnchor.MiddleCenter);
        string restored = match.HealthRestored > 0 ? $" (+{match.HealthRestored} for the win)" : "";
        UIKit.Label(panel, $"Health {run.Health}/{run.MaxHealth}{restored}    Gold {run.Gold}", 13, TextAnchor.MiddleCenter);

        UIKit.Label(panel, "Captains", 15, TextAnchor.MiddleCenter);
        foreach (CaptainData captain in run.Party)
        {
            Tower tower = Tower.All.Find(t => t.Captain == captain);
            float change = relationships != null ? relationships.ApprovalChangeThisMatch(captain) : 0f;
            string level = tower != null ? $"Lv {tower.Level}" : $"Lv {Mathf.Max(captain.level, LevelMath.LevelForXp(run.XpOf(captain)))}";
            string line = $"{captain.displayName}   {level}   Bond {BondMath.RankName(ApprovalStore.RankOf(captain))}   " +
                          $"Approval {ApprovalStore.ApprovalOf(captain):0} ({change:+0.#;-0.#;0})";
            bool walkedOut = relationships != null && relationships.Walkouts.Contains(captain);
            if (walkedOut) line += "   walked out!";
            else if (run.HasDeparted(captain)) line += "   (left earlier)";
            UIKit.Label(panel, line, 13, TextAnchor.MiddleCenter, walkedOut ? UIKit.SelectedButtonColor : UIKit.TextColor);
        }

        if (won && !runWon) UIKit.Button(panel, "Back to the map", match.ReturnToMap, 220f, 32f);
        UIKit.Button(panel, "New run", match.RestartRun, 220f, 28f);
    }
}
