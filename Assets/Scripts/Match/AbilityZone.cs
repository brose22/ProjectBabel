using System.Collections.Generic;
using UnityEngine;

// A short-lived sprite for an ability, created at runtime like Projectile. It first pops out to
// full size in real time, so it animates during the ability's hit-stop while the match is frozen,
// then fades out over its lifetime. With damage per second set it also hurts enemies inside its
// radius (Zone shape).
public class AbilityZone : MonoBehaviour
{
    const int SortingOrder = 3;
    // The pop starts from this fraction of full size.
    const float PopStartScale = 0.35f;

    SpriteRenderer spriteRenderer;
    Color color;
    Vector3 fullScale;
    float popSeconds;
    float popAge;
    float lifetime;
    float age;
    float radius;
    float damagePerSecond;
    // Credited with kills the zone lands.
    Tower source;

    public static AbilityZone Spawn(Vector3 position, float rotationDegrees, Vector3 scale, Sprite sprite, Color color,
        float lifetime, float popSeconds, float radius = 0f, float damagePerSecond = 0f, Tower source = null)
    {
        var zoneObject = new GameObject("Ability Effect");
        zoneObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotationDegrees));
        zoneObject.transform.localScale = scale;

        var zone = zoneObject.AddComponent<AbilityZone>();
        zone.spriteRenderer = zoneObject.AddComponent<SpriteRenderer>();
        zone.spriteRenderer.sprite = sprite;
        zone.spriteRenderer.color = color;
        zone.spriteRenderer.sortingOrder = SortingOrder;
        zone.color = color;
        zone.fullScale = scale;
        zone.popSeconds = popSeconds;
        if (popSeconds > 0f) zoneObject.transform.localScale = scale * PopStartScale;
        zone.lifetime = Mathf.Max(0.05f, lifetime);
        zone.radius = radius;
        zone.damagePerSecond = damagePerSecond;
        zone.source = source;
        return zone;
    }

    void Update()
    {
        if (popAge < popSeconds)
        {
            popAge += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(popAge / popSeconds);
            // Ease out so the burst snaps open and settles.
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = fullScale * Mathf.Lerp(PopStartScale, 1f, eased);
        }

        age += Time.deltaTime;
        Color faded = color;
        faded.a *= 1f - Mathf.Clamp01(age / lifetime);
        spriteRenderer.color = faded;

        if (damagePerSecond > 0f)
        {
            // Snapshot first: damage can kill enemies, which removes them from Enemy.Active.
            foreach (Enemy enemy in new List<Enemy>(Enemy.Active))
                if (Vector2.Distance(transform.position, enemy.transform.position) <= radius)
                    enemy.TakeDamage(damagePerSecond * Time.deltaTime, source);
        }

        if (age >= lifetime) Destroy(gameObject);
    }
}
