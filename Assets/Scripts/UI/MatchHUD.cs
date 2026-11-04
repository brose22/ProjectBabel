using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The match HUD, built in uGUI at runtime: top bar, party bar, captain widget, move hint, captain
// requests, approval toasts, next-wave preview and controls hint (RunScreens shows results). Labels
// and buttons refresh every frame; the captain widget and request cards rebuild only when their
// structure changes.
public class MatchHUD : MonoBehaviour
{
    const float TopBarHeight = 40f;
    const float RequestWidth = 440f;
    // Seconds a new request ignores answers, so a button already being pressed for the map can't
    // answer a question that just appeared.
    const float AnswerLockSeconds = 0.35f;
    // Seconds the health figure stays red after an enemy leaks.
    const float LeakFlashSeconds = 0.6f;
    // The wave button pulses over the last seconds of the countdown.
    const float PulseSeconds = 3f;
    static readonly Color NoColor = new(0.5f, 0.2f, 0.2f, 1f);
    static readonly Color YesColor = new(0.22f, 0.44f, 0.25f, 1f);
    static readonly Color HintColor = new(0.06f, 0.07f, 0.1f, 0.85f);
    static readonly Color WaveProgressColor = new(0.3f, 0.55f, 0.35f, 0.95f);

    static readonly string[] HeartRows =
    {
        ".XX.XX.",
        "XXXXXXX",
        "XXXXXXX",
        ".XXXXX.",
        "..XXX..",
        "...X...",
    };

    static readonly string[] CoinRows =
    {
        "..XXX..",
        ".XXXXX.",
        "XXXDXXX",
        "XXXDXXX",
        "XXXDXXX",
        ".XXXXX.",
        "..XXX..",
    };

    static Sprite heartSprite, coinSprite;

    [SerializeField] MatchManager match;
    [SerializeField] PlacementController placement;

    Relationships relationships;
    CaptainWidget captainWidget;
    PartyBar partyBar;
    CaptainCycler captainCycler;
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
    float answersFrom;
    RectTransform pauseMenu;
    RectTransform statsPanel;
    RectTransform wavePreview;
    string previewKey;
    Text controlsLabel;
    RectTransform controlsIcons;
    string controlsIconsKey;
    bool hintOpen;
    float canvasWidth;
    int lastLeaks;
    float leakFlashUntil;

    // Added to the match at runtime, so they are looked up rather than wired in the scene.
    Relationships Relationships => relationships != null ? relationships : relationships = match.GetComponent<Relationships>();
    CaptainRequests Requests => requests != null ? requests : requests = match.GetComponent<CaptainRequests>();

    // Takes a screen position in Input System coordinates; kept for PlacementController.
    public bool IsPointerOverHud(Vector2 screenPosition) => match.IsOver || match.IsBetweenMatches || UIKit.IsPointerOverUI();

    // Gold draw is set by captain requests; only the debug panel lets the player set it directly.
    public bool IsDebugPanelOpen => debugPanel != null && debugPanel.IsOpen;

