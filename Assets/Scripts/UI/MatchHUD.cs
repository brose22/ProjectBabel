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
    static readonly string[] DrawRateLabels = { "Low", "Normal", "High" };
    static readonly SupportEffect[] StatEffects = { SupportEffect.ActSpeed, SupportEffect.Damage, SupportEffect.Defense };

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
    Text moveHint;
    Text messages;
    string towerPanelKey;
    string requestKey;

    // Added to the match at runtime, so they are looked up rather than wired in the scene.
    Relationships Relationships => relationships != null ? relationships : relationships = match.GetComponent<Relationships>();
    CaptainRequests Requests => requests != null ? requests : requests = match.GetComponent<CaptainRequests>();

    // Takes a screen position in Input System coordinates; kept for PlacementController.
    public bool IsPointerOverHud(Vector2 screenPosition) => match.IsOver || match.State == MatchState.Setup || UIKit.IsPointerOverUI();

    void Start()
    {
        canvas = UIKit.CreateCanvas("Match HUD", 10);
        BuildTopBar();

        towerPanel = UIKit.Panel(canvas.transform, "Tower Panel", UIKit.PanelColor);
        UIKit.Place(towerPanel, new Vector2(0f, 0f), new Vector2(8f, 8f), new Vector2(PanelWidth, 0f));
        UIKit.Layout<VerticalLayoutGroup>(towerPanel, 8, 3, fitContent: true);

        moveHint = UIKit.Label(canvas.transform, "", 13, TextAnchor.MiddleCenter, UIKit.SelectedButtonColor);
        UIKit.Place(moveHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -TopBarHeight - 6f), new Vector2(520f, 22f));

        requestList = UIKit.Group(canvas.transform, "Requests");
        UIKit.Place(requestList, new Vector2(0.5f, 1f), new Vector2(0f, -TopBarHeight - 30f), new Vector2(600f, 0f));
        UIKit.Layout<VerticalLayoutGroup>(requestList, 0, 4, fitContent: true);

        messages = UIKit.Label(canvas.transform, "", 12, TextAnchor.UpperRight);
        UIKit.Place(messages.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, -TopBarHeight - 6f), new Vector2(330f, 120f));

        Text hint = UIKit.Label(canvas.transform,
            "Click: place / select    Tab: next captain    Q: ability    1-3: Gold draw    M: move    X: sell    Space: next wave",
            11, TextAnchor.LowerRight, UIKit.MutedTextColor);
        UIKit.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-8f, 6f), new Vector2(640f, 18f));
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        // The run setup screen covers everything; the HUD returns once the run begins.
        canvas.enabled = match.State != MatchState.Setup;
        UIKit.Refresh(barRefreshers);
        UpdateTowerPanel();
        UpdateRequests();
        UpdateMoveHint();
        messages.text = Relationships != null ? string.Join("\n", Relationships.RecentMessages) : "";
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // ---------- Top bar ----------

    void BuildTopBar()
    {
        RectTransform bar = UIKit.Panel(canvas.transform, "Top Bar", UIKit.PanelColor);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = Vector2.one;
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0f, TopBarHeight);
        UIKit.Layout<HorizontalLayoutGroup>(bar, 4, 10);

        RunState run() => RunState.Current;
        UIKit.Size(UIKit.LiveLabel(bar, () => $"Health {run().Health}/{run().MaxHealth}", barRefreshers), 90f);
        UIKit.Size(UIKit.LiveLabel(bar, () => $"Gold {run().Gold}", barRefreshers), 80f);
        UIKit.Size(UIKit.LiveLabel(bar, () => $"Wave {Mathf.Min(match.WavesCleared + 1, match.WaveCount)}/{match.WaveCount}", barRefreshers), 64f);
        UIKit.Size(UIKit.LiveLabel(bar, () => $"Towers {Tower.All.Count}/{match.Data.maxTowers}", barRefreshers), 66f);
        UIKit.LiveButton(bar, NextCaptainLabel, match.CycleNextCaptain, barRefreshers,
            interactable: () => match.CanPlaceTower, width: 200f);
        UIKit.Size(UIKit.LiveLabel(bar, () => $"Kills {match.Kills}  Leaks {match.Leaks}  Casualties {match.Casualties}",
            barRefreshers, 12, color: UIKit.MutedTextColor), flexibleWidth: 1f);
        UIKit.LiveButton(bar, WaveButtonLabel, match.StartNextWave, barRefreshers,
            interactable: () => match.State == MatchState.Preparing, width: 120f);
        UIKit.LiveButton(bar, () => match.GameSpeed > 1f ? "Speed x2" : "Speed x1", match.ToggleGameSpeed, barRefreshers, width: 76f);
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

        UIKit.LiveLabel(panel, () => $"{captain.displayName}  Lv {tower.Level}  Bond {BondMath.RankName(tower.BondRank)} ({tower.Approval:0})",
            panelRefreshers, 15);
        UIKit.Label(panel, $"{captain.element} {captain.role}", 12, color: UIKit.MutedTextColor);
        UIKit.LiveLabel(panel, () => $"Power {tower.SoldierPower:0}   Range {captain.range:0.#}   Speed {captain.speed:0.##}", panelRefreshers);
        UIKit.LiveLabel(panel, () => $"Reserve {tower.Reserve}/{captain.resourceCapacity}   " +
            (tower.IsMaxLevel ? $"XP {tower.Xp} (max level)" : $"XP {tower.Xp}/{tower.XpForNextLevel}"), panelRefreshers);
        UIKit.LiveLabel(panel, () => BoostLabel(tower), panelRefreshers, 12);
        UIKit.Label(panel, $"Likes: {string.Join(", ", captain.likes)}", 12, color: UIKit.MutedTextColor);
        UIKit.Label(panel, $"Dislikes: {string.Join(", ", captain.dislikes)}", 12, color: UIKit.MutedTextColor);

        BuildCondition(panel, tower);
        BuildAbility(panel, tower);
        BuildSoldierSlots(panel, tower);

        UIKit.Label(panel, "Gold draw", 12, color: UIKit.MutedTextColor);
        Transform drawRow = Row(panel);
        for (int i = 0; i < DrawRateLabels.Length; i++)
        {
            var rate = (DrawRate)i;
            UIKit.LiveButton(drawRow, () => DrawRateLabels[(int)rate], () => tower.SetDrawRate(rate), panelRefreshers,
                selected: () => tower.DrawRate == rate, width: 96f);
        }

        Transform actions = Row(panel);
        if (placement.IsMoving)
            UIKit.Button(actions, "Cancel move", placement.CancelMove, 145f);
        else
            UIKit.LiveButton(actions, () => match.MoveCost(tower) > 0 ? $"Move ({match.MoveCost(tower)} Gold)" : "Move (free)",
                placement.BeginMove, panelRefreshers, () => match.CanMove(tower) && match.CanAffordMove(tower), width: 145f);
        UIKit.Button(actions, "Sell (returns reserve)", placement.SellSelected, 145f);
    }

    static Transform Row(Transform parent)
    {
        RectTransform row = UIKit.Group(parent, "Row");
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 4);
        return row;
    }

    void BuildCondition(Transform panel, Tower tower)
    {
        if (!tower.IsDisabled)
        {
            UIKit.LiveLabel(panel, () => $"Health {Mathf.CeilToInt(tower.Health)}/{Mathf.CeilToInt(tower.MaxHealth)}", panelRefreshers);
            return;
        }
        if (!match.CanRestore(tower))
        {
            UIKit.Label(panel, $"Disabled (restorable after wave {tower.RestorableAfterWave})", 13, color: UIKit.SelectedButtonColor);
            return;
        }
        UIKit.Label(panel, "Disabled", 13, color: UIKit.SelectedButtonColor);
        UIKit.LiveButton(panel, () => $"Restore ({match.RestoreCost(tower)} Gold)", () => match.TryRestoreTower(tower), panelRefreshers,
            () => RunState.Current.Gold >= match.RestoreCost(tower));
    }

    // Meter percentage plus a Use button that only lights up when the ability is ready and usable.
    void BuildAbility(Transform panel, Tower tower)
    {
        AbilityData ability = tower.Captain.ability;
        if (ability == null)
        {
            UIKit.Label(panel, "No special ability", 13, color: UIKit.MutedTextColor);
            return;
        }
        Transform row = Row(panel);
        UIKit.Size(UIKit.LiveLabel(row, () => $"{ability.displayName}  {Mathf.FloorToInt(tower.SpecialCharge * 100f)}%", panelRefreshers),
            flexibleWidth: 1f);
        UIKit.LiveButton(row, () => "Use (Q)", () => tower.TryUseAbility(), panelRefreshers, () => tower.CanUseAbility, width: 80f);
        if (!string.IsNullOrEmpty(ability.description))
            UIKit.Label(panel, ability.description, 12, color: UIKit.MutedTextColor);
    }

    // Slots 3 and 4 open at bond ranks B and S (GDD).
    static string SlotLabel(int slot) => slot switch
    {
        2 => "Slot 3 (B)",
        3 => "Slot 4 (S)",
        _ => $"Slot {slot + 1}",
    };

    // Per slot: its class and state, then a button per class. Other classes show the reserve cost
    // of switching and grey out when the reserve can't cover it or the tower is disabled.
    void BuildSoldierSlots(Transform panel, Tower tower)
    {
        var classes = match.Data.ChoosableClasses;
        for (int i = 0; i < tower.Soldiers.Count; i++)
        {
            int slot = i;
            UIKit.LiveLabel(panel, () =>
            {
                Soldier soldier = tower.Soldiers[slot];
                return $"{SlotLabel(slot)}: {soldier.Class.displayName} ({(soldier.IsDead ? "dead" : "alive")})";
            }, panelRefreshers, 12);
            Transform row = Row(panel);
            foreach (SoldierClassData soldierClass in classes)
            {
                if (soldierClass == null) continue;
                SoldierClassData option = soldierClass;
                UIKit.LiveButton(row,
                    () => tower.Soldiers[slot].Class == option ? option.displayName : $"{option.displayName} {tower.RespawnCostOf(option)}",
                    () => tower.SetSlotClass(slot, option), panelRefreshers,
                    () => tower.Soldiers[slot].Class == option || (!tower.IsDisabled && tower.Reserve >= tower.RespawnCostOf(option)),
                    () => tower.Soldiers[slot].Class == option, width: 70f, height: 22f);
            }
        }
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
        if (!placement.IsMoving || tower == null)
        {
            moveHint.text = "";
            return;
        }
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
        foreach (CaptainRequests.Request request in Requests.Pending)
        {
            if (request.Tower == null) continue;
            RectTransform card = UIKit.Panel(requestList, "Request", UIKit.PanelColor);
            UIKit.Size(card, 600f, 30f);
            UIKit.Layout<HorizontalLayoutGroup>(card, 4, 6);
            UIKit.Size(UIKit.Label(card, $"{request.Tower.Captain.displayName}: \"{request.Text}\""), flexibleWidth: 1f);
            UIKit.Button(card, "No", () => Requests.Answer(request, RequestAnswer.No), 44f);
            UIKit.Button(card, "Some", () => Requests.Answer(request, RequestAnswer.Some), 52f);
            UIKit.Button(card, "Yes", () => Requests.Answer(request, RequestAnswer.Yes), 44f);
        }
    }

}
