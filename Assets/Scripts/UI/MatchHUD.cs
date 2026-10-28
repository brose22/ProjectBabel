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

    float Scale => Screen.height / ReferenceHeight;
    float ReferenceWidth => Screen.width / Scale;
    Rect TopBarRect => new(0f, 0f, ReferenceWidth, TopBarHeight);
    Rect SelectionRect
    {
        get
        {
            float height = 190f + SoldierSlotsHeight;
            return new(8f, ReferenceHeight - 8f - height, 300f, height);
        }
    }
    float SoldierSlotsHeight => placement.Selected != null ? placement.Selected.Soldiers.Count * SoldierSlotHeight : 0f;
    Rect EndRect => new(ReferenceWidth * 0.5f - 150f, ReferenceHeight * 0.5f - 85f, 300f, 170f);

    // Takes a screen position in Input System coordinates (origin bottom-left).
    public bool IsPointerOverHud(Vector2 screenPosition)
    {
        var gui = new Vector2(screenPosition.x / Scale, (Screen.height - screenPosition.y) / Scale);
        if (TopBarRect.Contains(gui)) return true;
        if (placement.Selected != null && SelectionRect.Contains(gui)) return true;
        return match.IsOver;
    }

    void OnGUI()
    {
        titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));

        DrawTopBar();
        if (placement.Selected != null) DrawSelection(placement.Selected);
        if (match.IsOver) DrawEndPanel();

        GUI.Label(new Rect(ReferenceWidth - 470f, ReferenceHeight - 24f, 465f, 22f),
            "Click: place / select    1-3: Gold draw    X: sell    Space: next wave    Esc: deselect");
    }

    void DrawTopBar()
    {
        RunState run = RunState.Current;
        GUILayout.BeginArea(TopBarRect, GUI.skin.box);
        GUILayout.BeginHorizontal();

        GUILayout.Label($"Health {run.Health}/{run.MaxHealth}", GUILayout.Width(95f));
        GUILayout.Label($"Gold {run.Gold}", GUILayout.Width(75f));
        GUILayout.Label($"Wave {Mathf.Min(match.WavesCleared + 1, match.WaveCount)}/{match.WaveCount}", GUILayout.Width(70f));

        string towers = $"Towers {Tower.All.Count}/{match.Data.maxTowers}";
        if (match.CanPlaceTower) towers += $"   Next: {match.NextCaptain.displayName}";
        GUILayout.Label(towers, GUILayout.Width(170f));
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

        GUILayout.Label($"{captain.displayName}  ({captain.element} {captain.role})");
        GUILayout.Label($"Power {tower.SoldierPower:0}   Range {captain.range:0.#}   Speed {captain.speed:0.##}");
        GUILayout.Label($"Reserve {tower.Reserve}/{captain.resourceCapacity}");
        GUILayout.Label($"Likes: {string.Join(", ", captain.likes)}");
        GUILayout.Label($"Dislikes: {string.Join(", ", captain.dislikes)}");
        DrawSoldierSlots(tower);

        GUILayout.Label("Gold draw");
        int rate = GUILayout.Toolbar((int)tower.DrawRate, DrawRateLabels);
        if (rate != (int)tower.DrawRate) tower.SetDrawRate((DrawRate)rate);

        if (GUILayout.Button("Sell (returns reserve)")) placement.SellSelected();

        GUILayout.EndArea();
    }

    static string SlotLabel(int slot) => slot switch
    {
        0 => "D",
        1 => "C",
        _ => $"Slot {slot + 1}",
    };

    // Per slot: rank, class and state, then one button per class. Other classes show the
    // reserve cost of switching and grey out when the reserve can't cover it.
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
                GUI.enabled = wasEnabled && tower.Reserve >= cost;
                if (GUILayout.Button($"{soldierClass.displayName} {cost}")) tower.SetSlotClass(slot, soldierClass);
                GUI.enabled = wasEnabled;
            }
            GUILayout.EndHorizontal();
        }
    }

    void DrawEndPanel()
    {
        GUILayout.BeginArea(EndRect, GUI.skin.box);
        GUILayout.Label(match.State == MatchState.Won ? "Victory" : "Defeat", titleStyle);
        GUILayout.Label($"Waves cleared {match.WavesCleared}/{match.WaveCount}");
        GUILayout.Label($"Kills {match.Kills}   Leaks {match.Leaks}   Casualties {match.Casualties}");
        GUILayout.Label($"Gold left {RunState.Current.Gold}");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Start a new run")) match.RestartRun();
        GUILayout.EndArea();
    }
}
