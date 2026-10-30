using System;
using UnityEngine;

// The kind of tile a soldier class likes to wait on.
public enum HomeTile { Any, Road, OffRoad }

// Which tile around a tower a soldier waits on.
public static class SoldierHomeMath
{
    // Listed in order of preference, so ties go to the tiles right beside the tower.
    static readonly Vector2Int[] Neighbours =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
        new(1, 1), new(1, -1), new(-1, -1), new(-1, 1),
    };

    // Standing somewhere legal matters most, then having the tile alone, then liking it. When
    // nothing is free the soldier shares a tile rather than having nowhere to stand.
    public static Vector2Int Pick(Vector2Int centre, HomeTile wanted, Func<Vector2Int, bool> isRoad,
        Func<Vector2Int, bool> canStand, Func<Vector2Int, bool> isTaken)
    {
        Vector2Int best = centre + Neighbours[0];
        int bestScore = -1;
        foreach (Vector2Int offset in Neighbours)
        {
            Vector2Int cell = centre + offset;
            int score = (canStand(cell) ? 4 : 0) + (isTaken(cell) ? 0 : 2) + (Likes(wanted, isRoad(cell)) ? 1 : 0);
            if (score <= bestScore) continue;
            best = cell;
            bestScore = score;
        }
        return best;
    }

    static bool Likes(HomeTile wanted, bool road) => wanted switch
    {
        HomeTile.Road => road,
        HomeTile.OffRoad => !road,
        _ => true,
    };
}
