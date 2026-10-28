using UnityEngine;

// Pure geometry for ability shapes, kept free of gameplay types so it can be unit tested.
public static class AbilityMath
{
    // Shortest distance from point to the segment a-b (a point when a == b).
    public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared <= 0f) return Vector2.Distance(point, a);
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        return Vector2.Distance(point, a + ab * t);
    }

    // The far end of a line that starts at origin, points at target and is length long.
    // A target on top of the origin gives a zero-length line.
    public static Vector2 LineEnd(Vector2 origin, Vector2 target, float length)
    {
        Vector2 direction = target - origin;
        if (direction.sqrMagnitude <= 0f) return origin;
        return origin + direction.normalized * length;
    }
}
