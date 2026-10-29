using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// The match HUD, built in uGUI at runtime: top bar, selected-tower panel, move hint, captain
// requests, approval messages and controls hint (RunScreens shows results). Labels and buttons
// refresh every frame; the tower panel and request cards rebuild only when their structure changes.
public class MatchHUD : MonoBehaviour
{
    const float TopBarHeight = 32f;
    const float PanelWidth = 310f;
    const float MessagesWidth = 240f;
    const float RequestWidth = UIKit.ReferenceWidth - PanelWidth - MessagesWidth - 4 * UIKit.Pad;
    static readonly Color NoColor = new(0.5f, 0.2f, 0.2f, 1f);
    static readonly Color YesColor = new(0.22f, 0.44f, 0.25f, 1f);
    static readonly Color HintColor = new(0.06f, 0.07f, 0.1f, 0.85f);

    [SerializeField] MatchManager match;
    [SerializeField] PlacementController placement;

    Relationships relationships;
    CaptainDebugPanel debugPanel;
    CaptainRequests requests;
    Canvas canvas;

    readonly List<Action> barRefreshers = new();
    readonly List<Action> requestRefreshers = new();

    RectTransform requestList;
    RectTransform moveHintBox;
    Text moveHint;
    Text messages;
    string requestKey;

    // Added to the match at runtime, so they are looked up rather than wired in the scene.
    Relationships Relationships => relationships != null ? relationships : relationships = match.GetComponent<Relationships>();
    CaptainRequests Requests => requests != null ? requests : requests = match.GetComponent<CaptainRequests>();

    // Takes a screen position in Input System coordinates; kept for PlacementController.
    public bool IsPointerOverHud(Vector2 screenPosition) => match.IsOver || match.IsBetweenMatches || UIKit.IsPointerOverUI();

    // Gold draw is set by captain requests; only the debug panel lets the player set it directly.
    public bool IsDebugPanelOpen => debugPanel != null && debugPanel.IsOpen;

    void Start()
    {
        canvas = UIKit.CreateCanvas("Match HUD", 10);
        BuildTopBar();

        moveHintBox = UIKit.Box(canvas.transform, "Move Hint", HintColor, blocksClicks: false);
        UIKit.Place(moveHintBox, new Vector2(0.5f, 1f), new Vector2(0f, -TopBarHeight - 6f), Vector2.zero);
        UIKit.Layout<HorizontalLayoutGroup>(moveHintBox, 6, 0, fitContent: true);
        moveHint = UIKit.Shadowed(UIKit.Label(moveHintBox, "", 13, TextAnchor.MiddleCenter, UIKit.AccentColor));

        // Between the tower panel and the approval messages, so none of them overlap.
        requestList = UIKit.Group(canvas.transform, "Requests");
        UIKit.Place(requestList, new Vector2(0f, 1f), new Vector2(PanelWidth + 2 * UIKit.Pad, -TopBarHeight - 36f), new Vector2(RequestWidth, 0f));
        UIKit.Layout<VerticalLayoutGroup>(requestList, 0, UIKit.Gap, fitContent: true);

        messages = UIKit.Shadowed(UIKit.Label(canvas.transform, "", 12, TextAnchor.UpperRight));
        UIKit.Place(messages.rectTransform, new Vector2(1f, 1f), new Vector2(-UIKit.Pad, -TopBarHeight - 8f), new Vector2(MessagesWidth, 120f));

        BuildControlsHint();
        // The full captain panel is a testing tool, kept out of release builds.
        if (Debug.isDebugBuild) debugPanel = new CaptainDebugPanel(canvas.transform, match, placement);
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        // The run setup screen covers everything; the HUD returns once the run begins.
        canvas.enabled = !match.IsBetweenMatches;
        UIKit.Refresh(barRefreshers);
        debugPanel?.Update();
        UpdateRequests();
        UpdateMoveHint();
        messages.text = Relationships != null ? MessagesText(Relationships.RecentMessages) : "";
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // Keys in the accent colour, each followed by what it does.
    void BuildControlsHint()
    {
        RectTransform box = UIKit.Box(canvas.transform, "Controls", HintColor, blocksClicks: false);
        UIKit.Place(box, new Vector2(1f, 0f), new Vector2(-UIKit.Pad, UIKit.Pad), Vector2.zero);
        UIKit.Layout<HorizontalLayoutGroup>(box, 5, 0, fitContent: true);
        string key = UIKit.Hex(UIKit.AccentColor);
        var parts = new List<string>();
        foreach ((string keys, string action) in new[]
                 {
                     ("Click", "place / select"), ("Tab", "next captain"), ("Q", "ability"), ("1-3", "Gold draw"),
                     ("M", "move"), ("X", "sell"), ("Space", "next wave"),
                 })
            parts.Add($"<color={key}>{keys}</color> {action}");
        UIKit.Label(box, string.Join("   ", parts), 11, TextAnchor.MiddleRight, UIKit.TextColor);
    }

    // Approval changes end in a signed number; colour it so gains and losses read at a glance.
    static string MessagesText(IEnumerable<string> recent)
    {
        var lines = new List<string>();
        foreach (string message in recent)
        {
            int split = message.LastIndexOf(' ');
            string amount = split >= 0 ? message.Substring(split + 1) : "";
            if (amount.Length > 1 && (amount[0] == '+' || amount[0] == '-'))
            {
                Color color = amount[0] == '+' ? UIKit.GoodColor : UIKit.BadColor;
                lines.Add($"{message.Substring(0, split)} <color={UIKit.Hex(color)}>{amount}</color>");
            }
            else lines.Add(message);
        }
        return string.Join("\n", lines);
    }

    // ---------- Top bar ----------

    void BuildTopBar()
    {
        RectTransform bar = UIKit.Box(canvas.transform, "Top Bar");
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = Vector2.one;
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0f, TopBarHeight);
        HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(bar, 3, 12);
        layout.padding.left = 10;
        layout.padding.right = 6;

        RunState run() => RunState.Current;
        Text health = Stat(bar, "HEALTH", () => $"{run().Health}/{run().MaxHealth}");
        barRefreshers.Add(() => health.color = run().Health * 3 <= run().MaxHealth ? UIKit.BadColor : UIKit.TextColor);
        Stat(bar, "GOLD", () => $"{run().Gold}").color = UIKit.AccentColor;
        Stat(bar, "CITY", CityLabel);
        Stat(bar, "WAVE", () => $"{Mathf.Min(match.WavesCleared + 1, match.WaveCount)}/{match.WaveCount}");
        Stat(bar, "TOWERS", () => $"{Tower.All.Count}/{match.Data.maxTowers}");
        Stat(bar, "KILLS", () => $"{match.Kills}");
        Stat(bar, "LEAKS", () => $"{match.Leaks}");
        Stat(bar, "CASUALTIES", () => $"{match.Casualties}");
        UIKit.Size(UIKit.Group(bar, "Spacer"), flexibleWidth: 1f);

        Button next = UIKit.LiveButton(bar, NextCaptainLabel, match.CycleNextCaptain, barRefreshers,
            interactable: () => match.CanPlaceTower, width: 190f);
        next.GetComponentInChildren<Text>().fontSize = 12;
        UIKit.Primary(UIKit.LiveButton(bar, WaveButtonLabel, match.StartNextWave, barRefreshers,
            interactable: () => match.State == MatchState.Preparing, width: 130f), barRefreshers);
        UIKit.LiveButton(bar, () => match.GameSpeed > 1f ? "Speed x2" : "Speed x1", match.ToggleGameSpeed, barRefreshers,
            selected: () => match.GameSpeed > 1f, width: 72f);
    }