    void Start()
    {
        canvas = UIKit.CreateCanvas("Match HUD", 10, GameSettings.UiScale);
        canvasWidth = UIKit.ReferenceWidth / GameSettings.UiScale;
        // The controls hint shows for the first few matches, then folds away until Tab opens it.
        GameSettings.CountMatchStarted();
        hintOpen = GameSettings.MatchesStarted <= GameSettings.HintMatches;
        BuildTopBar();

        moveHintBox = UIKit.Box(canvas.transform, "Move Hint", HintColor, blocksClicks: false);
        UIKit.Place(moveHintBox, new Vector2(0.5f, 1f), new Vector2(0f, -TopBarHeight - 6f), Vector2.zero);
        UIKit.Layout<HorizontalLayoutGroup>(moveHintBox, 6, 0, fitContent: true);
        moveHint = UIKit.Shadowed(UIKit.Label(moveHintBox, "", 13, TextAnchor.MiddleCenter, UIKit.AccentColor));

        // Top centre, under the move hint: the match is held while a request is shown, so it gets the room.
        requestList = UIKit.Group(canvas.transform, "Requests");
        UIKit.Place(requestList, new Vector2(0.5f, 1f), new Vector2(0f, -TopBarHeight - 40f),
            new Vector2(Mathf.Min(RequestWidth, canvasWidth - 2 * UIKit.Pad), 0f));
        UIKit.Layout<VerticalLayoutGroup>(requestList, 0, UIKit.Gap, fitContent: true);

        wavePreview = UIKit.Box(canvas.transform, "Next Wave", HintColor, blocksClicks: false);
        UIKit.Place(wavePreview, new Vector2(0f, 1f), new Vector2(UIKit.Pad, -TopBarHeight - UIKit.Gap), Vector2.zero);
        UIKit.Layout<HorizontalLayoutGroup>(wavePreview, 4, 4, fitContent: true);
        wavePreview.gameObject.SetActive(false);

        toasts = new ApprovalToasts(canvas.transform, TopBarHeight + 8f);
        // After the toasts, so the stats list draws over them.
        BuildStatsPanel();

        partyBar = new PartyBar(canvas.transform, match);
        BuildControlsHint();
        captainWidget = new CaptainWidget(canvas, TopBarHeight + UIKit.Pad, match, placement);
        // The full captain panel is a testing tool, kept out of release builds.
        captainCycler = new CaptainCycler(canvas, match, placement);
        if (Debug.isDebugBuild) debugPanel = new CaptainDebugPanel(canvas.transform, match, placement);
        // Last, so it draws over everything else on the canvas.
        BuildPauseMenu();
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        // The run setup screen covers everything; the HUD returns once the run begins.
        canvas.enabled = !match.IsBetweenMatches;
        UpdateKeys();
        UIKit.Refresh(barRefreshers);
        UpdateWavePreview();
        UpdateControlsHint();
        partyBar.Update();
        captainCycler.Update();
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

    void BuildControlsHint()
    {
        RectTransform box = UIKit.Box(canvas.transform, "Controls", HintColor, blocksClicks: false);
        // Above the party bar, which can run the width of the screen.
        UIKit.Place(box, new Vector2(1f, 0f), new Vector2(-UIKit.Pad, UIKit.Pad + PartyBar.Height + UIKit.Gap), Vector2.zero);
        UIKit.Layout<HorizontalLayoutGroup>(box, 5, 0, fitContent: true);
        controlsLabel = UIKit.Label(box, "", 12, TextAnchor.MiddleRight, UIKit.TextColor);
        controlsIcons = UIKit.Group(box, "Gamepad");
        UIKit.Layout<HorizontalLayoutGroup>(controlsIcons, 0, 3);
    }

    // Keys in the accent colour, each followed by what it does. Only the keys that apply now are
    // listed: a selected tower adds move and sell, the pause between waves adds starting the next.
    void UpdateControlsHint()
    {
        bool pad = GameInput.UsingGamepad;
        controlsLabel.gameObject.SetActive(!pad);
        controlsIcons.gameObject.SetActive(pad);
        if (pad)
        {
            UpdateGamepadHint();
            return;
        }

        var keys = new List<(string, string)>();
        if (hintOpen)
        {
            if (placement.Selected != null) keys.Add(("M", "move")); else keys.Add(("Click", "place / select"));
            keys.Add(("Wheel/Q/E", "captain"));
            if (placement.Selected != null) keys.Add(("X", "sell"));
            if (match.State == MatchState.Preparing) keys.Add(("Space", "start wave"));
            keys.Add(("F", "speed"));
            keys.Add(("Ctrl+Wheel", "zoom"));
            keys.Add(("Mid drag", "pan"));
            keys.Add(("Tab", "hide"));
        }
        else keys.Add(("Tab", "controls"));

        string accent = UIKit.Hex(UIKit.AccentColor);
        var parts = new List<string>();
        foreach ((string key, string action) in keys) parts.Add($"<color={accent}>{key}</color> {action}");
        string text = string.Join("   ", parts);
        if (controlsLabel.text != text) controlsLabel.text = text;
    }

    // The same hint for the gamepad, with button icons, rebuilt only when what it lists changes.
    void UpdateGamepadHint()
    {
        List<(PadGlyph[], string)> hints = GamepadHints();
        var parts = new List<string> { GameInput.PadFamily.ToString() };
        foreach ((PadGlyph[] glyphs, string action) in hints) parts.Add(string.Join("+", glyphs) + action);
        string key = string.Join("|", parts);
        if (key == controlsIconsKey) return;
        controlsIconsKey = key;

        UIKit.Clear(controlsIcons);
        foreach ((PadGlyph[] glyphs, string action) in hints)
        {
            foreach (PadGlyph glyph in glyphs) UIKit.Icon(controlsIcons, PadIcons.Get(glyph), PadIcons.Size);
            UIKit.Label(controlsIcons, action, 12);
            UIKit.Size(UIKit.Group(controlsIcons, "Gap"), 5f);
        }
    }

    List<(PadGlyph[], string)> GamepadHints()
    {
        var hints = new List<(PadGlyph[], string)>();
        void Add(string action, params PadGlyph[] glyphs) => hints.Add((glyphs, action));
        if (!hintOpen)
        {
            Add("controls", PadGlyph.LeftStickPress);
            return hints;
        }
        if (placement.IsMoving)
        {
            Add("move here", PadGlyph.South);
            Add("cancel", PadGlyph.East);
        }
        else if (placement.WidgetFocused) Add("back to map", PadGlyph.East);
        else if (placement.Selected != null)
        {
            Add("actions", PadGlyph.South);
            Add("deselect", PadGlyph.East);
        }
        else Add("place / select", PadGlyph.South);
        if (!placement.IsMoving && !placement.WidgetFocused) Add("captain", PadGlyph.LeftShoulder, PadGlyph.RightShoulder);
        Add("pan", PadGlyph.RightStick);
        Add("zoom", PadGlyph.LeftTrigger, PadGlyph.RightStick);
        Add("hide", PadGlyph.LeftStickPress);
        return hints;
    }

    // Tab folds the controls hint, F steps the game speed, Y and N answer a captain's request, with
    // the gamepad's equivalents, and the pause button opens and closes the pause menu.
    void UpdateKeys()
    {
        if (match.IsBetweenMatches) return;
        if (GameInput.ToggleHint) hintOpen = !hintOpen;
        if (match.IsPaused)
        {
            if (GameInput.Resume) match.SetPaused(false);
            return;
        }
        if (match.IsOver) return;
        if (GameInput.Pause)
        {
            match.SetPaused(true);
            return;
        }
        if (GameInput.CycleSpeed) match.CycleGameSpeed();
        CaptainRequests.Request request = Requests != null ? Requests.Active : null;
        if (request == null || Time.unscaledTime < answersFrom) return;
        if (GameInput.AnswerYes) Requests.Answer(request, RequestAnswer.Yes);
        else if (GameInput.AnswerNo) Requests.Answer(request, RequestAnswer.No);
    }

    // ---------- Next wave preview ----------

    // The enemy types of the wave about to start, shown during the pause before it. Rebuilt only
    // when the wave changes.
    void UpdateWavePreview()
    {
        bool show = match.State == MatchState.Preparing && (Requests == null || Requests.Active == null);
        List<EnemyData> types = show ? match.NextWaveEnemies() : null;
        show = show && types.Count > 0;
        wavePreview.gameObject.SetActive(show);
        if (!show) return;

        string key = $"{match.WavesCleared}/{match.Boss == null}";
        if (key == previewKey) return;
        previewKey = key;
        UIKit.Clear(wavePreview);
        UIKit.Label(wavePreview, "NEXT WAVE", 10, color: UIKit.AccentColor).fontStyle = FontStyle.Bold;
        foreach (EnemyData enemy in types)
        {
            Sprite art = enemy.portrait != null ? enemy.portrait : enemy.sprite;
            if (art == null) continue;
            Image icon = UIKit.Icon(wavePreview, art, 32f);
            icon.raycastTarget = true;
            RectTransform tip = UIKit.HoverTip(icon.rectTransform, enemy.displayName, null, 140f);
            WeaknessIcons.Row(tip, enemy, match.NextWaveElement(enemy));
        }
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

        // Only what the player acts on stays on the bar; the rest is behind the Stats button.
        heartSprite ??= UIKit.PixelSprite(HeartRows, UIKit.BadColor, UIKit.BadColor);
        coinSprite ??= UIKit.PixelSprite(CoinRows, UIKit.AccentColor, new Color(0.7f, 0.5f, 0.15f, 1f));
        RunState run() => RunState.Current;
        Text health = Stat(bar, "HEALTH", () => $"{run().Health}/{run().MaxHealth}", heartSprite);
        lastLeaks = match.Leaks;
        barRefreshers.Add(() =>
        {
            // A leak flashes the figure red even while the health is still high.
            if (match.Leaks != lastLeaks)
            {
                lastLeaks = match.Leaks;
                leakFlashUntil = Time.unscaledTime + LeakFlashSeconds;
            }
            bool low = run().Health * 3 <= run().MaxHealth;
            health.color = low || Time.unscaledTime < leakFlashUntil ? UIKit.BadColor : UIKit.TextColor;
        });
        Stat(bar, "GOLD", () => $"{run().Gold}", coinSprite).color = UIKit.AccentColor;
        Stat(bar, "WAVE", () => $"{Mathf.Min(match.WavesCleared + 1, match.WaveCount)}/{match.WaveCount}");
        Stat(bar, "TOWERS", () => $"{Tower.All.Count}/{match.Data.maxTowers}");
        UIKit.Size(UIKit.Group(bar, "Spacer"), flexibleWidth: 1f);

        const float buttonHeight = 28f;
        Button wave = UIKit.Primary(UIKit.LiveButton(bar, WaveButtonLabel, match.StartNextWave, barRefreshers,
            interactable: () => match.State == MatchState.Preparing, width: 160f, height: buttonHeight), barRefreshers);
        BuildWaveProgress(wave);
        UIKit.PadBadge(wave, PadGlyph.North, () => GameInput.PadInMatch);
        Button speed = UIKit.LiveButton(bar, () => $"Speed x{match.GameSpeed:0.#}", match.CycleGameSpeed, barRefreshers,
            selected: () => match.GameSpeed > 1f, width: 88f, height: buttonHeight);
        UIKit.PadBadge(speed, PadGlyph.View, () => GameInput.PadInMatch);
        // Only offered once the camera has been moved, so it takes no room otherwise.
        Button recenter = UIKit.LiveButton(bar, () => "Recenter", match.RecenterCamera, barRefreshers, width: 86f, height: buttonHeight);
        barRefreshers.Add(() => recenter.gameObject.SetActive(match.CameraMoved));
        UIKit.PadBadge(recenter, PadGlyph.RightStickPress, () => GameInput.PadInMatch);
        UIKit.LiveButton(bar, () => "Stats", () => statsPanel.gameObject.SetActive(!statsPanel.gameObject.activeSelf), barRefreshers,
            selected: () => statsPanel != null && statsPanel.gameObject.activeSelf, width: 56f, height: buttonHeight);
        Button pause = UIKit.LiveButton(bar, () => "Pause", () => match.SetPaused(true), barRefreshers,
            interactable: () => !match.IsOver && !match.IsBetweenMatches, width: 60f, height: buttonHeight);
        UIKit.PadBadge(pause, PadGlyph.Menu, () => GameInput.PadInMatch);
    }

    // A fill behind the wave button's label that follows the enemies killed or leaked this wave,
    // and a pulse over the last seconds of the countdown.
    void BuildWaveProgress(Button button)
    {
        var image = button.GetComponent<Image>();
        Text label = button.GetComponentInChildren<Text>();
        RectTransform fill = UIKit.Panel(button.transform, "Progress", WaveProgressColor, blocksClicks: false);
        fill.SetAsFirstSibling();
        fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        fill.anchorMin = Vector2.zero;
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = new Vector2(4f, 6f);
        fill.offsetMax = new Vector2(-4f, -4f);
        barRefreshers.Add(() =>
        {
            bool active = match.State == MatchState.WaveActive && match.WaveEnemyTotal > 0 && match.WavesCleared < match.WaveCount;
            float amount = active ? (float)match.WaveEnemiesResolved / match.WaveEnemyTotal : 0f;
            fill.anchorMax = new Vector2(amount, 1f);
            fill.gameObject.SetActive(amount > 0f);
            if (match.State == MatchState.WaveActive) label.color = UIKit.TextColor;

            float left = match.NextWaveCountdown;
            if (button.interactable && left > 0f && left <= PulseSeconds)
                image.color = Color.Lerp(UIKit.SelectedButtonColor, Color.white, 0.2f + 0.2f * Mathf.Sin(Time.unscaledTime * 10f));
        });
    }

    // The figures that matter less moment to moment, in a list the Stats button opens.
    void BuildStatsPanel()
    {
        statsPanel = UIKit.Box(canvas.transform, "Stats");
        UIKit.Place(statsPanel, new Vector2(1f, 1f), new Vector2(-UIKit.Pad, -TopBarHeight - UIKit.Gap), new Vector2(220f, 0f));
        UIKit.Layout<VerticalLayoutGroup>(statsPanel, UIKit.Pad, 2, fitContent: true);
        DetailRows(statsPanel);
        statsPanel.gameObject.SetActive(false);
    }

    void DetailRows(Transform parent)
    {
        DetailRow(parent, "City", CityLabel);
        DetailRow(parent, "Kills", () => $"{match.Kills}");
        DetailRow(parent, "Leaks", () => $"{match.Leaks}");
        DetailRow(parent, "Casualties", () => $"{match.Casualties}");
    }

    void DetailRow(Transform parent, string caption, Func<string> value)
    {
        RectTransform row = UIKit.Group(parent, caption);
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 6);
        UIKit.Size(UIKit.Label(row, caption, 12, color: UIKit.MutedTextColor), flexibleWidth: 1f);
        UIKit.LiveLabel(row, value, barRefreshers, 13, TextAnchor.MiddleRight);
    }

