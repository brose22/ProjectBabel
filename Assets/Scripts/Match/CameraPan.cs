using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// Lets the player look around the match: drag with the middle mouse button or use WASD or the
// right stick to pan, Ctrl and the scroll wheel or left trigger and right stick to zoom. The view stays inside
// the padded map. Pixel-perfect rendering only allows whole-number zooms, so it is turned off while
// the view is moved or zoomed and back on once the camera is home again. Does nothing while CameraFocus has the camera, and moves it through
// CameraShake's rest position so a shake still plays.
[RequireComponent(typeof(Camera), typeof(CameraShake), typeof(CameraFocus))]
public class CameraPan : MonoBehaviour
{
    // How close the camera can zoom, as a share of the default view.
    const float MinSizeShare = 0.4f;
    // Zoom per scroll step (120 units of wheel delta).
    const float ZoomPerStep = 1.15f;
    // WASD speed, in view heights per second.
    const float KeySpeed = 0.9f;
    const float HomeTolerance = 0.01f;
    // Zoom steps per second at full stick.
    const float StickZoomSteps = 5f;
    // World units kept between a followed point and the edge of the view.
    const float FollowMargin = 1f;

    Camera cam;
    CameraShake shake;
    CameraFocus focus;
    PixelPerfectCamera pixelPerfect;
    GridMap grid;
    Vector3 homePosition;
    float homeSize;
    bool moved;
    Vector2 lastMouse;

    // True while the view is panned or zoomed away from the default one.
    public bool IsMoved => moved;

    // Snaps back to the default view.
    public void Recenter()
    {
        if (moved && !focus.IsFocused) End();
    }

    // Moves the view just far enough to show a point, for the gamepad's tile cursor.
    public void KeepInView(Vector2 point)
    {
        if (grid == null || focus.IsFocused) return;
        float height = cam.orthographicSize;
        Vector2 half = new(height * cam.aspect, height);
        Vector2 centre = shake.RestPosition;
        Vector2 room = half - Vector2.one * FollowMargin;
        Vector2 wanted = new(Mathf.Clamp(centre.x, point.x - room.x, point.x + room.x),
            Mathf.Clamp(centre.y, point.y - room.y, point.y + room.y));
        wanted = CameraFocusMath.ClampCentre(wanted, grid.ViewBounds, half);
        if (Vector2.Distance(wanted, centre) < HomeTolerance) return;
        if (!moved) Begin();
        shake.RestPosition = new Vector3(wanted.x, wanted.y, homePosition.z);
    }

    public void Setup(GridMap gridMap)
    {
        grid = gridMap;
        focus.Bounds = grid.ViewBounds;
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        shake = GetComponent<CameraShake>();
        focus = GetComponent<CameraFocus>();
        pixelPerfect = GetComponent<PixelPerfectCamera>();
    }

    void Update()
    {
        if (grid == null || focus.IsFocused) return;
        if (GameInput.Recenter) Recenter();
        Mouse mouse = Mouse.current;
        Vector2 mouseNow = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        bool overUi = mouse != null && UIKit.IsPointerOverUI();

        // The plain wheel cycles captains (PlacementController); Ctrl and the wheel zoom.
        float scroll = mouse != null && !overUi ? GameInput.WheelZoom : 0f;
        bool dragging = mouse != null && mouse.middleButton.isPressed && !overUi;
        Vector2 drag = dragging && mouse.middleButton.wasPressedThisFrame == false ? mouseNow - lastMouse : Vector2.zero;
        lastMouse = mouseNow;
        Vector2 keys = GameInput.PanKeys;
        // The right stick scrolls lists while a menu is up.
        float stickZoom = 0f;
        if (UIFocusScope.Top == null)
        {
            keys = Vector2.ClampMagnitude(keys + GameInput.PadPan, 1f);
            stickZoom = GameInput.PadZoom;
            scroll += stickZoom * StickZoomSteps * 120f * Time.unscaledDeltaTime;
        }

        if (scroll == 0f && drag == Vector2.zero && keys == Vector2.zero) return;
        if (!moved) Begin();

        float size = cam.orthographicSize;
        Vector2 centre = shake.RestPosition;
        if (scroll != 0f)
        {
            float newSize = Mathf.Clamp(size * Mathf.Pow(1f / ZoomPerStep, scroll / 120f),
                homeSize * MinSizeShare, CameraFocusMath.MaxSize(grid.ViewBounds, cam.aspect, homeSize));
            // The stick zooms on the middle of the view, the wheel on the pointer.
            Vector2 anchor = stickZoom != 0f ? centre : (Vector2)cam.ScreenToWorldPoint(mouseNow);
            centre = CameraFocusMath.ZoomCentre(centre, anchor, size, newSize);
            size = newSize;
        }
        float worldPerPixel = 2f * size / Screen.height;
        centre -= drag * worldPerPixel;
        centre += keys * (KeySpeed * size * 2f * Time.unscaledDeltaTime);

        centre = CameraFocusMath.ClampCentre(centre, grid.ViewBounds, new Vector2(size * cam.aspect, size));
        cam.orthographicSize = size;
        shake.RestPosition = new Vector3(centre.x, centre.y, homePosition.z);
        if (Mathf.Abs(size - homeSize) < HomeTolerance && Vector2.Distance(centre, homePosition) < HomeTolerance) End();
    }

    // Remembers the default view and takes the camera from the pixel-perfect component.
    void Begin()
    {
        moved = true;
        homePosition = shake.RestPosition;
        homeSize = cam.orthographicSize;
        if (pixelPerfect != null) pixelPerfect.enabled = false;
    }

    // Back at the default view: hand the camera back to pixel-perfect rendering.
    void End()
    {
        moved = false;
        cam.orthographicSize = homeSize;
        shake.RestPosition = homePosition;
        if (pixelPerfect != null) pixelPerfect.enabled = true;
    }
}
