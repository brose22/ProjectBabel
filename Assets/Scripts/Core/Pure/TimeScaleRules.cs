// The match's time scale: frozen while the match is over, during an ability hit-stop or while a
// captain's request waits for an answer, otherwise the chosen game speed.
public static class TimeScaleRules
{
    public static float Resolve(bool matchOver, bool hitStopped, bool requestPaused, float gameSpeed) =>
        matchOver || hitStopped || requestPaused ? 0f : gameSpeed;
}