    // ---------- Pause menu ----------

    // A popup over the match that blocks clicks to it. It is built once and shown while paused; a
    // click on the backdrop or the gamepad's cancel button resumes.
    void BuildPauseMenu()
    {
        ModalWindow modal = UIKit.Modal(canvas.transform, "Pause Menu", 260f, new Vector2(0.5f, 0.5f), Vector2.zero,
            () => match.SetPaused(false));
        pauseMenu = modal.Backdrop;
        RectTransform window = modal.Window;
        UIKit.Label(window, "PAUSED", 22, TextAnchor.MiddleCenter);
        // Here as well as behind the Stats button, which the gamepad can't reach during play.
        DetailRows(window);
        Button resume = UIKit.Primary(UIKit.Button(window, "Resume", () => match.SetPaused(false)), barRefreshers);
        UIKit.Size(resume, height: 32f, flexibleWidth: 1f);
        UIKit.PadBadge(resume, PadGlyph.Menu);
        UIKit.Size(UIKit.Button(window, "Exit Match", match.ExitMatch), height: 32f, flexibleWidth: 1f);
        UIKit.Size(UIKit.Button(window, "Exit Game", MainMenu.Quit), height: 32f, flexibleWidth: 1f);
        pauseMenu.gameObject.SetActive(false);
    }

