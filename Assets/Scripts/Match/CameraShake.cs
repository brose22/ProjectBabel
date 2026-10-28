using UnityEngine;

// Small jitter around the camera's resting position. Runs in unscaled time so it plays through
// an ability's hit-stop, and fades out linearly so it reads as a single jolt.
public class CameraShake : MonoBehaviour
{
    Vector3 restPosition;
    float strength;
    float duration;
    float remaining;

    void Awake() => restPosition = transform.localPosition;

    // A new shake replaces a weaker or nearly finished one rather than adding to it.
    public void Shake(float shakeStrength, float shakeDuration)
    {
        if (shakeStrength <= 0f || shakeDuration <= 0f) return;
        float current = remaining > 0f ? strength * remaining / duration : 0f;
        if (shakeStrength < current) return;
        strength = shakeStrength;
        duration = shakeDuration;
        remaining = shakeDuration;
    }

    void LateUpdate()
    {
        if (remaining <= 0f) return;
        remaining -= Time.unscaledDeltaTime;
        if (remaining <= 0f)
        {
            transform.localPosition = restPosition;
            return;
        }
        Vector2 offset = Random.insideUnitCircle * (strength * remaining / duration);
        transform.localPosition = restPosition + (Vector3)offset;
    }
}
