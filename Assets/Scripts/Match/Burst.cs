using UnityEngine;

// A handful of small squares that fly outward, slow down, shrink and fade, created at runtime
// like Projectile. Used for dust and death pops. Runs in unscaled time so it plays through an
// ability's hit-stop. With a gather radius the pieces start out around the point and close in on
// it instead, for a spawn.
public class Burst : MonoBehaviour
{
    const int SortingOrder = 4;
    // Fraction of velocity kept each second.
    const float DragPerSecond = 0.02f;

    Transform[] pieces;
    Vector2[] velocities;
    SpriteRenderer[] renderers;
    Color color;
    float size;
    float gatherRadius;
    float lifetime;
    float age;

    public static Burst Spawn(Vector3 position, Color color, int count, float speed, float size, float lifetime, float gatherRadius = 0f)
    {
        var burstObject = new GameObject("Burst");
        burstObject.transform.position = position;
        var burst = burstObject.AddComponent<Burst>();
        burst.color = color;
        burst.size = size;
        burst.lifetime = Mathf.Max(0.05f, lifetime);
        burst.gatherRadius = gatherRadius;
        burst.pieces = new Transform[count];
        burst.velocities = new Vector2[count];
        burst.renderers = new SpriteRenderer[count];
        // Evenly spread directions with a random offset so bursts do not look identical.
        float offset = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            var piece = new GameObject("Piece");
            piece.transform.SetParent(burstObject.transform, false);
            piece.transform.localScale = Vector3.one * size;
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, Random.value * 90f);
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteUtil.Square;
            renderer.color = color;
            renderer.sortingOrder = SortingOrder;
            float angle = offset + Mathf.PI * 2f * i / count + Random.Range(-0.3f, 0.3f);
            burst.pieces[i] = piece.transform;
            burst.renderers[i] = renderer;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            // Gathering pieces keep a direction scaled by their own start distance instead of a speed.
            burst.velocities[i] = gatherRadius > 0f ? direction * Random.Range(0.6f, 1f) : direction * speed * Random.Range(0.6f, 1f);
            if (gatherRadius > 0f) piece.transform.localPosition = burst.velocities[i] * gatherRadius;
        }
        return burst;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float t = age / lifetime;
        if (gatherRadius > 0f)
        {
            UpdateGather(t);
            return;
        }
        float drag = Mathf.Pow(DragPerSecond, dt);
        Color faded = color;
        faded.a *= 1f - t;
        for (int i = 0; i < pieces.Length; i++)
        {
            velocities[i] *= drag;
            pieces[i].localPosition += (Vector3)(velocities[i] * dt);
            pieces[i].localScale = Vector3.one * (size * (1f - t * 0.6f));
            renderers[i].color = faded;
        }
    }

    // Eases each piece in to the centre, fading it over the last stretch.
    void UpdateGather(float t)
    {
        float closeness = 1f - (1f - t) * (1f - t);
        Color shown = color;
        shown.a *= Mathf.Clamp01((1f - t) / 0.3f);
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].localPosition = velocities[i] * gatherRadius * (1f - closeness);
            pieces[i].localScale = Vector3.one * (size * (1f - t * 0.5f));
            renderers[i].color = shown;
        }
    }
}