    void UpdatePauseMenu()
    {
        if (pauseMenu == null) return;
        // The match can end under the menu (a request timing out is paused too, but be safe).
        if (match.IsPaused && (match.IsOver || match.IsBetweenMatches)) match.SetPaused(false);
        pauseMenu.gameObject.SetActive(match.IsPaused);
    }

    // A small caption over a live value, with an icon to the left when given one.
    Text Stat(Transform bar, string caption, Func<string> value, Sprite icon = null)
    {
        RectTransform stat = UIKit.Group(bar, caption);
        UIKit.Layout<HorizontalLayoutGroup>(stat, 0, 5);
        if (icon != null) UIKit.Icon(stat, icon, 14f);
        RectTransform text = UIKit.Group(stat, "Text");
        UIKit.Layout<VerticalLayoutGroup>(text, 0, -2f);
        UIKit.Label(text, caption, 10, color: UIKit.MutedTextColor).fontStyle = FontStyle.Bold;
        return UIKit.LiveLabel(text, value, barRefreshers, 15);
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
        if (match.State == MatchState.WaveActive)
        {
            if (match.WavesCleared >= match.WaveCount) return "Defeat the boss";
            return match.WaveEnemyTotal > 0 ? $"Wave {match.WavesCleared + 1}: {match.WaveEnemiesResolved}/{match.WaveEnemyTotal}" : "Wave in progress";
        }
        if (match.State != MatchState.Preparing) return "Match over";
        return match.NextWaveCountdown > 0f ? $"Next wave ({Mathf.CeilToInt(match.NextWaveCountdown)})" : "Start wave";
    }

