using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// Lets the player look around the match: drag with the middle mouse button or use the arrow keys to
// pan, the scroll wheel to zoom. The view stays inside the padded map. Pixel-perfect rendering only
// allows whole-number zooms, so it is turned off while the view is moved or zoomed and back on once
// the camera is home again. Does nothing while CameraFocus has the camera, and moves it through
// CameraShake's rest position so a shake still plays.
[RequireComponent(typeof(Camera), typeof(CameraShake), typeof(CameraFocus))]
public class CameraPan : MonoBehaviour
{
    // How close the camera can zoom, as a share of the default view.
    const float MinSizeShare = 0.4f;
    // Zoom per scroll step (120 units of wheel delta).
    const float ZoomPerStep = 1.15f;
    // Arrow-key speed, in view heights per second.
    const float KeySpeed = 0.9f;
    const float HomeTolerance = 0.01f;

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
        Mouse mouse = Mouse.current;
        Vector2 mouseNow = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        bool overUi = mouse != null && UIKit.IsPointerOverUI();

        float scroll = mouse != null && !overUi ? mouse.scroll.ReadValue().y : 0f;
        bool dragging = mouse != null && mouse.middleButton.isPressed && !overUi;
        Vector2 drag = dragging && mouse.middleButton.wasPressedThisFrame == false ? mouseNow - lastMouse : Vector2.zero;
        lastMouse = mouseNow;
        Vector2 keys = GameInput.PanKeys;

        if (scroll == 0f && drag == Vector2.zero && keys == Vector2.zero) return;
        if (!moved) Begin();

        float size = cam.orthographicSize;
        Vector2 centre = shake.RestPosition;
        if (scroll != 0f)
        {
            float newSize = Mathf.Clamp(size * Mathf.Pow(1f / ZoomPerStep, scroll / 120f),
                homeSize * MinSizeShare, CameraFocusMath.MaxSize(grid.ViewBounds, cam.aspect, homeSize));
            Vector2 anchor = cam.ScreenToWorldPoint(mouseNow);
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
