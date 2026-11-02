using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Gamepad focus for the uGUI screens. Screens are built from code and rebuilt freely, so nothing can
// wire navigation by hand, and Unity's automatic navigation searches every canvas, which would walk
// out of a window. So buttons turn navigation off (UIKit) and this steps between them by position,
// inside the top UIFocusScope. It picks a new button when the focused one goes away, keeps the
// focused button in view in scrolling lists, and rings it. Lives on the EventSystem (see UIKit).
public class UIFocus : MonoBehaviour
{
    const float RepeatDelay = 0.35f;
    const float RepeatInterval = 0.12f;
    const float RingMargin = 2f;
    // Share of a list scrolled per second at full stick.
    const float ScrollSpeed = 1.2f;

    static readonly Vector3[] Corners = new Vector3[4];

    readonly StepRepeat repeat = new(RepeatDelay, RepeatInterval);
    EventSystem events;
    Canvas ringCanvas;
    Image ring;
    UIFocusScope lastTop;
    string lastName;
    Vector2 lastPosition;
    Selectable kept;

    Selectable Current
    {
        get
        {
            GameObject selected = events.currentSelectedGameObject;
            return selected != null ? selected.GetComponent<Selectable>() : null;
        }
    }

    void Awake()
    {
        events = GetComponent<EventSystem>();
        // Over every screen; only hover tips, which show the focused button's text, sort higher.
        ringCanvas = UIKit.CreateCanvas("Focus Ring", 1000);
        ring = UIKit.Outline(ringCanvas.transform, Color.white);
        UIKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = Vector2.zero;
        ring.enabled = false;
    }

    void Update()
    {
        UIFocusScope top = UIFocusScope.Top;
        if (!GameInput.UsingGamepad || top == null)
        {
            // Nothing stays selected for the match, where the confirm button acts on the map.
            if (GameInput.UsingGamepad && events.currentSelectedGameObject != null) events.SetSelectedGameObject(null);
            lastTop = null;
            lastName = null;
            return;
        }
        if (GameInput.UiCancel)
        {
            top.OnCancel?.Invoke();
            return;
        }

        if (top != lastTop)
        {
            if (!top.Ready) return;
            Focus(Pick(top));
            lastTop = top;
            lastName = top.name;
        }
        else if (!CanFocus(Current, top)) Focus(Nearest(top, lastPosition));

        Selectable current = Current;
        Vector2Int step = repeat.Update(PadMath.ToStep(GameInput.PadMove), Time.unscaledTime);
        if (step != Vector2Int.zero && current != null)
        {
            Selectable next = InDirection(top, current, step);
            if (next != null) Focus(current = next);
        }
        ScrollWithStick(top);
        if (current == null) return;

        top.Remembered = current;
        lastPosition = Centre(current);
        if (current != kept) KeepInView(current);
        kept = current;
    }

    // After the screens have moved things this frame.
    void LateUpdate()
    {
        Selectable current = GameInput.UsingGamepad && UIFocusScope.Top != null ? Current : null;
        ring.enabled = current != null;
        if (current == null) return;
        GetBounds(current, out Vector2 min, out Vector2 max);
        Vector2 size = (max - min) / ringCanvas.scaleFactor + Vector2.one * (2f * RingMargin);
        ring.rectTransform.anchoredPosition = (min + max) / 2f / ringCanvas.scaleFactor;
        ring.rectTransform.sizeDelta = size;
    }

    void Focus(Selectable selectable)
    {
        GameObject target = selectable != null ? selectable.gameObject : null;
        if (events.currentSelectedGameObject != target) events.SetSelectedGameObject(target);
    }

    // A rebuilt screen keeps focus where it was, so stepping a setting doesn't jump to the top; a
    // window closing hands it back; anything new starts at its default or first button.
    Selectable Pick(UIFocusScope top)
    {
        if (lastName == top.name) return Nearest(top, lastPosition);
        if (CanFocus(top.Remembered, top)) return top.Remembered;
        Selectable preferred = top.Default?.Invoke();
        return CanFocus(preferred, top) ? preferred : First(top);
    }

    // Dismiss layers and window backs are buttons too, but with no transition; scrollbars are left to
    // the right stick. Closed drawers wait off screen.
    static bool CanFocus(Selectable selectable, UIFocusScope scope)
    {
        if (selectable == null || selectable is not Button || selectable.transition == Selectable.Transition.None) return false;
        if (!selectable.IsActive() || !selectable.IsInteractable() || !selectable.transform.IsChildOf(scope.Root)) return false;
        Vector2 centre = Centre(selectable);
        return centre.x >= 0f && centre.y >= 0f && centre.x <= Screen.width && centre.y <= Screen.height;
    }

    static Selectable First(UIFocusScope scope)
    {
        Selectable best = null;
        foreach (Selectable selectable in Selectable.allSelectablesArray)
        {
            if (!CanFocus(selectable, scope)) continue;
            if (best == null || IsBefore(Centre(selectable), Centre(best))) best = selectable;
        }
        return best;
    }

    // Reading order: higher on the screen first, then further left.
    static bool IsBefore(Vector2 a, Vector2 b) => !Mathf.Approximately(a.y, b.y) ? a.y > b.y : a.x < b.x;

    static Selectable Nearest(UIFocusScope scope, Vector2 position)
    {
        Selectable best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (Selectable selectable in Selectable.allSelectablesArray)
        {
            if (!CanFocus(selectable, scope)) continue;
            float distance = (Centre(selectable) - position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            best = selectable;
            bestDistance = distance;
        }
        return best;
    }

    static Selectable InDirection(UIFocusScope scope, Selectable from, Vector2Int step)
    {
        Selectable best = null;
        float bestCost = float.PositiveInfinity;
        Vector2 origin = Centre(from);
        foreach (Selectable selectable in Selectable.allSelectablesArray)
        {
            if (selectable == from || !CanFocus(selectable, scope)) continue;
            float cost = PadMath.StepCost(origin, Centre(selectable), step);
            if (cost >= bestCost) continue;
            best = selectable;
            bestCost = cost;
        }
        return best;
    }

    static void ScrollWithStick(UIFocusScope scope)
    {
        float amount = GameInput.PadScroll;
        if (amount == 0f) return;
        ScrollRect list = scope.Root.GetComponentInChildren<ScrollRect>();
        if (list == null) return;
        list.verticalNormalizedPosition = Mathf.Clamp01(list.verticalNormalizedPosition + amount * ScrollSpeed * Time.unscaledDeltaTime);
    }

    // Moves a scrolling list just far enough to show the widget. Overlay canvases draw in pixels,
    // so world positions are screen positions here.
    static void KeepInView(Selectable selectable)
    {
        ScrollRect list = selectable.GetComponentInParent<ScrollRect>();
        if (list == null) return;
        GetBounds(selectable, out Vector2 min, out Vector2 max);
        list.viewport.GetWorldCorners(Corners);
        float bottom = Corners[0].y, top = Corners[1].y;
        float shift = max.y > top ? top - max.y : min.y < bottom ? bottom - min.y : 0f;
        if (shift == 0f) return;
        list.StopMovement();
        list.content.position += Vector3.up * shift;
    }

    static Vector2 Centre(Selectable selectable)
    {
        GetBounds(selectable, out Vector2 min, out Vector2 max);
        return (min + max) / 2f;
    }

    static void GetBounds(Selectable selectable, out Vector2 min, out Vector2 max)
    {
        ((RectTransform)selectable.transform).GetWorldCorners(Corners);
        min = Corners[0];
        max = Corners[2];
    }
}
