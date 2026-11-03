using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen the game starts on: start or resume a run, start a new game, change settings, or quit. Built in
// uGUI at runtime like the run screens; the menu scene only holds a camera and this component.
public class MainMenu : MonoBehaviour
{
    public const string SceneName = "MainMenu";
    public const string MatchSceneName = "Match";

    static readonly Color BackgroundColor = new(0.05f, 0.06f, 0.09f, 1f);

    [Tooltip("Where the AI Run screen finds the maps and captains to pick from.")]
    [SerializeField] MatchData data;

    Canvas canvas;
    RectTransform screen;
    readonly List<Action> refreshers = new();

    // Leaves the match for the menu. The run stays saved as it was; the menu offers to resume it.
    // The scene only changes at the end of the frame, so the run is cleared once the menu loads
    // (Awake) rather than here, where the match HUD would still read it this frame.
    public static void Open() => SceneManager.LoadScene(SceneName);

    // Resume reloads the profile and run from their saves, so nothing from the match lingers in memory.
    void Awake()
    {
        RunState.Clear();
        Profile.Clear();
    }

    void Start()
    {
        canvas = UIKit.CreateCanvas("Main Menu", 0);
        BuildMain();
    }

    void LateUpdate() => UIKit.Refresh(refreshers);

    // Every screen but the first goes back to it on the gamepad's cancel button.
    RectTransform NewScreen(string name, float width)
    {
        refreshers.Clear();
        if (screen != null) Destroy(screen.gameObject);
        screen = UIKit.Stretch(UIKit.Panel(canvas.transform, name, BackgroundColor));
        UIKit.Focus(screen, name == "Main" ? null : BuildMain);
        RectTransform column = UIKit.Group(screen, "Column");
        UIKit.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 10, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;
        return column;
    }

    // ---------- Main ----------

