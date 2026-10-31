using UnityEngine;

// Where the match camera may look: centring a view inside the map bounds, and zooming around a point.
public static class CameraFocusMath
{
    // Centres a view with the given half size on the point while keeping it inside the bounds, so
    // panning or zooming in near the edge never shows past them. A view larger than the bounds on
    // an axis sits centred on that axis.
    public static Vector2 ClampCentre(Vector2 point, Rect bounds, Vector2 halfSize) =>
        new(ClampAxis(point.x, bounds.xMin, bounds.xMax, halfSize.x), ClampAxis(point.y, bounds.yMin, bounds.yMax, halfSize.y));

    static float ClampAxis(float value, float min, float max, float half) =>
        max - min <= 2f * half ? (min + max) * 0.5f : Mathf.Clamp(value, min + half, max - half);

    // The largest orthographic size that still fits inside the bounds, but never below the minimum.
    public static float MaxSize(Rect bounds, float aspect, float minimum) =>
        Mathf.Max(minimum, Mathf.Min(bounds.height * 0.5f, bounds.width * 0.5f / aspect));

    // The view centre after changing the orthographic size, so the world point under the cursor
    // stays under it.
    public static Vector2 ZoomCentre(Vector2 centre, Vector2 anchor, float oldSize, float newSize) =>
        anchor + (centre - anchor) * (newSize / oldSize);
}
