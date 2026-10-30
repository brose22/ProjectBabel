using System;
using System.Collections.Generic;

// Where the AI puts captains, and when it spends Gold on them. Cells are tile coordinates and
// range is in tiles.
public static class AiPlacementScorer
{
    // A road cell counts for more the nearer the castle it is, since that is where leaks happen.
    // roadCells run from the gate to the castle. Cells another captain already reaches count less.
    public static float Coverage(int cellX, int cellY, float range, IReadOnlyList<(int x, int y)> roadCells, bool[] alreadyCovered = null)
    {
        float total = 0f;
        float rangeSqr = range * range;
        for (int i = 0; i < roadCells.Count; i++)
        {
            float dx = roadCells[i].x - cellX;
            float dy = roadCells[i].y - cellY;
            if (dx * dx + dy * dy > rangeSqr) continue;
            float weight = 1f + 0.5f * i / Math.Max(1, roadCells.Count - 1);
            total += alreadyCovered != null && alreadyCovered[i] ? weight * 0.4f : weight;
        }
        return total;
    }

    // Supports (healers, buffers) act on the whole party, so they want safety more than reach:
    // their soldiers still fight, but the tower stays off the road.
    public static float Score(int cellX, int cellY, float range, IReadOnlyList<(int x, int y)> roadCells, bool support, bool[] alreadyCovered)
    {
        float coverage = Coverage(cellX, cellY, range, roadCells, alreadyCovered);
        if (!support) return coverage;
        float nearestSqr = float.MaxValue;
        foreach ((int x, int y) in roadCells)
        {
            float dx = x - cellX;
            float dy = y - cellY;
            nearestSqr = Math.Min(nearestSqr, dx * dx + dy * dy);
        }
        return coverage * 0.5f - (nearestSqr <= 2.25f ? 4f : 0f);
    }

    // Marks the road cells a tower at this cell reaches.
    public static void MarkCovered(int cellX, int cellY, float range, IReadOnlyList<(int x, int y)> roadCells, bool[] covered)
    {
        float rangeSqr = range * range;
        for (int i = 0; i < roadCells.Count; i++)
        {
            float dx = roadCells[i].x - cellX;
            float dy = roadCells[i].y - cellY;
            if (dx * dx + dy * dy <= rangeSqr) covered[i] = true;
        }
    }

    // The best candidate's index (the first on a tie), or -1 with none.
    public static int PickCell(IReadOnlyList<(int x, int y)> candidates, float range, IReadOnlyList<(int x, int y)> roadCells,
        bool support, bool[] alreadyCovered, out float bestScore)
    {
        int best = -1;
        bestScore = float.MinValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            float score = Score(candidates[i].x, candidates[i].y, range, roadCells, support, alreadyCovered);
            if (score <= bestScore) continue;
            best = i;
            bestScore = score;
        }
        return best;
    }

    // Moving costs Gold, so only a clearly poor spot is worth it, and only with Gold to spare.
    public static bool ShouldMove(float currentScore, float bestScore, int gold, int moveCost)
    {
        if (bestScore <= 0f || currentScore >= bestScore * 0.4f) return false;
        return gold >= moveCost * 3;
    }

    // Restore a disabled tower when the cost leaves a cushion for the draws that keep towers fighting.
    public static bool ShouldRestore(int restoreCost, int gold, int towersOnField) => gold >= restoreCost + 10 * Math.Max(1, towersOnField);

    // Draw rate as 0 Low, 1 Normal, 2 High: draw hard while Gold is plentiful, ease off when it is not.
    public static int DrawRateFor(int gold, int towersOnField)
    {
        int perTower = gold / Math.Max(1, towersOnField);
        return perTower >= 120 ? 2 : perTower >= 25 ? 1 : 0;
    }
}
