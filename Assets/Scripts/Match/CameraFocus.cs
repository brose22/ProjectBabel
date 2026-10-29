using UnityEngine;
using UnityEngine.Rendering.Universal;

// Zooms the camera in on a point of interest, such as a captain asking for something, and back
// out when released. Runs in unscaled time since it plays while the match is paused. Pixel-perfect
// rendering is turned off while zoomed (it only allows whole-number zooms) and back on once the
// camera is home. Moves the camera through CameraShake's rest position, so a shake still plays.
[RequireComponent(typeof(Camera), typeof(CameraShake))]
// Before CameraShake, which places the camera at the rest position set here.
[DefaultExecutionOrder(-10)]
public class CameraFocus : MonoBehaviour
{
    // How much closer the camera gets, and how long zooming in or out takes.
    const float Zoom = 2f;
    const float TransitionSeconds = 0.35f;

    Camera cam;
    CameraShake shake;
    PixelPerfectCamera pixelPerfect;
    Vector3 homePosition;
    float homeSize;
    Transform target;
    // 0 = home, 1 = fully zoomed on the target.
    float blend;
    // Where the camera zoomed in on, kept so zooming out after the target is gone still works.
    Vector3 focusPoint;

    void Awake()
    {
        cam = GetComponent<Camera>();
        shake = GetComponent<CameraShake>();
        pixelPerfect = GetComponent<PixelPerfectCamera>();
        homePosition = transform.localPosition;
    }

    // Zooms in on the target, or back out with null.
    public void Focus(Transform newTarget)
    {
        if (newTarget != null && blend <= 0f)
        {
            // The pixel-perfect size is the one to return to.
            homeSize = cam.orthographicSize;
            if (pixelPerfect != null) pixelPerfect.enabled = false;
        }
        target = newTarget;
    }

    void LateUpdate()
    {
        if (target != null) focusPoint = target.position;
        float goal = target != null ? 1f : 0f;
        if (blend == goal) return;

        blend = Mathf.MoveTowards(blend, goal, Time.unscaledDeltaTime / TransitionSeconds);
        float t = Mathf.SmoothStep(0f, 1f, blend);
        float size = Mathf.Lerp(homeSize, homeSize / Zoom, t);
        Vector2 centre = CameraFocusMath.ClampCentre(focusPoint, homePosition,
            new Vector2(homeSize * cam.aspect, homeSize), new Vector2(size * cam.aspect, size));
        Vector2 position = Vector2.Lerp(homePosition, centre, t);
        cam.orthographicSize = size;
        shake.RestPosition = new Vector3(position.x, position.y, homePosition.z);

        if (blend <= 0f && pixelPerfect != null) pixelPerfect.enabled = true;
    }
}
