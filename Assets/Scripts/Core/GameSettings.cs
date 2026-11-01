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
    const string VolumeKey = "settings.volume.";

    // Match feedback switches, all on by default. Read per enemy per frame, so each is cached.
    static readonly Dictionary<string, bool> flags = new();

    public static bool DamageNumbers => Flag("damageNumbers");
    public static bool ArmorLabels => Flag("armorLabels");
    public static bool HitFlash => Flag("hitFlash");
    public static bool StatusIcons => Flag("statusIcons");
    public static bool StunStars => Flag("stunStars");
    public static bool DizzyWobble => Flag("dizzyWobble");
    public static bool KnockbackEffects => Flag("knockbackEffects");
    public static bool DeathBurst => Flag("deathBurst");
    public static bool SoldierSpawnEffects => Flag("soldierSpawnEffects");
    public static bool LevelUpEffects => Flag("levelUpEffects");
    public static bool HealthBarExtras => Flag("healthBarExtras");
    public static bool EdgeFlash => Flag("edgeFlash");
    public static bool ThreatBanner => Flag("threatBanner");

    public static bool Flag(string name)
    {
        if (!flags.TryGetValue(name, out bool on)) flags[name] = on = PlayerPrefs.GetInt("settings.fx." + name, 1) == 1;
        return on;
    }

    public static void SetFlag(string name, bool on)
    {
        flags[name] = on;
        PlayerPrefs.SetInt("settings.fx." + name, on ? 1 : 0);
        Save();
    }

    // Whether a captain's request holds the match while it waits. On by default.
    public static bool PauseDuringRequests => PlayerPrefs.GetInt("settings.pauseDuringRequests", 1) == 1;

    public static void SetPauseDuringRequests(bool on)
    {
        PlayerPrefs.SetInt("settings.pauseDuringRequests", on ? 1 : 0);
        Save();
    }

    // Roster captains offered to fill the party at run start. At least the party size, at most the roster.
    public const int MaxCaptainsOffered = 12;
    public const int MinCaptainsOffered = 3;
    public static int CaptainsOffered => Mathf.Clamp(PlayerPrefs.GetInt("settings.captainsOffered", 5), MinCaptainsOffered, MaxCaptainsOffered);

    public static void SetCaptainsOffered(int count)
    {
        PlayerPrefs.SetInt("settings.captainsOffered", Mathf.Clamp(count, MinCaptainsOffered, MaxCaptainsOffered));
        Save();
    }

    public const int MaxTowerDisabledWaves = 3;
    // Waves a disabled tower sits out. 0 lets it be restored for Gold as soon as the current wave concludes.
    public static int TowerDisabledWaves => Mathf.Clamp(PlayerPrefs.GetInt("settings.towerDisabledWaves", 1), 0, MaxTowerDisabledWaves);

    public static void SetTowerDisabledWaves(int waves)
    {
        PlayerPrefs.SetInt("settings.towerDisabledWaves", Mathf.Clamp(waves, 0, MaxTowerDisabledWaves));
        Save();
    }

    public static Vector2Int Resolution =>
        new(PlayerPrefs.GetInt(WidthKey, Screen.width), PlayerPrefs.GetInt(HeightKey, Screen.height));
    public static FullScreenMode WindowMode => (FullScreenMode)PlayerPrefs.GetInt(WindowModeKey, (int)Screen.fullScreenMode);
    public static bool VSync => PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
    public static int FrameRateCap => PlayerPrefs.GetInt(FrameRateKey, 0);
    public static int Quality => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
    // 0 to 1.
    public static float ChannelVolume(AudioChannel channel) => PlayerPrefs.GetFloat(VolumeKey + channel, 1f);

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
        AudioManager.ApplyVolumes();
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

    public static void SetChannelVolume(AudioChannel channel, float volume)
    {
        PlayerPrefs.SetFloat(VolumeKey + channel, Mathf.Clamp01(volume));
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
