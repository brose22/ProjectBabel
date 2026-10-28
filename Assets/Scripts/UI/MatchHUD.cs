using UnityEngine;

// Grey-box HUD drawn with IMGUI. It is meant to be replaced by real UI once the loop plays well.
// All layout is in a 540-pixel-tall reference space that is scaled to the screen.
public class MatchHUD : MonoBehaviour
{
    const float ReferenceHeight = 540f;
    const float TopBarHeight = 34f;
    // One label row plus one button row per soldier slot in the selection panel.
    const float SoldierSlotHeight = 48f;
    static readonly string[] DrawRateLabels = { "Low", "Normal", "High" };

    [SerializeField] MatchManager match;
    [SerializeField] PlacementController placement;

    GUIStyle titleStyle;
    Relationships relationships;
    CaptainRequests requests;

    float Scale => Screen.height / ReferenceHeight;
    float ReferenceWidth => Screen.width / Scale;
    Rect TopBarRect => new(0f, 0f, ReferenceWidth, TopBarHeight);
    Rect SelectionRect
    {
        get
        {
            float height = 310f + SoldierSlotsHeight;
            return new(8f, ReferenceHeight - 8f - height, 300f, height);
        }
    }
    float SoldierSlotsHeight => placement.Selected != null ? placement.Selected.Soldiers.Count * SoldierSlotHeight : 0f;
    Rect EndRect => new(ReferenceWidth * 0.5f - 150f, ReferenceHeight * 0.5f - 100f, 300f, 200f);

    // Takes a screen position in Input System coordinates (origin bottom-left).
    public bool IsPointerOverHud(Vector2 screenPosition)
    {
        var gui = new Vector2(screenPosition.x / Scale, (Screen.height - screenPosition.y) / Scale);
        if (TopBarRect.Contains(gui)) return true;
        if (placement.Selected != null && SelectionRect.Contains(gui)) return true;
        if (Requests != null)
            for (int i = 0; i < Requests.Pending.Count; i++)
                if (RequestRect(i).Contains(gui)) return true;
        return match.IsOver;
    }

