using UnityEngine;

// A homing shot created at runtime (there is no prefab). It deals its damage on arrival and
// simply vanishes if the target dies or leaks first.
public class Projectile : MonoBehaviour
{
    const float HitDistance = 0.05f;

    Enemy target;
    float damage;
    Tower source;
    SoldierClassData soldierClass;
    float speed;

    public static Projectile Launch(Vector3 origin, Enemy target, float damage, Tower source, SoldierClassData soldierClass, float speed,
        Sprite sprite, Color color, float scale, int sortingOrder)
    {
        var shot = new GameObject("Projectile");
        shot.transform.position = origin;
        shot.transform.localScale = Vector3.one * scale;

        var renderer = shot.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        var projectile = shot.AddComponent<Projectile>();
        projectile.target = target;
        projectile.damage = damage;
        projectile.source = source;
        projectile.soldierClass = soldierClass;
        projectile.speed = speed;
        return projectile;
    }

    void Update()
    {
        if (target == null || !target.IsTargetable)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = target.transform.position;
        // A non-positive speed means the shot lands immediately.
        transform.position = speed > 0f
            ? Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime)
            : destination;
        if (Vector2.Distance(transform.position, destination) > HitDistance) return;

        target.TakeDamage(damage, source, damageType: soldierClass.damageType);
        Destroy(gameObject);
    }
}
