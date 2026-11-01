using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The match HUD, built in uGUI at runtime: top bar, party bar, captain widget, move hint, captain
// requests, approval toasts and controls hint (RunScreens shows results). Labels and buttons
// refresh every frame; the captain widget and request cards rebuild only when their structure changes.
public class MatchHUD : MonoBehaviour
{
    const float TopBarHeight = 32f;
    // Left of the request cards, kept clear for the cheat window and captain debug panel.
    const float LeftColumnWidth = 310f;
    const float RequestWidth = UIKit.ReferenceWidth - LeftColumnWidth - ApprovalToasts.Width - 4 * UIKit.Pad;
    static readonly Color NoColor = new(0.5f, 0.2f, 0.2f, 1f);
    static readonly Color YesColor = new(0.22f, 0.44f, 0.25f, 1f);
    static readonly Color HintColor = new(0.06f, 0.07f, 0.1f, 0.85f);

    [SerializeField] MatchManager match;
    [SerializeField] PlacementController placement;

    Relationships relationships;
    CaptainWidget captainWidget;
    PartyBar partyBar;
    CaptainDebugPanel debugPanel;
    CaptainRequests requests;
    Canvas canvas;

    readonly List<Action> barRefreshers = new();
    readonly List<Action> requestRefreshers = new();

    RectTransform requestList;
    RectTransform moveHintBox;
    Text moveHint;
    ApprovalToasts toasts;
    string requestKey;
    RectTransform pauseMenu;

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

        // Between the left column and the approval toasts, so none of them overlap.
        requestList = UIKit.Group(canvas.transform, "Requests");
        UIKit.Place(requestList, new Vector2(0f, 1f), new Vector2(LeftColumnWidth + 2 * UIKit.Pad, -TopBarHeight - 36f), new Vector2(RequestWidth, 0f));
        UIKit.Layout<VerticalLayoutGroup>(requestList, 0, UIKit.Gap, fitContent: true);

        toasts = new ApprovalToasts(canvas.transform, TopBarHeight + 8f);

        partyBar = new PartyBar(canvas.transform, match);
        BuildControlsHint();
        captainWidget = new CaptainWidget(canvas, TopBarHeight + UIKit.Pad, match, placement);
        // The full captain panel is a testing tool, kept out of release builds.
        if (Debug.isDebugBuild) debugPanel = new CaptainDebugPanel(canvas.transform, match, placement);
        // Last, so it draws over everything else on the canvas.
        BuildPauseMenu();
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        // The run setup screen covers everything; the HUD returns once the run begins.
        canvas.enabled = !match.IsBetweenMatches;
        UIKit.Refresh(barRefreshers);
        partyBar.Update();
        captainWidget.Update();
        debugPanel?.Update();
        UpdateRequests();
        UpdateMoveHint();
        UpdatePauseMenu();
        toasts.Update(Relationships);
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // Keys in the accent colour, each followed by what it does.
    void BuildControlsHint()
    {
        RectTransform box = UIKit.Box(canvas.transform, "Controls", HintColor, blocksClicks: false);
        // Above the party bar, which can run the width of the screen.
        UIKit.Place(box, new Vector2(1f, 0f), new Vector2(-UIKit.Pad, UIKit.Pad + PartyBar.Height + UIKit.Gap), Vector2.zero);
        UIKit.Layout<HorizontalLayoutGroup>(box, 5, 0, fitContent: true);
        string key = UIKit.Hex(UIKit.AccentColor);
        var parts = new List<string>();
        foreach ((string keys, string action) in new[]
                 {
                     ("Click", "place / select"), ("M", "move"), ("X", "sell"), ("Space", "next wave"), ("Wheel", "zoom"), ("Mid drag", "pan"),
                 })
            parts.Add($"<color={key}>{keys}</color> {action}");
        UIKit.Label(box, string.Join("   ", parts), 11, TextAnchor.MiddleRight, UIKit.TextColor);
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

        UIKit.Primary(UIKit.LiveButton(bar, WaveButtonLabel, match.StartNextWave, barRefreshers,
            interactable: () => match.State == MatchState.Preparing, width: 130f), barRefreshers);
        UIKit.LiveButton(bar, () => match.GameSpeed > 1f ? "Speed x2" : "Speed x1", match.ToggleGameSpeed, barRefreshers,
            selected: () => match.GameSpeed > 1f, width: 72f);
        UIKit.LiveButton(bar, () => "Recenter", match.RecenterCamera, barRefreshers,
            interactable: () => match.CameraMoved, width: 72f);
        UIKit.LiveButton(bar, () => "Pause", () => match.SetPaused(true), barRefreshers,
            interactable: () => !match.IsOver && !match.IsBetweenMatches, width: 60f);
    }