    void BuildMain()
    {
        RectTransform column = NewScreen("Main", 400f);
        UIKit.Label(column, "PROJECT BABEL", 36, TextAnchor.MiddleCenter);
        UIKit.Label(column, "A roguelite tower defense", 14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        UIKit.Size(UIKit.Group(column, "Gap"), height: 16f);

        UIKit.Button(column, "Start run", () => { if (RunSave.Exists) BuildConfirmNewRun(); else StartRun(); }, 240f, 34f);
        UIKit.LiveButton(column, () => "Resume run", ResumeRun, refreshers, () => RunSave.Exists, width: 240f, height: 34f);
        UIKit.Button(column, "New game", () => { if (ProfileSave.Exists) BuildConfirmNewGame(); else NewGame(); }, 240f, 34f);
        UIKit.Button(column, "AI Run", BuildAiRun, 240f, 34f);
        UIKit.Button(column, "Settings", BuildSettings, 240f, 34f);
        UIKit.Button(column, "Exit", Quit, 240f, 34f);
    }

    // ---------- AI Run ----------

    static readonly float[] AiSpeeds = { 1f, 2f, 4f, 8f };
    int aiRuns = 1;
    float aiSpeed = 4f;

    // The role of each party row of a picked map; a captain of each is rolled when the run starts.
    // Static with the map and layer, so the choices are still set when an AI run comes back to the menu.
    static readonly CaptainRole[] aiPartyRoles = { CaptainRole.Knight, CaptainRole.Healer, CaptainRole.Archer };
    static readonly CaptainRole[] AllRoles = (CaptainRole[])Enum.GetValues(typeof(CaptainRole));
    // The picked layout, or -1 for a full run.
    static int aiMap = -1;
    static int aiLayer;
    // How many captains the AI fields; 0 until first shown, then the game's party size.
    static int aiCaptains;
    // The bond rank the AI's captains start at, 0 (D) to 4 (S).
    static int aiBond;

    // The AI plays by itself with its own saves, then writes a report to Reports/.
    void BuildAiRun()
    {
        RectTransform column = NewScreen("AI Run", 520f);
        UIKit.Label(column, "AI RUN", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, "The AI plays on its own, using separate saves, and writes a report to the project's Reports folder when it is done.",
            14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        // Picking a map changes which rows there are, so the screen rebuilds.
        SettingRow(column, "Map", () => aiMap >= 0 ? data.layouts[aiMap].displayName : "Full run (random maps)", step =>
        {
            aiMap = Wrap(aiMap + 1 + step, data.layouts.Count + 1) - 1;
            BuildAiRun();
        });
        if (aiCaptains <= 0) aiCaptains = data.partySize;
        if (aiMap >= 0) BuildAiMatchRows(column);
        SettingRow(column, "Captains", () => aiCaptains.ToString(),
            step => aiCaptains = Mathf.Clamp(aiCaptains + step, 1, data.maxTowers));
        SettingRow(column, "Bond", () => BondMath.RankName(aiBond),
            step => aiBond = Mathf.Clamp(aiBond + step, 0, BondMath.MaxRank));
        SettingRow(column, "Runs", () => aiRuns.ToString(), step => aiRuns = Mathf.Clamp(aiRuns + step, 1, 50));
        SettingRow(column, "Speed", () => $"{aiSpeed:0}x",
            step => aiSpeed = AiSpeeds[Wrap(Array.IndexOf(AiSpeeds, aiSpeed) + step, AiSpeeds.Length)]);
        RectTransform buttons = UIKit.Group(column, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 10, fitContent: true);
        UIKit.Button(buttons, "Start", StartAiRun, 180f, 32f);
        UIKit.PadBadge(UIKit.Button(buttons, "Back", BuildMain, 140f, 32f), PadGlyph.East);
    }

    void BuildAiMatchRows(RectTransform column)
    {
        int layers = Mathf.Max(1, data.matchesPerRun - 1);
        SettingRow(column, "Layer", () => $"{aiLayer + 1} of {layers}", step => aiLayer = Wrap(aiLayer + step, layers));
        for (int i = 0; i < aiPartyRoles.Length; i++)
        {
            int row = i;
            SettingRow(column, $"Captain {row + 1}", () => aiPartyRoles[row].ToString(), step =>
                aiPartyRoles[row] = AllRoles[Wrap(Array.IndexOf(AllRoles, aiPartyRoles[row]) + step, AllRoles.Length)]);
        }
        SettingsNote(column, "One match on this map, with no world map, shops or recruits. Enemies get tougher with each layer.");
    }

    void StartAiRun()
    {
        int captains = aiCaptains > 0 ? aiCaptains : data.partySize;
        var options = new AiOptions { runs = aiRuns, speed = aiSpeed, seed = Environment.TickCount & 0x7fffff, layout = aiMap, layer = aiLayer, captains = captains, bondRank = aiBond };
        if (aiMap >= 0)
        {
            // The picked roles first, then every role in turn to fill a bigger party; a smaller one drops the last.
            options.partyRoles.AddRange(aiPartyRoles);
            for (int turn = 0; options.partyRoles.Count < captains; turn++)
                options.partyRoles.Add(AllRoles[turn % AllRoles.Length]);
            if (options.partyRoles.Count > captains) options.partyRoles.RemoveRange(captains, options.partyRoles.Count - captains);
        }
        AiPlayer.Begin(options);
    }

    void BuildConfirmNewRun()
    {
        RectTransform column = NewScreen("Confirm", 460f);
        UIKit.Label(column, "START A NEW RUN?", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, "Your run in progress will be lost. Your captains, soldiers and Gold stay.", 14,
            TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        RectTransform buttons = UIKit.Group(column, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 10, fitContent: true);
        UIKit.Button(buttons, "Start new run", StartRun, 180f, 32f);
        UIKit.PadBadge(UIKit.Button(buttons, "Cancel", BuildMain, 140f, 32f), PadGlyph.East);
    }

    void BuildConfirmNewGame()
    {
        RectTransform column = NewScreen("Confirm", 460f);
        UIKit.Label(column, "START A NEW GAME?", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, "Your captains, soldiers, Gold and any run in progress will be lost.", 14,
            TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        RectTransform buttons = UIKit.Group(column, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 10, fitContent: true);
        UIKit.Button(buttons, "Start new game", NewGame, 180f, 32f);
        UIKit.PadBadge(UIKit.Button(buttons, "Cancel", BuildMain, 140f, 32f), PadGlyph.East);
    }

    // A new run on the saved profile; a new profile is made if there is none.
    static void StartRun()
    {
        RunSave.Delete();
        RunState.Clear();
        SceneManager.LoadScene(MatchSceneName);
    }

    // Throws the profile away with the run, so the next run starts a new one.
    static void NewGame()
    {
        ProfileSave.Delete();
        Profile.Clear();
        StartRun();
    }

    static void ResumeRun()
    {
        RunState.Clear();
        SceneManager.LoadScene(MatchSceneName);
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Settings ----------

    enum SettingsTab { Graphics, Audio, Gameplay }

    SettingsTab settingsTab;

    void BuildSettings()
    {
        RectTransform column = NewScreen("Settings", 520f);
        UIKit.Label(column, "SETTINGS", 24, TextAnchor.MiddleCenter);

        RectTransform tabs = UIKit.Group(column, "Tabs");
        UIKit.Layout<HorizontalLayoutGroup>(tabs, 0, 6, fitContent: true);
        foreach (SettingsTab tab in Enum.GetValues(typeof(SettingsTab)))
        {
            SettingsTab chosen = tab;
            UIKit.LiveButton(tabs, () => chosen.ToString(),
                () => { settingsTab = chosen; BuildSettings(); }, refreshers, selected: () => chosen == settingsTab, width: 120f, height: 30f);
        }

        // Each tab scrolls in a fixed-height window, so the Back button stays put.
        // The framed box is the window; the scrolling list fills it.
        RectTransform frame = UIKit.Box(column, "Options Window");
        LayoutElement window = UIKit.Size(frame, 520f, 330f);
        window.minHeight = 330f;
        window.flexibleHeight = 0f;
        UIKit.Layout<VerticalLayoutGroup>(frame, 8, 0);
        ScrollRect list = UIKit.ScrollList(frame, "Options", 10f);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        list.content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        RectTransform options = list.content;

        switch (settingsTab)
        {
            case SettingsTab.Graphics: BuildGraphicsTab(options); break;
            case SettingsTab.Audio: BuildAudioTab(options); break;
            default: BuildGameplayTab(options); break;
        }

        UIKit.PadBadge(UIKit.Button(column, "Back", BuildMain, 200f, 32f), PadGlyph.East);
    }

    void BuildGraphicsTab(RectTransform column)
    {
        SettingRow(column, "Resolution", () => $"{GameSettings.Resolution.x} x {GameSettings.Resolution.y}", step =>
        {
            List<Vector2Int> sizes = GameSettings.Resolutions;
            GameSettings.SetResolution(sizes[Wrap(sizes.IndexOf(GameSettings.Resolution) + step, sizes.Count)]);
        });
        SettingRow(column, "Window mode", () => GameSettings.WindowModeName(GameSettings.WindowMode), step =>
        {
            FullScreenMode[] modes = GameSettings.WindowModes;
            GameSettings.SetWindowMode(modes[Wrap(Array.IndexOf(modes, GameSettings.WindowMode) + step, modes.Length)]);
        });
        SettingRow(column, "VSync", () => GameSettings.VSync ? "On" : "Off", _ => GameSettings.SetVSync(!GameSettings.VSync));
        SettingRow(column, "Frame-rate cap",
            () => (GameSettings.FrameRateCap > 0 ? $"{GameSettings.FrameRateCap} fps" : "Unlimited") + (GameSettings.VSync ? " (VSync on)" : ""),
            step =>
            {
                int[] caps = GameSettings.FrameRateCaps;
                GameSettings.SetFrameRateCap(caps[Wrap(Array.IndexOf(caps, GameSettings.FrameRateCap) + step, caps.Length)]);
            });
        SettingRow(column, "Quality", () => QualitySettings.names[GameSettings.Quality],
            step => GameSettings.SetQuality(Wrap(GameSettings.Quality + step, QualitySettings.names.Length)));
        SettingRow(column, "Match HUD size", () => $"{Mathf.RoundToInt(GameSettings.UiScale * 100f)}%", step =>
        {
            float[] scales = GameSettings.UiScales;
            int current = 0;
            for (int i = 0; i < scales.Length; i++)
                if (Mathf.Abs(scales[i] - GameSettings.UiScale) < Mathf.Abs(scales[current] - GameSettings.UiScale)) current = i;
            GameSettings.SetUiScale(scales[Wrap(current + step, scales.Length)]);
        });
        SettingsNote(column, "Changes apply at once and are kept between launches. The HUD size applies from the next match. The editor's Game view ignores resolution and window mode.");
    }

    void BuildAudioTab(RectTransform column)
    {
        VolumeRow(column, "Music", AudioChannel.Music);
        VolumeRow(column, "Sound effects", AudioChannel.Sfx);
        VolumeRow(column, "Interface", AudioChannel.Ui);
        SettingsNote(column, "Changes apply at once and are kept between launches.");
    }

    void VolumeRow(RectTransform column, string label, AudioChannel channel) =>
        SettingRow(column, label, () => $"{Mathf.RoundToInt(GameSettings.ChannelVolume(channel) * 100f)}%",
            step => GameSettings.SetChannelVolume(channel, Mathf.Round(GameSettings.ChannelVolume(channel) * 10f + step) / 10f));

    void BuildGameplayTab(RectTransform column)
    {
        SettingsNote(column, "Combat feedback and tower rules. Turn off anything that is too busy or too bright.");

        UIKit.Label(column, "Numbers and labels", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Damage numbers", "damageNumbers");
        FlagRow(column, "Strong! / Resist labels", "armorLabels");

        UIKit.Label(column, "Enemy body", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Hit flash and swell", "hitFlash");
        FlagRow(column, "Stun stars", "stunStars");
        FlagRow(column, "Knockback squash, dust", "knockbackEffects");
        FlagRow(column, "Death burst", "deathBurst");

        UIKit.Label(column, "Soldiers", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Spawn / despawn effects", "soldierSpawnEffects");

        UIKit.Label(column, "Captains", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Level up effects", "levelUpEffects");

        UIKit.Label(column, "Indicators", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Status icons", "statusIcons");
        FlagRow(column, "Health bar extras", "healthBarExtras");

        UIKit.Label(column, "Threat alerts", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Leak edge flash", "edgeFlash");
        FlagRow(column, "Elite / boss banner", "threatBanner");

        UIKit.Label(column, "Run setup", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        SettingRow(column, "Captains to choose from", () => GameSettings.CaptainsOffered.ToString(),
            step => GameSettings.SetCaptainsOffered(GameSettings.MinCaptainsOffered + Wrap(
                GameSettings.CaptainsOffered - GameSettings.MinCaptainsOffered + step,
                GameSettings.MaxCaptainsOffered - GameSettings.MinCaptainsOffered + 1)));
        SettingsNote(column, "A new run offers this many random roster captains to pick the party from. Never fewer than the party size or more than the roster.");

        UIKit.Label(column, "Towers", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        SettingRow(column, "Disabled for", () => GameSettings.TowerDisabledWaves switch
            {
                0 => "Rest of the wave",
                1 => "1 more wave",
                int n => $"{n} more waves",
            },
            step => GameSettings.SetTowerDisabledWaves(Wrap(GameSettings.TowerDisabledWaves + step, GameSettings.MaxTowerDisabledWaves + 1)));
        SettingsNote(column, "Rest of the wave: a disabled tower can be restored for Gold once the current wave ends.");

        UIKit.Label(column, "Requests", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        SettingRow(column, "Pause during requests", () => GameSettings.PauseDuringRequests ? "On" : "Off",
            _ => GameSettings.SetPauseDuringRequests(!GameSettings.PauseDuringRequests));
        SettingsNote(column, "Off: the match keeps running while a captain waits for an answer, and the request still times out.");

        SettingsNote(column, "Reduce flashing: turn off Hit flash and swell and Leak edge flash. Health bar extras covers the draining ghost segment and hiding full bars.");
    }

    void SettingsNote(Transform parent, string text) => UIKit.Label(parent, text, 12, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

    // A name and an On/Off button that flips it.
    void FlagRow(Transform parent, string name, string flag) =>
        SettingRow(parent, name, () => GameSettings.Flag(flag) ? "On" : "Off", _ => GameSettings.SetFlag(flag, !GameSettings.Flag(flag)));

    // A name, then "<", the current value and ">"; the arrows step through the options.
    void SettingRow(Transform parent, string name, Func<string> value, Action<int> step)
    {
        RectTransform row = UIKit.Group(parent, name);
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 6, fitContent: true);
        UIKit.Size(UIKit.Label(row, name, 14), 150f, 28f);
        UIKit.Button(row, "<", () => step(-1), 32f, 28f);
        UIKit.Size(UIKit.LiveLabel(row, value, refreshers, 14, TextAnchor.MiddleCenter), 220f, 28f);
        UIKit.Button(row, ">", () => step(1), 32f, 28f);
    }

    // Wraps an index into [0, count), treating a missing entry (-1 plus a step) as the start.
    static int Wrap(int index, int count) => count == 0 ? 0 : ((index % count) + count) % count;
}
