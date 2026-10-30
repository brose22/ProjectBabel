using UnityEngine;

// A number or word that drifts up and fades, created at runtime like Projectile. It runs in
// unscaled time so it keeps moving through an ability's hit-stop.
public class FloatingText : MonoBehaviour
{
    const int SortingOrder = 20;
    const float RiseSpeed = 0.9f;
    // Text is authored at this font size and scaled by characterSize, which keeps it crisp.
    const int FontSize = 48;
    const float CharacterSize = 0.02f;

    TextMesh mesh;
    Color color;
    float lifetime;
    float age;

    public static FloatingText Spawn(Vector3 position, string text, Color color, float size = 1f, float lifetime = 0.8f)
    {
        var textObject = new GameObject("Floating Text");
        textObject.transform.position = position;

        var floating = textObject.AddComponent<FloatingText>();
        floating.mesh = textObject.AddComponent<TextMesh>();
        floating.mesh.font = UIKit.Font;
        floating.mesh.fontSize = FontSize;
        floating.mesh.characterSize = CharacterSize * size;
        floating.mesh.anchor = TextAnchor.MiddleCenter;
        floating.mesh.alignment = TextAlignment.Center;
        floating.mesh.fontStyle = FontStyle.Bold;
        floating.mesh.text = text;
        floating.mesh.color = color;

        var renderer = textObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = UIKit.Font.material;
        renderer.sortingOrder = SortingOrder;

        floating.color = color;
        floating.lifetime = Mathf.Max(0.05f, lifetime);
        return floating;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }
        transform.position += Vector3.up * (RiseSpeed * Time.unscaledDeltaTime);
        // Hold full opacity for the first half, then fade.
        Color faded = color;
        faded.a *= Mathf.Clamp01(2f * (1f - age / lifetime));
        mesh.color = faded;
    }
}