    // ---------- Pause menu ----------

    // A dimmed overlay with the menu window; it blocks clicks to the match behind it.
    void BuildPauseMenu()
    {
        pauseMenu = UIKit.Stretch(UIKit.Panel(canvas.transform, "Pause Menu", new Color(0f, 0f, 0f, 0.55f)));
        RectTransform window = UIKit.Box(pauseMenu, "Window");
        UIKit.Place(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 0f));
        UIKit.Layout<VerticalLayoutGroup>(window, 14, 8, fitContent: true);
        UIKit.Label(window, "PAUSED", 22, TextAnchor.MiddleCenter);
        UIKit.Size(UIKit.Primary(UIKit.Button(window, "Resume", () => match.SetPaused(false)), barRefreshers), height: 32f, flexibleWidth: 1f);
        UIKit.Size(UIKit.Button(window, "Exit Match", match.ExitMatch), height: 32f, flexibleWidth: 1f);
        UIKit.Size(UIKit.Button(window, "Exit Game", MainMenu.Quit), height: 32f, flexibleWidth: 1f);
        pauseMenu.gameObject.SetActive(false);
    }

    void UpdatePauseMenu()
    {
        if (pauseMenu == null) return;
        // The match can end under the menu (a request timing out is paused too, but be safe).
        if (match.IsPaused && (match.IsOver || match.IsBetweenMatches)) match.SetPaused(false);
        Keyboard keyboard = Keyboard.current;
        if (match.IsPaused && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) match.SetPaused(false);
        pauseMenu.gameObject.SetActive(match.IsPaused);
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

    // Only the request being asked is shown, with the time left to answer it; the match is paused
    // until then. Any others wait their turn.
    void UpdateRequests()
    {
        if (Requests == null) return;
        CaptainRequests.Request request = match.IsOver ? null : Requests.Active;
        string key = request == null ? "" : request.GetHashCode().ToString();
        if (key == requestKey)
        {
            UIKit.Refresh(requestRefreshers);
            return;
        }
        requestKey = key;

        requestRefreshers.Clear();
        UIKit.Clear(requestList);
        if (request == null || request.Tower == null) return;
        // Deselecting closes the captain widget, so it doesn't sit over the question.
        placement.Select(null);
        string accent = UIKit.Hex(UIKit.AccentColor);
        RectTransform card = UIKit.Box(requestList, "Request");
        UIKit.Size(card, RequestWidth);
        UIKit.Layout<VerticalLayoutGroup>(card, 5, 4);

        RectTransform row = UIKit.Group(card, "Ask");
        UIKit.Size(row, flexibleWidth: 1f).minHeight = 24f;
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 4);
        int waiting = Requests.Pending.Count - 1;
        string more = waiting > 0 ? $"  <color={accent}>(+{waiting} more)</color>" : "";
        UIKit.Size(UIKit.Label(row, $"<color={accent}>{request.Tower.Captain.displayName}</color>  \"{request.Text}\"{more}", 12),
            flexibleWidth: 1f);
        AnswerButton(row, request, "No", RequestAnswer.No, NoColor, 40f);
        AnswerButton(row, request, "Yes", RequestAnswer.Yes, YesColor, 40f);

        UIKit.LiveBar(card, () => Requests.SecondsLeft / CaptainRequests.ResponseSeconds,
            () => $"{(GameSettings.PauseDuringRequests ? "Paused - " : "")}{Mathf.CeilToInt(Requests.SecondsLeft)}s to answer", UIKit.ReserveBarColor, requestRefreshers, 12f);
    }

    void AnswerButton(Transform card, CaptainRequests.Request request, string text, RequestAnswer answer, Color color, float width)
    {
        Button button = UIKit.Button(card, text, () => Requests.Answer(request, answer), width, 22f);
        button.GetComponent<Image>().color = color;
    }
}
