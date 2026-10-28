using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Full-screen run screens around a match: run setup (title and captain pick) before a run's first
// match, and results after every match with Next match or New run. Built in uGUI at runtime and
// added to the match by MatchManager, like Relationships.
public class RunScreens : MonoBehaviour
{
    static readonly Color DimColor = new(0.02f, 0.02f, 0.04f, 0.82f);
    // Short enough that three rows (the full 12-captain roster) fit on screen with the title.
    const float CardHeight = 112f;

    MatchManager match;
    Relationships relationships;
    Canvas canvas;
    RectTransform screen;
    readonly List<Action> refreshers = new();
    readonly List<CaptainData> picked = new();
    MatchState shownFor = (MatchState)(-1);

    void Awake()
    {
        match = GetComponent<MatchManager>();
        relationships = GetComponent<Relationships>();
    }

    void Start() => canvas = UIKit.CreateCanvas("Run Screens", 20);

    void LateUpdate()
    {
        if (canvas == null) return;
        MatchState wanted = match.State == MatchState.Setup || match.IsOver ? match.State : MatchState.Preparing;
        if (wanted != shownFor)
        {
            shownFor = wanted;
            refreshers.Clear();
            if (screen != null) Destroy(screen.gameObject);
            screen = null;
            if (wanted == MatchState.Setup) BuildSetup();
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
            BuildCaptainCard(grid, captain);
        }

        UIKit.LiveButton(column, () => "Begin run", () => match.BeginRun(picked), refreshers,
            () => picked.Count == PartySize, width: 220f, height: 34f);
    }

    void BuildCaptainCard(Transform parent, CaptainData captain)
    {
        Button card = UIKit.Button(parent, "", () => TogglePick(captain));
        UIKit.Clear(card.transform);
        var image = card.GetComponent<Image>();
        refreshers.Add(() => image.color = picked.Contains(captain) ? UIKit.SelectedButtonColor : UIKit.ButtonColor);
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

        UIKit.Label(panel, won ? "Victory" : "Defeat", 30, TextAnchor.MiddleCenter);
        UIKit.Label(panel, won ? $"Match {run.MatchesPlayed + 1} won" : $"The run ends after {run.MatchesPlayed} match{(run.MatchesPlayed == 1 ? "" : "es")} won",
            13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        UIKit.Label(panel, $"Waves {match.WavesCleared}/{match.WaveCount}    Kills {match.Kills}    Leaks {match.Leaks}    Casualties {match.Casualties}",
            13, TextAnchor.MiddleCenter);
        UIKit.Label(panel, $"Health {run.Health}/{run.MaxHealth}    Gold {run.Gold}", 13, TextAnchor.MiddleCenter);

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

        if (won) UIKit.Button(panel, "Next match", match.NextMatch, 220f, 32f);
        UIKit.Button(panel, "New run", match.RestartRun, 220f, 28f);
    }
}
