using UnityEngine;

// A placeholder bar made of two square sprites. The fill shrinks toward the left edge.
// Enemies switch on UseEnemyStyle: the bar only shows once damaged, a white ghost segment
// drains behind the fill, and elites and bosses get a wider bar with a marker at its left end.
public class HealthBar : MonoBehaviour
{
    // The ghost holds for this long after a hit, then drains at this fraction of the bar per second.
    const float GhostHoldSeconds = 0.25f;
    const float GhostDrainPerSecond = 1.5f;
    const float EliteScale = 1.4f;
    const float BossScale = 1.8f;
    const float MarkerSize = 0.14f;
    static readonly Color GhostColor = new(1f, 1f, 1f, 0.9f);
    static readonly Color EliteMarkerColor = new(1f, 0.82f, 0.25f);
    static readonly Color BossMarkerColor = new(0.9f, 0.2f, 0.2f);

    [SerializeField] Transform fill;
    [SerializeField] float width = 0.8f;
    [SerializeField] bool hideWhenFull;

    bool enemyStyle;
    Transform ghost;
    Transform marker;
    SpriteRenderer markerRenderer;
    float fraction = 1f;
    float ghostFraction = 1f;
    float ghostHold;

    // Hides the bar at full health, adds the draining ghost, and sizes it for a tier:
    // 0 normal, 1 elite, 2 boss.
    public void UseEnemyStyle(int tier)
    {
        if (!enemyStyle)
        {
            enemyStyle = true;
            hideWhenFull = true;
            var fillRenderer = fill.GetComponent<SpriteRenderer>();
            ghost = new GameObject("Ghost").transform;
            ghost.SetParent(fill.parent, false);
            var ghostRenderer = ghost.gameObject.AddComponent<SpriteRenderer>();
            ghostRenderer.sprite = fillRenderer.sprite;
            ghostRenderer.color = GhostColor;
            // The ghost sits between the back and the fill, which moves up one to make room.
            ghostRenderer.sortingOrder = fillRenderer.sortingOrder;
            fillRenderer.sortingOrder++;
            marker = new GameObject("Tier Marker").transform;
            marker.SetParent(fill.parent, false);
            markerRenderer = marker.gameObject.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = fillRenderer.sprite;
            markerRenderer.sortingOrder = fillRenderer.sortingOrder;
            UpdateGhost();
        }
        SetTier(tier);
    }

    public void SetTier(int tier)
    {
        float scale = tier >= 2 ? BossScale : tier == 1 ? EliteScale : 1f;
        transform.localScale = new Vector3(scale, scale, 1f);
        marker.gameObject.SetActive(tier > 0);
        markerRenderer.color = tier >= 2 ? BossMarkerColor : EliteMarkerColor;
        // A diamond just off the left end of the bar.
        marker.localPosition = new Vector3(-width * 0.5f - MarkerSize * 0.9f, fill.localPosition.y, 0f);
        marker.localScale = Vector3.one * MarkerSize * (tier >= 2 ? 1.3f : 1f);
        marker.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    public void SetFraction(float value)
    {
        fraction = Mathf.Clamp01(value);
        fill.localScale = new Vector3(width * fraction, fill.localScale.y, 1f);
        fill.localPosition = new Vector3(-width * (1f - fraction) * 0.5f, fill.localPosition.y, 0f);
        // Off, an enemy bar is a plain always-visible bar (the tier marker stays).
        bool extras = !enemyStyle || GameSettings.HealthBarExtras;
        gameObject.SetActive(!(hideWhenFull && extras && fraction >= 1f));
        if (!enemyStyle) return;
        ghost.gameObject.SetActive(extras);
        if (!extras) ghostFraction = fraction;
        if (fraction < ghostFraction) ghostHold = GhostHoldSeconds;
        else ghostFraction = fraction;
        UpdateGhost();
    }

    // Unscaled, so the ghost keeps draining through an ability's hit-stop.
    void Update()
    {
        if (!enemyStyle || ghostFraction <= fraction) return;
        if (ghostHold > 0f)
        {
            ghostHold -= Time.unscaledDeltaTime;
            return;
        }
        ghostFraction = Mathf.Max(fraction, ghostFraction - GhostDrainPerSecond * Time.unscaledDeltaTime);
        UpdateGhost();
    }

    void UpdateGhost()
    {
        ghost.localScale = new Vector3(width * ghostFraction, fill.localScale.y, 1f);
        ghost.localPosition = new Vector3(-width * (1f - ghostFraction) * 0.5f, fill.localPosition.y, 0f);
    }
}
