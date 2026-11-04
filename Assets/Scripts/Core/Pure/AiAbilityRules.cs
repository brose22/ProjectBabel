using System;

// When the AI fires specials and how it answers captain requests.
public static class AiAbilityRules
{
    public enum Kind { Attack, Heal, Buff }

    // Answers, in RequestAnswer's order.
    public const int AnswerNo = 0;
    public const int AnswerYes = 1;

    // Request kinds, in RequestKind's order.
    public const int RequestMoreDraw = 0;
    public const int RequestRelocate = 1;
    public const int RequestUseSpecial = 2;
    public const int RequestReinforce = 3;
    public const int RequestLowerDraw = 4;
    public const int RequestRetreat = 5;

    // enemiesInRange counts enemies inside the special's reach. weakestFraction is the lowest
    // health share among towers. enemiesLeft is every enemy still on the field.
    public static bool ShouldFire(Kind kind, int enemiesInRange, bool bossInRange, float weakestFraction, int enemiesLeft, bool spawningDone)
    {
        switch (kind)
        {
            case Kind.Heal:
                return weakestFraction < 0.5f;
            case Kind.Buff:
                return enemiesLeft > 0;
            default:
                // Wait for a crowd or a boss, but don't let the meter sit full while the wave runs out.
                return bossInRange || enemiesInRange >= 3 || (enemiesInRange >= 1 && spawningDone && enemiesLeft <= 3);
        }
    }

    // The AI says Yes only when the cons stay well under what a Yes gives: a bad request is always refused.
    public const float ConTolerance = 0.75f;

    public static int AnswerRequest(RequestEvaluation evaluation) =>
        !evaluation.IsBad && evaluation.ConWeight <= evaluation.ProWeight * ConTolerance ? AnswerYes : AnswerNo;

    // Grants what costs the run nothing; draws only when Gold is plentiful.
    public static int AnswerRequest(int request, int gold, int towersOnField) =>
        request != RequestMoreDraw || gold / Math.Max(1, towersOnField) >= 100 ? AnswerYes : AnswerNo;
}
