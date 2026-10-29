using UnityEngine;

// Where CameraFocus centres a zoomed-in view.
public static class CameraFocusMath
{
    // Centres a zoomed view on the point while keeping it inside the view from home, so zooming
    // in near the edge of the map never shows past it.
    public static Vector2 ClampCentre(Vector2 point, Vector2 home, Vector2 homeHalfSize, Vector2 halfSize)
    {
        float slackX = Mathf.Max(0f, homeHalfSize.x - halfSize.x);
        float slackY = Mathf.Max(0f, homeHalfSize.y - halfSize.y);
        return new Vector2(Mathf.Clamp(point.x, home.x - slackX, home.x + slackX),
            Mathf.Clamp(point.y, home.y - slackY, home.y + slackY));
    }
}
