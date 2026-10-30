// The match's time scale: frozen while the match is over, during an ability hit-stop or while a
// captain's request waits for an answer or the player has paused, otherwise the chosen game speed.
public static class TimeScaleRules
{
    public static float Resolve(bool matchOver, bool hitStopped, bool requestPaused, float gameSpeed, bool playerPaused = false) =>
        matchOver || hitStopped || requestPaused || playerPaused ? 0f : gameSpeed;
}