    void OnGUI()
    {
        titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));

        DrawTopBar();
        if (placement.Selected != null) DrawSelection(placement.Selected);
        if (placement.IsMoving && placement.Selected != null) DrawMoveHint(placement.Selected);
        DrawApprovalMessages();
        DrawRequests();
        if (match.IsOver) DrawEndPanel();

        GUI.Label(new Rect(ReferenceWidth - 655f, ReferenceHeight - 24f, 650f, 22f),
            "Click: place / select    Tab: next captain    Q: ability    1-3: Gold draw    M: move    X: sell    Space: next wave");
    }

    static readonly SupportEffect[] StatEffects = { SupportEffect.ActSpeed, SupportEffect.Damage, SupportEffect.Defense };

    // Total boosts on the tower's soldiers right now (aura plus timed), with time left on timed ones.
    static string BoostLabel(Tower tower)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (SupportEffect effect in StatEffects)
        {
            float boost = tower.Boost(effect);
            if (boost <= 0f) continue;
            float secondsLeft = tower.TimedBoostSecondsLeft(effect);
            parts.Add($"{effect} +{boost * 100f:0}%" + (secondsLeft > 0f ? $" ({Mathf.CeilToInt(secondsLeft)}s)" : ""));
        }
        return parts.Count == 0 ? "Boosts: none" : "Boosts: " + string.Join(", ", parts);
    }

    // Shows the live boost during a wave, otherwise what the aura will do when one starts.
    static string AuraLabel(Tower tower)
    {
        CaptainData captain = tower.Captain;
        switch (captain.aura)
        {
            case SupportEffect.None:
                return "Aura: none";
            case SupportEffect.Heal:
                return $"Aura: heals soldiers {captain.auraStrength:0.#}/s" + (tower.IsAuraActive ? "" : " in waves");
            default:
                return tower.IsAuraActive
                    ? $"Aura: {captain.aura} +{tower.Boost(captain.aura) * 100f:0}% (fading)"
                    : $"Aura: {captain.aura} +{captain.auraStrength * 100f:0}% at wave start";
        }
    }

    void DrawTopBar()
    {
        RunState run = RunState.Current;
        GUILayout.BeginArea(TopBarRect, GUI.skin.box);
        GUILayout.BeginHorizontal();

        GUILayout.Label($"Health {run.Health}/{run.MaxHealth}", GUILayout.Width(95f));
        GUILayout.Label($"Gold {run.Gold}", GUILayout.Width(75f));
        GUILayout.Label($"Wave {Mathf.Min(match.WavesCleared + 1, match.WaveCount)}/{match.WaveCount}", GUILayout.Width(70f));

        GUILayout.Label($"Towers {Tower.All.Count}/{match.Data.maxTowers}", GUILayout.Width(70f));
        if (match.CanPlaceTower)
        {
            CaptainData next = match.NextCaptain;
            if (GUILayout.Button($"Next: {next.displayName} ({next.role})  [Tab]", GUILayout.Width(190f)))
                match.CycleNextCaptain();
        }
        GUILayout.Label($"Kills {match.Kills}   Leaks {match.Leaks}   Casualties {match.Casualties}");

        GUILayout.FlexibleSpace();
        if (match.State == MatchState.Preparing)
        {
            string label = match.NextWaveCountdown > 0f
                ? $"Next wave ({Mathf.CeilToInt(match.NextWaveCountdown)})"
                : "Start wave";
            if (GUILayout.Button(label, GUILayout.Width(120f))) match.StartNextWave();
        }
        if (GUILayout.Button(match.GameSpeed > 1f ? "Speed x2" : "Speed x1", GUILayout.Width(75f)))
            match.ToggleGameSpeed();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DrawSelection(Tower tower)
    {
        CaptainData captain = tower.Captain;
        GUILayout.BeginArea(SelectionRect, GUI.skin.box);

        GUILayout.Label($"{captain.displayName}  Lv {tower.Level}  Bond {BondMath.RankName(tower.BondRank)} ({tower.Approval:0})  ({captain.element} {captain.role})");
        GUILayout.Label($"Power {tower.SoldierPower:0}   Range {captain.range:0.#}   Speed {captain.speed:0.##}");
        GUILayout.Label($"Reserve {tower.Reserve}/{captain.resourceCapacity}   " +
                        (tower.IsMaxLevel ? $"XP {tower.Xp} (max level)" : $"XP {tower.Xp}/{tower.XpForNextLevel}"));
        GUILayout.Label(AuraLabel(tower));
        GUILayout.Label(BoostLabel(tower));
        GUILayout.Label($"Likes: {string.Join(", ", captain.likes)}");
        GUILayout.Label($"Dislikes: {string.Join(", ", captain.dislikes)}");
        DrawTowerCondition(tower);
        DrawAbility(tower);
        DrawSoldierSlots(tower);

        GUILayout.Label("Gold draw");
        int rate = GUILayout.Toolbar((int)tower.DrawRate, DrawRateLabels);
        if (rate != (int)tower.DrawRate) tower.SetDrawRate((DrawRate)rate);

        GUILayout.BeginHorizontal();
        DrawMoveButton(tower);
        if (GUILayout.Button("Sell (returns reserve)")) placement.SellSelected();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    // Slots 3 and 4 open at bond ranks B and S (GDD).
    static string SlotLabel(int slot) => slot switch
    {
        2 => "Slot 3 (B)",
        3 => "Slot 4 (S)",
        _ => $"Slot {slot + 1}",
    };

    // Per slot: rank, class and state, then one button per class. Other classes show the
    // reserve cost of switching and grey out when the reserve can't cover it or the tower is disabled.
    void DrawSoldierSlots(Tower tower)
    {
        var classes = match.Data.ChoosableClasses;
        for (int slot = 0; slot < tower.Soldiers.Count; slot++)
        {
            Soldier soldier = tower.Soldiers[slot];
            GUILayout.Label($"{SlotLabel(slot)}: {soldier.Class.displayName} ({(soldier.IsDead ? "dead" : "alive")})");

            GUILayout.BeginHorizontal();
            foreach (SoldierClassData soldierClass in classes)
            {
                if (soldierClass == null) continue;
                if (soldierClass == soldier.Class)
                {
                    GUILayout.Toggle(true, soldierClass.displayName, GUI.skin.button);
                    continue;
                }
                int cost = tower.RespawnCostOf(soldierClass);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && !tower.IsDisabled && tower.Reserve >= cost;
                if (GUILayout.Button($"{soldierClass.displayName} {cost}")) tower.SetSlotClass(slot, soldierClass);
                GUI.enabled = wasEnabled;
            }
            GUILayout.EndHorizontal();
        }
    }

    // Meter percentage plus a Use button that only lights up when the ability is ready and has a target.
    void DrawAbility(Tower tower)
    {
        AbilityData ability = tower.Captain.ability;
        GUILayout.BeginHorizontal();
        if (ability == null)
        {
            GUILayout.Label("No special ability");
        }
        else
        {
            GUILayout.Label($"{ability.displayName}  {Mathf.FloorToInt(tower.SpecialCharge * 100f)}%");
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && tower.CanUseAbility;
            if (GUILayout.Button("Use (Q)", GUILayout.Width(80f))) tower.TryUseAbility();
            GUI.enabled = wasEnabled;
        }
        GUILayout.EndHorizontal();
    }

    void DrawTowerCondition(Tower tower)
    {
        if (!tower.IsDisabled)
        {
            GUILayout.Label($"Health {Mathf.CeilToInt(tower.Health)}/{Mathf.CeilToInt(tower.MaxHealth)}");
            return;
        }

        if (!match.CanRestore(tower))
        {
            GUILayout.Label($"Disabled (restorable after wave {tower.RestorableAfterWave})");
            return;
        }

        GUILayout.Label("Disabled");
        int cost = match.RestoreCost(tower);
        GUI.enabled = RunState.Current.Gold >= cost;
        if (GUILayout.Button($"Restore ({cost} Gold)")) match.TryRestoreTower(tower);
        GUI.enabled = true;
    }

    void DrawMoveButton(Tower tower)
    {
        if (placement.IsMoving)
        {
            if (GUILayout.Button("Cancel move")) placement.CancelMove();
            return;
        }

        int cost = match.MoveCost(tower);
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && match.CanMove(tower) && match.CanAffordMove(tower);
        if (GUILayout.Button(cost > 0 ? $"Move ({cost} Gold)" : "Move (free)")) placement.BeginMove();
        GUI.enabled = wasEnabled;
    }

    // Purely informational (a box takes no clicks), so it stays out of IsPointerOverHud and
    // tiles under it remain clickable.
    void DrawMoveHint(Tower tower)
    {
        string hint = $"Moving {tower.Captain.displayName}: click a free tile (Esc / right-click to cancel)";
        if (!match.CanAffordMove(tower)) hint += "  - not enough Gold";
        GUI.Box(new Rect(ReferenceWidth * 0.5f - 230f, TopBarHeight + 6f, 460f, 24f), hint);
    }

    // Added to the match at runtime, so it is looked up rather than wired in the scene.
    Relationships Relationships => relationships != null ? relationships : relationships = match.GetComponent<Relationships>();
    CaptainRequests Requests => requests != null ? requests : requests = match.GetComponent<CaptainRequests>();

    // Request cards sit below the move hint, top centre.
    Rect RequestRect(int index) => new(ReferenceWidth * 0.5f - 280f, TopBarHeight + 34f + index * 30f, 560f, 28f);

    void DrawRequests()
    {
        if (Requests == null) return;
        // Answering removes the request, so draw from a copy.
        var snapshot = new System.Collections.Generic.List<CaptainRequests.Request>(Requests.Pending);
        for (int i = 0; i < snapshot.Count; i++)
        {
            CaptainRequests.Request request = snapshot[i];
            if (request.Tower == null) continue;
            GUILayout.BeginArea(RequestRect(i), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{request.Tower.Captain.displayName}: \"{request.Text}\"");
            if (GUILayout.Button("No", GUILayout.Width(44f))) Requests.Answer(request, RequestAnswer.No);
            if (GUILayout.Button("Some", GUILayout.Width(52f))) Requests.Answer(request, RequestAnswer.Some);
            if (GUILayout.Button("Yes", GUILayout.Width(44f))) Requests.Answer(request, RequestAnswer.Yes);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }

    // Recent approval changes, top right under the bar. Labels only, so clicks pass through.
    void DrawApprovalMessages()
    {
        if (Relationships == null) return;
        float y = TopBarHeight + 4f;
        foreach (string message in Relationships.RecentMessages)
        {
            GUI.Label(new Rect(ReferenceWidth - 320f, y, 315f, 20f), message);
            y += 18f;
        }
    }

    void DrawEndPanel()
    {
        GUILayout.BeginArea(EndRect, GUI.skin.box);
        GUILayout.Label(match.State == MatchState.Won ? "Victory" : "Defeat", titleStyle);
        GUILayout.Label($"Waves cleared {match.WavesCleared}/{match.WaveCount}");
        GUILayout.Label($"Kills {match.Kills}   Leaks {match.Leaks}   Casualties {match.Casualties}");
        GUILayout.Label($"Gold left {RunState.Current.Gold}");
        if (Relationships != null && Relationships.Walkouts.Count > 0)
            GUILayout.Label("Walked out for this run: " + string.Join(", ", Relationships.Walkouts.ConvertAll(c => c.displayName)));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Start a new run")) match.RestartRun();
        GUILayout.EndArea();
    }
}