    void UpdateMoveHint()
    {
        Tower tower = placement.Selected;
        bool moving = placement.IsMoving && tower != null;
        moveHintBox.gameObject.SetActive(moving);
        if (!moving) return;
        string how = GameInput.UsingGamepad ? $"{GameInput.ConfirmName} on a free tile ({GameInput.CancelName} to cancel)"
            : "click a free tile (Esc / right-click to cancel)";
        moveHint.text = $"Moving {tower.Captain.displayName}: {how}" +
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
        answersFrom = Time.unscaledTime + AnswerLockSeconds;
        // Deselecting closes the captain widget, so it doesn't sit over the question.
        placement.Select(null);
        string accent = UIKit.Hex(UIKit.AccentColor);
        RectTransform card = UIKit.Box(requestList, "Request");
        UIKit.Layout<VerticalLayoutGroup>(card, UIKit.Pad, 6);

        // Who is asking and what: a portrait beside the name and the quote.
        RectTransform ask = UIKit.Group(card, "Ask");
        UIKit.Layout<HorizontalLayoutGroup>(ask, 0, UIKit.Pad).childAlignment = TextAnchor.UpperLeft;
        Captain captain = request.Tower.Captain;
        UIKit.Icon(ask, captain.Portrait != null ? captain.Portrait : captain.Sprite, 48f);
        RectTransform words = UIKit.Group(ask, "Words");
        UIKit.Size(words, flexibleWidth: 1f);
        UIKit.Layout<VerticalLayoutGroup>(words, 0, 2);
        int waiting = Requests.Pending.Count - 1;
        string more = waiting > 0 ? $"  <color={accent}>(+{waiting} more)</color>" : "";
        UIKit.Label(words, $"{captain.displayName}{more}", 14, color: UIKit.AccentColor).fontStyle = FontStyle.Bold;
        UIKit.Label(words, $"\"{request.Text}\"", 13);

        // What each answer does, in two columns so the choice can be made without remembering the rules.
        RectTransform columns = UIKit.Group(card, "Answers");
        UIKit.Layout<HorizontalLayoutGroup>(columns, 0, UIKit.Pad).childAlignment = TextAnchor.UpperLeft;
        UIKit.Size(UIKit.Group(columns, "Yes"), flexibleWidth: 1f);
        UIKit.Size(UIKit.Group(columns, "No"), flexibleWidth: 1f);
        string good = UIKit.Hex(UIKit.GoodColor), bad = UIKit.Hex(UIKit.BadColor);
        Transform yes = columns.Find("Yes"), no = columns.Find("No");
        UIKit.Layout<VerticalLayoutGroup>((RectTransform)yes, 0, 2);
        UIKit.Layout<VerticalLayoutGroup>((RectTransform)no, 0, 2);
        UIKit.LiveLabel(yes, () =>
        {
            RequestEvaluation eval = Requests.Evaluate(request);
            var text = new System.Text.StringBuilder($"<b>Yes</b>: {YesEffect(request)}\n<color={good}>{Signed(eval.ApprovalYes)} approval</color>");
            foreach (RequestEffect pro in eval.Pros) text.Append($"\n<color={good}>+ {pro.Text}</color>");
            foreach (RequestEffect con in eval.Cons) text.Append($"\n<color={bad}>- {con.Text}</color>");
            if (eval.IsBad) text.Append($"\n<color={bad}><b>Costs more than it gives</b></color>");
            return text.ToString();
        }, requestRefreshers, 12, color: UIKit.MutedTextColor);
        UIKit.LiveLabel(no, () => $"<b>No</b>: nothing changes\n<color={bad}>{Signed(Requests.ApprovalIfNo(request))} approval</color>",
            requestRefreshers, 12, color: UIKit.MutedTextColor);

        RectTransform buttons = UIKit.Group(card, "Answers");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, UIKit.Pad);
        UIKit.Size(UIKit.Group(buttons, "Spacer"), flexibleWidth: 1f);
        AnswerButton(buttons, request, "No", "N", PadGlyph.East, RequestAnswer.No, NoColor);
        AnswerButton(buttons, request, "Yes", "Y", PadGlyph.South, RequestAnswer.Yes, YesColor);

