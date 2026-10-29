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
    const float SlotLabelWidth = 52f;
    static readonly string[] DrawRateLabels = { "Low", "Normal", "High" };
    static readonly SupportEffect[] StatEffects = { SupportEffect.ActSpeed, SupportEffect.Damage, SupportEffect.Defense };
    static readonly Color NoColor = new(0.5f, 0.2f, 0.2f, 1f);
    static readonly Color YesColor = new(0.22f, 0.44f, 0.25f, 1f);
    static readonly Color HintColor = new(0.06f, 0.07f, 0.1f, 0.85f);

    [SerializeField] MatchManager match;
    [SerializeField] PlacementController placement;

    Relationships relationships;
    CaptainRequests requests;
    Canvas canvas;

    readonly List<Action> barRefreshers = new();
    readonly List<Action> panelRefreshers = new();
    readonly List<Action> requestRefreshers = new();

    RectTransform towerPanel;
    RectTransform requestList;
    RectTransform moveHintBox;
    Text moveHint;
    Text messages;
    string towerPanelKey;
    string requestKey;

    // Added to the match at runtime, so they are looked up rather than wired in the scene.
    Relationships Relationships => relationships != null ? relationships : relationships = match.GetComponent<Relationships>();
    CaptainRequests Requests => requests != null ? requests : requests = match.GetComponent<CaptainRequests>();

    // Takes a screen position in Input System coordinates; kept for PlacementController.
    public bool IsPointerOverHud(Vector2 screenPosition) => match.IsOver || match.IsBetweenMatches || UIKit.IsPointerOverUI();

    void Start()
    {
        canvas = UIKit.CreateCanvas("Match HUD", 10);
        BuildTopBar();

        towerPanel = UIKit.Box(canvas.transform, "Tower Panel");
        UIKit.Place(towerPanel, new Vector2(0f, 0f), new Vector2(UIKit.Pad, UIKit.Pad), new Vector2(PanelWidth, 0f));
        UIKit.Layout<VerticalLayoutGroup>(towerPanel, UIKit.Pad, 3, fitContent: true);

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
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        // The run setup screen covers everything; the HUD returns once the run begins.
        canvas.enabled = !match.IsBetweenMatches;
        UIKit.Refresh(barRefreshers);
        UpdateTowerPanel();
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

    // ---------- Tower panel ----------

    // The panel's structure depends on these; anything else changes through refreshers.
    string TowerPanelKey(Tower tower)
    {
        if (tower == null) return "";
        return $"{tower.GetHashCode()}|{tower.Soldiers.Count}|{tower.IsDisabled}|{match.CanRestore(tower)}|{placement.IsMoving}";
    }

    void UpdateTowerPanel()
    {
        Tower tower = placement.Selected;
        string key = TowerPanelKey(tower);
        if (key != towerPanelKey)
        {
            towerPanelKey = key;
            panelRefreshers.Clear();
            UIKit.Clear(towerPanel);
            towerPanel.gameObject.SetActive(tower != null);
            if (tower != null) BuildTowerPanel(tower);
        }
        if (tower != null) UIKit.Refresh(panelRefreshers);
    }

    void BuildTowerPanel(Tower tower)
    {
        CaptainData captain = tower.Captain;
        Transform panel = towerPanel;

        Transform title = Row(panel);
        Text name = UIKit.Label(title, captain.displayName, 16);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        UIKit.LiveLabel(title, () => $"Lv {tower.Level}", panelRefreshers, 14, TextAnchor.MiddleRight, UIKit.AccentColor);
        string element = $"<color={UIKit.Hex(UIKit.ElementColor(captain.element))}>{captain.element}</color>";
        UIKit.LiveLabel(panel, () => $"{element} {captain.role}   ·   Bond {BondMath.RankName(tower.BondRank)} ({tower.Approval:0})",
            panelRefreshers, 12, color: UIKit.MutedTextColor);

        BuildCondition(panel, tower);
        UIKit.LiveBar(panel, () => tower.Reserve / (float)Mathf.Max(1, captain.resourceCapacity),
            () => $"Reserve {tower.Reserve}/{captain.resourceCapacity}", UIKit.ReserveBarColor, panelRefreshers);
        UIKit.LiveBar(panel, () => tower.IsMaxLevel ? 1f : tower.Xp / (float)Mathf.Max(1, tower.XpForNextLevel),
            () => tower.IsMaxLevel ? $"XP {tower.Xp} (max level)" : $"XP {tower.Xp}/{tower.XpForNextLevel}", UIKit.XpBarColor, panelRefreshers);

        UIKit.LiveLabel(panel, () => $"Power {tower.SoldierPower:0}    Range {captain.range:0.#}    Speed {captain.speed:0.##}", panelRefreshers, 12);
        Text boosts = UIKit.LiveLabel(panel, () => BoostLabel(tower), panelRefreshers, 12);
        panelRefreshers.Add(() => boosts.color = HasBoosts(tower) ? UIKit.GoodColor : UIKit.MutedTextColor);
        TagLine(panel, "Likes", captain.likes, UIKit.GoodColor);
        TagLine(panel, "Dislikes", captain.dislikes, UIKit.BadColor);

        BuildAbility(panel, tower);
        BuildSoldierSlots(panel, tower);

        UIKit.Header(panel, "Gold draw");
        Transform drawRow = Row(panel);
        for (int i = 0; i < DrawRateLabels.Length; i++)
        {
            var rate = (DrawRate)i;
            Flexible(UIKit.LiveButton(drawRow, () => DrawRateLabels[(int)rate], () => tower.SetDrawRate(rate), panelRefreshers,
                selected: () => tower.DrawRate == rate, height: 22f));
        }

        UIKit.Size(UIKit.Group(panel, "Gap"), height: 2f);
        Transform actions = Row(panel);
        if (placement.IsMoving)
            Flexible(UIKit.Button(actions, "Cancel move", placement.CancelMove));
        else
            Flexible(UIKit.LiveButton(actions, () => match.MoveCost(tower) > 0 ? $"Move ({match.MoveCost(tower)} Gold)" : "Move (free)",
                placement.BeginMove, panelRefreshers, () => match.CanMove(tower) && match.CanAffordMove(tower)));
        Flexible(UIKit.Button(actions, "Sell (returns reserve)", placement.SellSelected));
    }

    // A row that spans the panel, for buttons that share its width.
    static Transform Row(Transform parent)
    {
        RectTransform row = UIKit.Group(parent, "Row");
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 3);
        UIKit.Size(row, flexibleWidth: 1f);
        return row;
    }

    static void Flexible(Button button, float height = 24f) => UIKit.Size(button, height: height, flexibleWidth: 1f);

    static void TagLine(Transform panel, string caption, CaptainTag[] tags, Color color) =>
        UIKit.Label(panel, $"<color={UIKit.Hex(color)}>{caption}</color>  {string.Join(", ", tags)}", 11, color: UIKit.MutedTextColor);

    void BuildCondition(Transform panel, Tower tower)
    {
        if (!tower.IsDisabled)
        {
            UIKit.LiveBar(panel, () => tower.Health / tower.MaxHealth,
                () => $"Health {Mathf.CeilToInt(tower.Health)}/{Mathf.CeilToInt(tower.MaxHealth)}", UIKit.HealthBarColor, panelRefreshers);
            return;
        }
        if (!match.CanRestore(tower))
        {
            UIKit.Label(panel, $"DISABLED  (restorable after wave {tower.RestorableAfterWave})", 12, color: UIKit.BadColor).fontStyle = FontStyle.Bold;
            return;
        }
        Transform row = Row(panel);
        Text disabled = UIKit.Label(row, "DISABLED", 12, color: UIKit.BadColor);
        disabled.fontStyle = FontStyle.Bold;
        UIKit.Size(disabled, flexibleWidth: 1f);
        UIKit.LiveButton(row, () => $"Restore ({match.RestoreCost(tower)} Gold)", () => match.TryRestoreTower(tower), panelRefreshers,
            () => RunState.Current.Gold >= match.RestoreCost(tower), width: 160f, height: 22f);
    }

    // Name and Use button, a charge meter, then what the special does.
    void BuildAbility(Transform panel, Tower tower)
    {
        UIKit.Header(panel, "Special");
        AbilityData ability = tower.Captain.ability;
        if (ability == null)
        {
            UIKit.Label(panel, "No special ability", 12, color: UIKit.MutedTextColor);
            return;
        }
        Transform row = Row(panel);
        Text name = UIKit.Label(row, ability.displayName, 13);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        UIKit.LiveButton(row, () => "Use (Q)", () => tower.TryUseAbility(), panelRefreshers, () => tower.CanUseAbility,
            selected: () => tower.CanUseAbility, width: 80f, height: 22f);
        UIKit.LiveBar(panel, () => tower.SpecialCharge,
            () => tower.IsSpecialReady ? "Ready" : $"Charge {Mathf.FloorToInt(tower.SpecialCharge * 100f)}%", UIKit.ChargeBarColor, panelRefreshers, 12f);
        if (!string.IsNullOrEmpty(ability.description))
            UIKit.Label(panel, ability.description, 11, color: UIKit.MutedTextColor);
    }

    // Slots 3 and 4 open at bond ranks B and S (GDD).
    static string SlotLabel(int slot) => slot switch
    {
        2 => "Slot 3 (B)",
        3 => "Slot 4 (S)",
        _ => $"Slot {slot + 1}",
    };

    // Per slot: its label (red when the soldier is down), then a button per class. The current
    // class is gold; others show the reserve cost of switching in gold and grey out when the
    // reserve can't cover it or the tower is disabled.
    void BuildSoldierSlots(Transform panel, Tower tower)
    {
        UIKit.Header(panel, "Soldiers");
        var classes = match.Data.ChoosableClasses;
        string cost = UIKit.Hex(UIKit.AccentColor), unaffordable = UIKit.Hex(UIKit.DisabledTextColor);
        for (int i = 0; i < tower.Soldiers.Count; i++)
        {
            int slot = i;
            Transform row = Row(panel);
            Text label = UIKit.LiveLabel(row, () => tower.Soldiers[slot].IsDead ? $"{SlotLabel(slot)}\n<size=9>down</size>" : SlotLabel(slot),
                panelRefreshers, 11);
            UIKit.Size(label, SlotLabelWidth);
            panelRefreshers.Add(() => label.color = tower.Soldiers[slot].IsDead ? UIKit.BadColor : UIKit.MutedTextColor);
            foreach (SoldierClassData soldierClass in classes)
            {
                if (soldierClass == null) continue;
                SoldierClassData option = soldierClass;
                bool current() => tower.Soldiers[slot].Class == option;
                bool affordable() => !tower.IsDisabled && tower.Reserve >= tower.RespawnCostOf(option);
                Button button = UIKit.LiveButton(row,
                    () => current() ? option.displayName
                        : $"{option.displayName} <size=9><color={(affordable() ? cost : unaffordable)}>{tower.RespawnCostOf(option)}g</color></size>",
                    () => tower.SetSlotClass(slot, option), panelRefreshers, () => current() || affordable(), current, height: 22f);
                Flexible(button, 22f);
                Text text = button.GetComponentInChildren<Text>();
                text.fontSize = 11;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.rectTransform.offsetMin = new Vector2(1f, text.rectTransform.offsetMin.y);
                text.rectTransform.offsetMax = new Vector2(-1f, 0f);
            }
        }
    }

    static bool HasBoosts(Tower tower)
    {
        foreach (SupportEffect effect in StatEffects)
            if (tower.Boost(effect) > 0f) return true;
        return false;
    }

    // Boosts every tower has from Buffer specials cast this wave.
    static string BoostLabel(Tower tower)
    {
        var parts = new List<string>();
        foreach (SupportEffect effect in StatEffects)
        {
            float boost = tower.Boost(effect);
            if (boost > 0f) parts.Add($"{effect} +{boost * 100f:0}%");
        }
        return parts.Count == 0 ? "Wave boosts: none" : "Wave boosts: " + string.Join(", ", parts);
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
