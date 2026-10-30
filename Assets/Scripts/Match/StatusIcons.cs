using UnityEngine;

// A row of small icons above an enemy, one per active status, each with a bar under it that
// shrinks as the status runs out. Built at runtime so the enemy prefab needs no change.
public class StatusIcons : MonoBehaviour
{
    const float IconSize = 0.2f;
    const float IconSpacing = 0.24f;
    const float TimerHeight = 0.04f;
    const float TimerGap = 0.03f;
    static readonly Color FrameColor = new(0.05f, 0.06f, 0.09f, 0.9f);
    static readonly char[] Letters = { 'S', 'B', 'T', 'D' };

    class Icon
    {
        public GameObject root;
        public Transform timer;
    }

    readonly Icon[] icons = new Icon[StatusRules.KindCount];

    public static StatusIcons Create(Transform parent, Vector3 localPosition, int sortingOrder)
    {
        var row = new GameObject("Status Icons");
        row.transform.SetParent(parent, false);
        row.transform.localPosition = localPosition;
        var statusIcons = row.AddComponent<StatusIcons>();
        for (int i = 0; i < statusIcons.icons.Length; i++)
            statusIcons.icons[i] = statusIcons.MakeIcon((StatusKind)i, sortingOrder);
        return statusIcons;
    }

    Icon MakeIcon(StatusKind kind, int sortingOrder)
    {
        var icon = new Icon { root = new GameObject(kind + " Icon") };
        icon.root.transform.SetParent(transform, false);

        AddSquare(icon.root.transform, "Frame", Vector3.zero, new Vector3(IconSize + 0.04f, IconSize + 0.04f, 1f), FrameColor, sortingOrder);
        AddSquare(icon.root.transform, "Fill", Vector3.zero, Vector3.one * IconSize, StatusRules.TintOf(kind), sortingOrder + 1);

        var letter = new GameObject("Letter");
        letter.transform.SetParent(icon.root.transform, false);
        var mesh = letter.AddComponent<TextMesh>();
        mesh.font = UIKit.Font;
        mesh.fontSize = 48;
        mesh.characterSize = 0.0035f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.fontStyle = FontStyle.Bold;
        mesh.text = Letters[(int)kind].ToString();
        mesh.color = FrameColor;
        var meshRenderer = letter.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = UIKit.Font.material;
        meshRenderer.sortingOrder = sortingOrder + 2;

        float timerY = -(IconSize * 0.5f + TimerGap + TimerHeight * 0.5f);
        icon.timer = AddSquare(icon.root.transform, "Timer", new Vector3(0f, timerY, 0f),
            new Vector3(IconSize, TimerHeight, 1f), StatusRules.TintOf(kind), sortingOrder + 1);
        icon.root.SetActive(false);
        return icon;
    }

    static Transform AddSquare(Transform parent, string name, Vector3 position, Vector3 scale, Color color, int sortingOrder)
    {
        var square = new GameObject(name);
        square.transform.SetParent(parent, false);
        square.transform.localPosition = position;
        square.transform.localScale = scale;
        var renderer = square.AddComponent<SpriteRenderer>();
        renderer.sprite = SpriteUtil.Square;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return square.transform;
    }

    // Shows the icons for statuses with time left, centred side by side. The timer bar is
    // anchored to the icon's left edge and shrinks toward it.
    public void Refresh(float[] statusTime)
    {
        bool show = GameSettings.StatusIcons;
        int active = 0;
        for (int i = 0; i < icons.Length; i++)
            if (show && statusTime[i] > 0f) active++;

        int slot = 0;
        for (int i = 0; i < icons.Length; i++)
        {
            bool on = show && statusTime[i] > 0f;
            icons[i].root.SetActive(on);
            if (!on) continue;
            icons[i].root.transform.localPosition = new Vector3((slot++ - (active - 1) * 0.5f) * IconSpacing, 0f, 0f);
            float fraction = Mathf.Clamp01(statusTime[i] / StatusRules.DurationOf((StatusKind)i));
            Vector3 scale = icons[i].timer.localScale;
            icons[i].timer.localScale = new Vector3(IconSize * fraction, scale.y, 1f);
            Vector3 position = icons[i].timer.localPosition;
            icons[i].timer.localPosition = new Vector3(-IconSize * (1f - fraction) * 0.5f, position.y, 0f);
        }
    }
}
