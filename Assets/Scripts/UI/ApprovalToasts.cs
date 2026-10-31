using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Approval changes as toasts down the top right of the match screen: the captain's portrait, what
// they thought and the change. Each slides in from the right, holds for a few seconds, then slides
// back out to the right. Runs in unscaled time so it keeps moving through an ability's hit-stop.
public class ApprovalToasts
{
    public const float Width = 240f;
    const float Height = 44f;
    const int MaxVisible = 5;
    const float SlideInSeconds = 0.3f;
    const float HoldSeconds = 5.4f;
    const float SlideOutSeconds = 0.35f;
    // How quickly the stack closes up when a toast above leaves.
    const float RestackSpeed = 14f;
    // The 32 px portraits at 1x, so their pixels stay square.
    const float PortraitSize = 32f;
    static readonly Color GainTint = new(0.5f, 0.84f, 0.45f, 0.55f);
    static readonly Color LossTint = new(0.94f, 0.42f, 0.38f, 0.55f);

    class Toast
    {
        public RectTransform rect;
        public float age;
        public float y;
    }

    readonly RectTransform stack;
    readonly List<Toast> toasts = new();

    public ApprovalToasts(Transform parent, float top)
    {
        stack = UIKit.Group(parent, "Approval Toasts");
        UIKit.Place(stack, new Vector2(1f, 1f), new Vector2(-UIKit.Pad, -top), new Vector2(Width, 0f));
    }

    // relationships is null until the match adds it, so it is passed each frame.
    public void Update(Relationships relationships)
    {
        float dt = Time.unscaledDeltaTime;
        if (relationships != null)
            while (relationships.TryTakeNotice(out ApprovalNotice notice))
                Add(notice);

        // Over the limit, the oldest start leaving at once.
        for (int i = 0; i < toasts.Count - MaxVisible; i++)
            toasts[i].age = Mathf.Max(toasts[i].age, SlideInSeconds + HoldSeconds);

        float slot = 0f;
        for (int i = 0; i < toasts.Count; i++)
        {
            Toast toast = toasts[i];
            toast.age += dt;
            if (toast.age >= SlideInSeconds + HoldSeconds + SlideOutSeconds)
            {
                Object.Destroy(toast.rect.gameObject);
                toasts.RemoveAt(i--);
                continue;
            }
            toast.y = Mathf.Lerp(toast.y, slot, 1f - Mathf.Exp(-RestackSpeed * dt));
            toast.rect.anchoredPosition = new Vector2(OffsetX(toast.age), -toast.y);
            slot += Height + UIKit.Gap;
        }
    }

    // How far right of its resting place a toast is: off the screen edge before it slides in and
    // after it slides out.
    static float OffsetX(float age)
    {
        float away = Width + UIKit.Pad;
        if (age < SlideInSeconds)
        {
            float t = 1f - age / SlideInSeconds;
            return away * t * t * t;
        }
        float leaving = age - SlideInSeconds - HoldSeconds;
        if (leaving <= 0f) return 0f;
        float u = Mathf.Clamp01(leaving / SlideOutSeconds);
        return away * u * u * u;
    }

    void Add(ApprovalNotice notice)
    {
        bool gain = notice.amount > 0f;
        RectTransform card = UIKit.Box(stack, notice.captain.displayName, UIKit.PanelColor, blocksClicks: false);
        UIKit.Place(card, new Vector2(1f, 1f), Vector2.zero, new Vector2(Width, Height));
        UIKit.Layout<HorizontalLayoutGroup>(card, 5, 6);
        UIKit.Outline(card, gain ? GainTint : LossTint);

        RectTransform frame = UIKit.Box(card, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, notice.captain.portrait, PortraitSize);

        RectTransform info = UIKit.Group(card, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 1);
        UIKit.Size(info, flexibleWidth: 1f);
        Text name = UIKit.Shadowed(UIKit.Label(info, notice.captain.displayName, 12));
        name.fontStyle = FontStyle.Bold;
        UIKit.Shadowed(UIKit.Label(info, Capitalised(notice.reason), 11, TextAnchor.MiddleLeft, UIKit.MutedTextColor));

        Text amount = UIKit.Shadowed(UIKit.Label(card, notice.amount.ToString("+0.#;-0.#"), 16, TextAnchor.MiddleRight,
            gain ? UIKit.GoodColor : UIKit.BadColor));
        amount.fontStyle = FontStyle.Bold;
        UIKit.Size(amount, 40f);

        // Starts fully off to the right and in its slot, so it never flashes at the wrong place.
        var toast = new Toast { rect = card, y = toasts.Count * (Height + UIKit.Gap) };
        card.anchoredPosition = new Vector2(OffsetX(0f), -toast.y);
        toasts.Add(toast);
    }

    static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text.Substring(1);
}
