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
}