    // A small caption over a live value.
    Text Stat(Transform bar, string caption, Func<string> value)
    {
        RectTransform stat = UIKit.Group(bar, caption);
        UIKit.Layout<VerticalLayoutGroup>(stat, 0, -2f);
        UIKit.Label(stat, caption, 9, color: UIKit.MutedTextColor).fontStyle = FontStyle.Bold;
        return UIKit.LiveLabel(stat, value, barRefreshers, 13);
    }

    string CityLabel()
    {
        WorldMap map = RunState.Current.Map;
        if (match.City == null || map == null) return "";
        string name = match.IsBaseMatch ? "Enemy base" : match.CityLayout != null ? match.CityLayout.displayName : "City";
        return $"{name} ({match.City.layer + 1}/{map.layerCount})";
    }

    string NextCaptainLabel()
    {
        CaptainData next = match.NextCaptain;
        return match.CanPlaceTower && next != null ? $"Next: {next.displayName} ({next.role})  [Tab]" : "No captains to place";
    }

    string WaveButtonLabel()
    {
        if (match.State == MatchState.WaveActive) return "Wave in progress";
        if (match.State != MatchState.Preparing) return "Match over";
        return match.NextWaveCountdown > 0f ? $"Next wave ({Mathf.CeilToInt(match.NextWaveCountdown)})" : "Start wave";
    }

    void UpdateMoveHint()
    {
        Tower tower = placement.Selected;
        bool moving = placement.IsMoving && tower != null;
        moveHintBox.gameObject.SetActive(moving);
        if (!moving) return;
        moveHint.text = $"Moving {tower.Captain.displayName}: click a free tile (Esc / right-click to cancel)" +
                        (match.CanAffordMove(tower) ? "" : "  - not enough Gold");
    }

    // ---------- Requests ----------

    void UpdateRequests()
    {
        if (Requests == null) return;
        var key = new StringBuilder();
        foreach (CaptainRequests.Request request in Requests.Pending) key.Append(request.GetHashCode()).Append('|');
        if (key.ToString() == requestKey) return;
        requestKey = key.ToString();

        requestRefreshers.Clear();
        UIKit.Clear(requestList);
        string accent = UIKit.Hex(UIKit.AccentColor);
        foreach (CaptainRequests.Request request in Requests.Pending)
        {
            if (request.Tower == null) continue;
            RectTransform card = UIKit.Box(requestList, "Request");
            UIKit.Size(card, RequestWidth).minHeight = 32f;
            UIKit.Layout<HorizontalLayoutGroup>(card, 5, 4);
            UIKit.Size(UIKit.Label(card, $"<color={accent}>{request.Tower.Captain.displayName}</color>  \"{request.Text}\"", 12),
                flexibleWidth: 1f);
            AnswerButton(card, request, "No", RequestAnswer.No, NoColor, 40f);
            AnswerButton(card, request, "Some", RequestAnswer.Some, UIKit.ButtonColor, 50f);
            AnswerButton(card, request, "Yes", RequestAnswer.Yes, YesColor, 40f);
        }
    }

    void AnswerButton(Transform card, CaptainRequests.Request request, string text, RequestAnswer answer, Color color, float width)
    {
        Button button = UIKit.Button(card, text, () => Requests.Answer(request, answer), width, 22f);
        button.GetComponent<Image>().color = color;
    }
}
