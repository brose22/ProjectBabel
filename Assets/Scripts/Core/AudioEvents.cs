using UnityEngine.SceneManagement;

// Turns game events into sounds, so gameplay code stays free of audio calls. GameEvents clears its
// listeners on entering Play mode, before AudioManager subscribes this.
public static class AudioEvents
{
    public static void Subscribe()
    {
        GameEvents.EnemyKilled += _ => AudioManager.Play(AudioKeys.EnemyKilled);
        GameEvents.EnemyLeaked += _ => AudioManager.Play(AudioKeys.EnemyLeaked);
        GameEvents.EnemyArrived += _ => AudioManager.Play(AudioKeys.EnemyArrived);
        GameEvents.SoldierDied += _ => AudioManager.Play(AudioKeys.SoldierDied);
        GameEvents.TowerPlaced += _ => AudioManager.Play(AudioKeys.TowerPlaced);
        GameEvents.TowerSold += _ => AudioManager.Play(AudioKeys.TowerSold);
        GameEvents.TowerDisabled += _ => AudioManager.Play(AudioKeys.TowerDisabled);
        GameEvents.TowerRestored += _ => AudioManager.Play(AudioKeys.TowerRestored);
        GameEvents.AbilityUsed += _ => AudioManager.Play(AudioKeys.AbilityUsed);
        GameEvents.CaptainLeveledUp += (_, _) => AudioManager.Play(AudioKeys.CaptainLeveledUp);
        GameEvents.BondRankUp += _ => AudioManager.Play(AudioKeys.BondRankUp);
        GameEvents.WaveStarted += _ => AudioManager.Play(AudioKeys.WaveStarted);
        GameEvents.WaveEnded += _ => AudioManager.Play(AudioKeys.WaveEnded);
        GameEvents.MatchEnded += won => AudioManager.Play(won ? AudioKeys.MatchWon : AudioKeys.MatchLost);
        SceneManager.sceneLoaded -= PlaySceneMusic;
        SceneManager.sceneLoaded += PlaySceneMusic;
    }

    static void PlaySceneMusic(Scene scene, LoadSceneMode mode)
    {
        if (scene.name is MainMenu.SceneName or CastleScreen.SceneName) AudioManager.PlayMusic(AudioKeys.MusicMenu);
        else if (scene.name == MainMenu.MatchSceneName) AudioManager.PlayMusic(AudioKeys.MusicMatch);
    }
}
