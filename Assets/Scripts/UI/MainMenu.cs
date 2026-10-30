using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen the game starts on: start or resume a run, change settings, or quit. Built in
// uGUI at runtime like the run screens; the menu scene only holds a camera and this component.
public class MainMenu : MonoBehaviour
{
    public const string SceneName = "MainMenu";
    public const string MatchSceneName = "Match";

    static readonly Color BackgroundColor = new(0.05f, 0.06f, 0.09f, 1f);

    Canvas canvas;
    RectTransform screen;
    readonly List<Action> refreshers = new();

    // Leaves the match for the menu. The run stays saved as it was; the menu offers to resume it.
    // The scene only changes at the end of the frame, so the run is cleared once the menu loads
    // (Awake) rather than here, where the match HUD would still read it this frame.
    public static void Open() => SceneManager.LoadScene(SceneName);

    // Resume reloads the run from its save, so nothing from the match lingers in memory.
    void Awake() => RunState.Clear();

    void Start()
    {
        canvas = UIKit.CreateCanvas("Main Menu", 0);
        BuildMain();
    }

    void LateUpdate() => UIKit.Refresh(refreshers);

    RectTransform NewScreen(string name, float width)
    {
        refreshers.Clear();
        if (screen != null) Destroy(screen.gameObject);
        screen = UIKit.Stretch(UIKit.Panel(canvas.transform, name, BackgroundColor));
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
        UIKit.Button(column, "Settings", BuildSettings, 240f, 34f);
        UIKit.Button(column, "Exit", Quit, 240f, 34f);
    }

    void BuildConfirmNewRun()
    {
        RectTransform column = NewScreen("Confirm", 460f);
        UIKit.Label(column, "START A NEW RUN?", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, "Your saved run will be lost. Captain approval and bonds carry over as always.", 14,
            TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        RectTransform buttons = UIKit.Group(column, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 10, fitContent: true);
        UIKit.Button(buttons, "Start new run", StartRun, 180f, 32f);
        UIKit.Button(buttons, "Cancel", BuildMain, 140f, 32f);
    }

    static void StartRun()
    {
        RunSave.Delete();
        RunState.Clear();
        SceneManager.LoadScene(MatchSceneName);
    }

    static void ResumeRun()
    {
        RunState.Clear();
        SceneManager.LoadScene(MatchSceneName);
    }

    static void Quit()
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

        switch (settingsTab)
        {
            case SettingsTab.Graphics: BuildGraphicsTab(column); break;
            case SettingsTab.Audio: BuildAudioTab(column); break;
            default: BuildGameplayTab(column); break;
        }

        UIKit.Button(column, "Back", BuildMain, 200f, 32f);
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
        SettingsNote(column, "Changes apply at once and are kept between launches. The editor's Game view ignores resolution and window mode.");
    }

    void BuildAudioTab(RectTransform column)
    {
        SettingRow(column, "Master volume", () => $"{Mathf.RoundToInt(GameSettings.Volume * 100f)}%",
            step => GameSettings.SetVolume(Mathf.Round(GameSettings.Volume * 10f + step) / 10f));
        SettingsNote(column, "Changes apply at once and are kept between launches.");
    }

    void BuildGameplayTab(RectTransform column)
    {
        SettingsNote(column, "Combat feedback on enemies. Turn off anything that is too busy or too bright.");

        UIKit.Label(column, "Numbers and labels", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Damage numbers", "damageNumbers");
        FlagRow(column, "Weak! / Resist labels", "weaknessLabels");

        UIKit.Label(column, "Enemy body", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Hit flash and swell", "hitFlash");
        FlagRow(column, "Stun stars", "stunStars");
        FlagRow(column, "Dizzy wobble", "dizzyWobble");
        FlagRow(column, "Knockback squash, dust", "knockbackEffects");
        FlagRow(column, "Death burst", "deathBurst");

        UIKit.Label(column, "Indicators", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Status icons", "statusIcons");
        FlagRow(column, "Health bar extras", "healthBarExtras");

        UIKit.Label(column, "Threat alerts", 16, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        FlagRow(column, "Leak edge flash", "edgeFlash");
        FlagRow(column, "Elite / boss banner", "threatBanner");

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