        UIKit.LiveBar(card, () => Requests.SecondsLeft / CaptainRequests.ResponseSeconds,
            () => $"{(GameSettings.PauseDuringRequests ? "Paused - " : "")}{Mathf.CeilToInt(Requests.SecondsLeft)}s to answer", UIKit.ReserveBarColor, requestRefreshers, 14f);
    }

    static string Signed(float amount) => amount > 0f ? $"+{amount:0.#}" : $"{amount:0.#}";

    // What a Yes does, by kind of request.
    static string YesEffect(CaptainRequests.Request request) => request.Kind switch
    {
        RequestKind.MoreDraw => "draw set to High",
        RequestKind.LowerDraw => "draw set to Low, captain keeps half the reserve",
        RequestKind.Relocate => "moves closer to the gate, free",
        RequestKind.UseSpecial => $"uses {request.Tower.Captain.Ability.displayName} now",
        RequestKind.Retreat => "falls back away from the gate, free",
        RequestKind.Reinforce => request.Fallen != null ? $"moves in beside {request.Fallen.Captain.displayName}, free" : "moves in to cover, free",
        _ => "granted",
    };

    // Shows its key in the label, or the gamepad button's icon beside it.
    void AnswerButton(Transform card, CaptainRequests.Request request, string text, string key, PadGlyph glyph, RequestAnswer answer, Color color)
    {
        Button button = UIKit.Button(card, "", () => Requests.Answer(request, answer), 100f, 30f);
        button.GetComponent<Image>().color = color;
        Text label = button.GetComponentInChildren<Text>();
        Func<string> caption = () => GameInput.UsingGamepad ? text : $"{text}  [{key}]";
        label.text = caption();
        requestRefreshers.Add(() => label.text = caption());
        UIKit.PadBadge(button, glyph);
    }
}
