using System.Collections.Generic;
using UnityEngine;

// Player graphics and audio settings, kept in PlayerPrefs. Each change applies at once and is
// saved; everything is applied again when the game starts. Until the player changes something the
// defaults are whatever the player already runs with.
public static class GameSettings
{
    public static readonly FullScreenMode[] WindowModes =
        { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
    // Zero means no cap.
    public static readonly int[] FrameRateCaps = { 30, 60, 120, 144, 240, 0 };

    const string WidthKey = "settings.width";
    const string HeightKey = "settings.height";
    const string WindowModeKey = "settings.windowMode";
    const string VSyncKey = "settings.vsync";
    const string FrameRateKey = "settings.frameRate";
    const string QualityKey = "settings.quality";
    const string VolumeKey = "settings.volume";

    public static Vector2Int Resolution =>
        new(PlayerPrefs.GetInt(WidthKey, Screen.width), PlayerPrefs.GetInt(HeightKey, Screen.height));
    public static FullScreenMode WindowMode => (FullScreenMode)PlayerPrefs.GetInt(WindowModeKey, (int)Screen.fullScreenMode);
    public static bool VSync => PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
    public static int FrameRateCap => PlayerPrefs.GetInt(FrameRateKey, 0);
    public static int Quality => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
    // 0 to 1.
    public static float Volume => PlayerPrefs.GetFloat(VolumeKey, 1f);

    // The monitor's resolutions, largest last, without duplicates for each refresh rate.
    public static List<Vector2Int> Resolutions
    {
        get
        {
            var sizes = new List<Vector2Int>();
            foreach (UnityEngine.Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!sizes.Contains(size)) sizes.Add(size);
            }
            if (!sizes.Contains(Resolution)) sizes.Add(Resolution);
            sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return sizes;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyAtStartup() => Apply(screen: true);

    // Quality first: switching level resets VSync, which is applied after it. The screen mode is
    // only set when asked, since setting it again can flicker a fullscreen window.
    public static void Apply(bool screen)
    {
        if (QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Quality, true);
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FrameRateCap > 0 ? FrameRateCap : -1;
        AudioListener.volume = Volume;
        // The editor's Game view ignores these; only a player window resizes.
        if (screen && !Application.isEditor && (PlayerPrefs.HasKey(WidthKey) || PlayerPrefs.HasKey(WindowModeKey)))
            Screen.SetResolution(Resolution.x, Resolution.y, WindowMode);
    }

    public static void SetResolution(Vector2Int size)
    {
        PlayerPrefs.SetInt(WidthKey, size.x);
        PlayerPrefs.SetInt(HeightKey, size.y);
        Save(screen: true);
    }

    public static void SetWindowMode(FullScreenMode mode)
    {
        PlayerPrefs.SetInt(WindowModeKey, (int)mode);
        Save(screen: true);
    }

    public static void SetVSync(bool on)
    {
        PlayerPrefs.SetInt(VSyncKey, on ? 1 : 0);
        Save();
    }

    public static void SetFrameRateCap(int cap)
    {
        PlayerPrefs.SetInt(FrameRateKey, cap);
        Save();
    }

    public static void SetQuality(int level)
    {
        PlayerPrefs.SetInt(QualityKey, level);
        Save();
    }

    public static void SetVolume(float volume)
    {
        PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(volume));
        Save();
    }

    static void Save(bool screen = false)
    {
        PlayerPrefs.Save();
        Apply(screen);
    }

    public static string WindowModeName(FullScreenMode mode) => mode switch
    {
        FullScreenMode.ExclusiveFullScreen => "Fullscreen",
        FullScreenMode.FullScreenWindow => "Borderless window",
        FullScreenMode.MaximizedWindow => "Maximized window",
        _ => "Windowed",
    };
}
