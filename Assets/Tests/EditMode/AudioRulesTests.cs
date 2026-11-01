using NUnit.Framework;

public class AudioRulesTests
{
    [TestCase(1f, 0f)]
    [TestCase(0.1f, -20f)]
    [TestCase(0f, AudioRules.SilentDecibels)]
    [TestCase(2f, 0f)]
    public void VolumeToDecibels_ClampsToTheMixerRange(float volume, float expected)
    {
        Assert.That(AudioRules.VolumeToDecibels(volume), Is.EqualTo(expected).Within(1e-3f));
    }

    [TestCase(1f, 1.05f, 0.1f, false)]
    [TestCase(1f, 1.1f, 0.1f, true)]
    [TestCase(1f, 1f, 0f, true)]
    public void CooldownOver_WaitsTheFullCooldown(float lastPlayed, float now, float cooldown, bool expected)
    {
        Assert.That(AudioRules.CooldownOver(lastPlayed, now, cooldown), Is.EqualTo(expected));
    }

    [TestCase("enemy.killed", "EnemyKilled")]
    [TestCase("ui_click", "UiClick")]
    [TestCase("music.menu-2", "MusicMenu2")]
    [TestCase("2.hit", "_2Hit")]
    [TestCase("...", "")]
    [TestCase(null, "")]
    public void KeyToIdentifier_MakesAValidName(string key, string expected)
    {
        Assert.That(AudioRules.KeyToIdentifier(key), Is.EqualTo(expected));
    }
}
