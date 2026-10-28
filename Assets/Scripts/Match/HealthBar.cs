using UnityEngine;

// A placeholder bar made of two square sprites. The fill shrinks toward the left edge.
public class HealthBar : MonoBehaviour
{
    [SerializeField] Transform fill;
    [SerializeField] float width = 0.8f;
    [SerializeField] bool hideWhenFull;

    public void SetFraction(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        fill.localScale = new Vector3(width * fraction, fill.localScale.y, 1f);
        fill.localPosition = new Vector3(-width * (1f - fraction) * 0.5f, fill.localPosition.y, 0f);
        gameObject.SetActive(!(hideWhenFull && fraction >= 1f));
    }
}
