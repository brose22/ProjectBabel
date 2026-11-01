using System.Text;
using UnityEngine;

// Pure sums behind AudioManager and the AudioKeys generator.
public static class AudioRules
{
    public const float SilentDecibels = -80f;

    // Mixer volumes are decibels; settings are 0 to 1. Zero has no decibel value, so it is the
    // mixer's silent floor.
    public static float VolumeToDecibels(float volume) =>
        volume <= 0.0001f ? SilentDecibels : Mathf.Max(SilentDecibels, 20f * Mathf.Log10(Mathf.Min(volume, 1f)));

    public static bool CooldownOver(float lastPlayed, float now, float cooldown) => now - lastPlayed >= cooldown;

    // "enemy.killed" and "enemy_killed" both become "EnemyKilled". Empty if nothing usable is left.
    public static string KeyToIdentifier(string key)
    {
        var name = new StringBuilder();
        bool startWord = true;
        foreach (char c in key ?? "")
        {
            if (!char.IsLetterOrDigit(c))
            {
                startWord = true;
                continue;
            }
            if (name.Length == 0 && char.IsDigit(c)) name.Append('_');
            name.Append(startWord ? char.ToUpperInvariant(c) : c);
            startWord = false;
        }
        return name.ToString();
    }
}
