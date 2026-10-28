using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds uGUI from code so screens need no prefabs or scene wiring. Widgets that change at runtime
// register a refresher (a small callback) in a list the owning screen runs every frame, which keeps
// labels and button states in step with the game without rebuilding the hierarchy.
public static class UIKit
{
    public const float ReferenceWidth = 960f;
    public const float ReferenceHeight = 540f;

    public static readonly Color PanelColor = new(0.07f, 0.08f, 0.11f, 0.9f);
    public static readonly Color ButtonColor = new(0.2f, 0.24f, 0.32f, 1f);
    public static readonly Color SelectedButtonColor = new(0.85f, 0.62f, 0.2f, 1f);
    public static readonly Color TextColor = new(0.93f, 0.92f, 0.86f, 1f);
    public static readonly Color MutedTextColor = new(0.62f, 0.64f, 0.68f, 1f);

    static Font font;
    public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var canvasObject = new GameObject(name);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 1f;
        canvasObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return canvas;
    }

    // uGUI clicks need an EventSystem; the project uses the Input System, so it gets that module.
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    public static bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    // Places a rect relative to its parent: anchor and pivot are the same corner or edge point,
    // offset moves it inwards from there, and size is in reference pixels.
    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static RectTransform Panel(Transform parent, string name, Color color, bool blocksClicks = true)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = blocksClicks;
        return (RectTransform)panel.transform;
    }

    // An invisible container, for grouping and layout only.
    public static RectTransform Group(Transform parent, string name)
    {
        var group = new GameObject(name, typeof(RectTransform));
        group.transform.SetParent(parent, false);
        return (RectTransform)group.transform;
    }

    public static T Layout<T>(Component target, int padding, float spacing, bool fitContent = false)
        where T : HorizontalOrVerticalLayoutGroup
    {
        var layout = target.gameObject.AddComponent<T>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        if (fitContent)
        {
            var fitter = target.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = typeof(T) == typeof(HorizontalLayoutGroup) ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        return layout;
    }

    // Preferred size for a layout child; a negative value leaves that axis to the content.
    public static LayoutElement Size(Component target, float width = -1f, float height = -1f, float flexibleWidth = -1f)
    {
        if (!target.TryGetComponent(out LayoutElement element)) element = target.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
        element.flexibleWidth = flexibleWidth;
        return element;
    }

    public static Text Label(Transform parent, string text, int size = 13, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
    {
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        var label = labelObject.GetComponent<Text>();
        label.font = Font;
        label.fontSize = size;
        label.alignment = anchor;
        label.color = color ?? TextColor;
        label.text = text;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    // A label whose text is recomputed every frame.
    public static Text LiveLabel(Transform parent, Func<string> text, List<Action> refreshers, int size = 13,
        TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
    {
        Text label = Label(parent, text(), size, anchor, color);
        refreshers.Add(() => label.text = text());
        return label;
    }

    public static Button Button(Transform parent, string text, Action onClick, float width = -1f, float height = 24f)
    {
        RectTransform rect = Panel(parent, "Button", ButtonColor);
        var button = rect.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        button.colors = colors;
        button.onClick.AddListener(() => onClick());
        Text label = Label(rect, text, 13, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        Size(rect, width, height);
        return button;
    }

    // A button whose label, enabled state and highlighted ("selected") state follow the game.
    public static Button LiveButton(Transform parent, Func<string> text, Action onClick, List<Action> refreshers,
        Func<bool> interactable = null, Func<bool> selected = null, float width = -1f, float height = 24f)
    {
        Button button = Button(parent, text(), onClick, width, height);
        Text label = button.GetComponentInChildren<Text>();
        var image = button.GetComponent<Image>();
        refreshers.Add(() =>
        {
            label.text = text();
            if (interactable != null) button.interactable = interactable();
            if (selected != null) image.color = selected() ? SelectedButtonColor : ButtonColor;
        });
        return button;
    }

    // Detaches before destroying so layouts stop counting the old children this frame.
    public static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    public static void Refresh(List<Action> refreshers)
    {
        foreach (Action refresh in refreshers) refresh();
    }
}
