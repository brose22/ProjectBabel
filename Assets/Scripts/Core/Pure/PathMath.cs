using UnityEngine;

// Pure path geometry, kept free of gameplay types so it can be unit tested on its own.
public static class PathMath
{
    // Moves a walker that is heading for path[nextPoint] back toward earlier waypoints by
    // distance, stopping at path[0]. newNextPoint follows the walker's convention: the index of
    // the waypoint it should walk to next, so it resumes forward movement normally.
    public static Vector3 MoveBack(Vector3[] path, int nextPoint, Vector3 position, float distance, out int newNextPoint)
    {
        newNextPoint = nextPoint;
        if (path == null || path.Length < 2) return position;

        int next = Mathf.Clamp(nextPoint, 1, path.Length - 1);
        float remaining = distance;
        // Zero, negative and NaN distances skip the loop and leave the walker in place.
        while (remaining > 0f)
        {
            Vector3 previous = path[next - 1];
            float toPrevious = Vector3.Distance(position, previous);
            if (remaining <= toPrevious)
            {
                position = Vector3.MoveTowards(position, previous, remaining);
                break;
            }

            position = previous;
            remaining -= toPrevious;
            if (next == 1) break;
            next--;
        }

        newNextPoint = next;
        return position;
    }

    // The unit vector pointing left of the segment from one waypoint to the next (zero for a
    // zero-length segment).
    public static Vector2 LeftNormal(Vector3 from, Vector3 to)
    {
        Vector2 direction = to - from;
        return direction.sqrMagnitude > 1e-8f ? new Vector2(-direction.y, direction.x).normalized : Vector2.zero;
    }

    // Eases a walker's sideways direction toward the new segment's, so a turn blends over a short
    // stretch instead of snapping. Keeps unit length; a zero target leaves the current direction.
    public static Vector2 EaseNormal(Vector2 current, Vector2 target, float blend)
    {
        if (target == Vector2.zero) return current;
        if (current == Vector2.zero) return target;
        Vector2 eased = Vector2.Lerp(current, target, Mathf.Clamp01(blend));
        return eased.sqrMagnitude > 1e-8f ? eased.normalized : target;
    }
}
