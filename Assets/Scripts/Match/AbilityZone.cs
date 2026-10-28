using System.Collections.Generic;
using UnityEngine;

// A short-lived sprite for an ability, created at runtime like Projectile. It fades out over
// its lifetime; with damage per second set it also hurts enemies inside its radius (Zone shape).
public class AbilityZone : MonoBehaviour
{
    const int SortingOrder = 3;

    SpriteRenderer spriteRenderer;
    Color color;
    float lifetime;
    float age;
    float radius;
    float damagePerSecond;

    public static AbilityZone Spawn(Vector3 position, float rotationDegrees, Vector3 scale, Sprite sprite, Color color,
        float lifetime, float radius = 0f, float damagePerSecond = 0f)
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
        zone.lifetime = Mathf.Max(0.05f, lifetime);
        zone.radius = radius;
        zone.damagePerSecond = damagePerSecond;
        return zone;
    }

    void Update()
    {
        age += Time.deltaTime;
        Color faded = color;
        faded.a *= 1f - Mathf.Clamp01(age / lifetime);
        spriteRenderer.color = faded;

        if (damagePerSecond > 0f)
        {
            // Snapshot first: damage can kill enemies, which removes them from Enemy.Active.
            foreach (Enemy enemy in new List<Enemy>(Enemy.Active))
                if (Vector2.Distance(transform.position, enemy.transform.position) <= radius)
                    enemy.TakeDamage(damagePerSecond * Time.deltaTime);
        }

        if (age >= lifetime) Destroy(gameObject);
    }
}
