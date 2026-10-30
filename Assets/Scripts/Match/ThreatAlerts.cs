using UnityEngine;
using UnityEngine.UI;

// Screen-level warnings: a red flash round the screen edge when an enemy reaches the base, and a
// banner when an elite or boss walks on. Added to the match object at runtime by MatchManager,
// like CameraShake. Runs in unscaled time so it plays through an ability's hit-stop.
public class ThreatAlerts : MonoBehaviour
{
    const float EdgeThickness = 36f;
    const float FlashSeconds = 0.6f;
    const float BannerSeconds = 2.5f;
    const float BannerFadeSeconds = 0.6f;
    static readonly Color FlashColor = new(0.9f, 0.1f, 0.1f, 0.55f);

    Image[] edges;
    Text banner;
    float flashUntil;
    float bannerUntil;

    void Awake()
    {
        Canvas canvas = UIKit.CreateCanvas("Threat Alerts", 15);
        canvas.transform.SetParent(transform, false);
        // Top, bottom, left and right strips, none of which catch clicks.
        edges = new Image[4];
        for (int i = 0; i < edges.Length; i++)
        {
            RectTransform strip = UIKit.Panel(canvas.transform, "Edge", FlashColor, false);
            Vector2 anchor = i switch { 0 => new Vector2(0.5f, 1f), 1 => new Vector2(0.5f, 0f), 2 => new Vector2(0f, 0.5f), _ => new Vector2(1f, 0.5f) };
            bool horizontal = i < 2;
            UIKit.Place(strip, anchor, Vector2.zero, horizontal ? new Vector2(UIKit.ReferenceWidth, EdgeThickness) : new Vector2(EdgeThickness, UIKit.ReferenceHeight));
            edges[i] = strip.GetComponent<Image>();
        }

        banner = UIKit.Shadowed(UIKit.Label(canvas.transform, "", 26, TextAnchor.MiddleCenter));
        banner.fontStyle = FontStyle.Bold;
        UIKit.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(UIKit.ReferenceWidth, 40f));
        SetAlphas(0f, 0f);
    }

    void OnEnable()
    {
        GameEvents.EnemyLeaked += OnEnemyLeaked;
        GameEvents.EnemyArrived += OnEnemyArrived;
    }

    void OnDisable()
    {
        GameEvents.EnemyLeaked -= OnEnemyLeaked;
        GameEvents.EnemyArrived -= OnEnemyArrived;
    }

    void OnEnemyLeaked(Enemy enemy) => flashUntil = Time.unscaledTime + FlashSeconds;

    void OnEnemyArrived(Enemy enemy)
    {
        banner.text = (enemy.IsBoss ? "BOSS: " : "ELITE: ") + enemy.Data.displayName;
        banner.color = enemy.IsBoss ? UIKit.BadColor : UIKit.AccentColor;
        bannerUntil = Time.unscaledTime + BannerSeconds;
    }

    void Update() => SetAlphas(Mathf.Clamp01((flashUntil - Time.unscaledTime) / FlashSeconds),
        Mathf.Clamp01((bannerUntil - Time.unscaledTime) / BannerFadeSeconds));

    void SetAlphas(float flash, float bannerAlpha)
    {
        foreach (Image edge in edges)
        {
            Color color = FlashColor;
            color.a *= flash;
            edge.color = color;
        }
        Color text = banner.color;
        text.a = bannerAlpha;
        banner.color = text;
    }
}
