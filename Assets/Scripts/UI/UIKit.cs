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

    // Spacing scale, in reference pixels: gaps between widgets, and padding inside panels.
    public const int Gap = 4;
    public const int Pad = 8;

    public static readonly Color PanelColor = new(0.1f, 0.11f, 0.16f, 0.97f);
    public static readonly Color InsetColor = new(0.05f, 0.06f, 0.09f, 1f);
    public static readonly Color ButtonColor = new(0.24f, 0.29f, 0.4f, 1f);
    public static readonly Color SelectedButtonColor = new(0.91f, 0.67f, 0.24f, 1f);
    public static readonly Color TextColor = new(0.95f, 0.93f, 0.86f, 1f);
    public static readonly Color MutedTextColor = new(0.66f, 0.68f, 0.73f, 1f);
    public static readonly Color DisabledTextColor = new(0.5f, 0.52f, 0.57f, 1f);
    // Dark text for the gold "selected" fill, which cream text can't be read on.
    public static readonly Color SelectedTextColor = new(0.14f, 0.1f, 0.05f, 1f);
    public static readonly Color AccentColor = new(0.96f, 0.76f, 0.36f, 1f);
    public static readonly Color GoodColor = new(0.5f, 0.84f, 0.45f, 1f);
    public static readonly Color BadColor = new(0.94f, 0.42f, 0.38f, 1f);
    public static readonly Color RuleColor = new(0.3f, 0.34f, 0.44f, 1f);

    public static readonly Color HealthBarColor = new(0.36f, 0.78f, 0.38f, 1f);
    public static readonly Color ReserveBarColor = new(0.9f, 0.7f, 0.25f, 1f);
    public static readonly Color XpBarColor = new(0.38f, 0.58f, 0.95f, 1f);
    public static readonly Color ChargeBarColor = new(0.62f, 0.45f, 0.95f, 1f);

    static Font font;
    public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static Color ElementColor(Element element) => element switch
    {
        Element.Fire => new Color(1f, 0.58f, 0.36f, 1f),
        Element.Water => new Color(0.45f, 0.72f, 1f, 1f),
        Element.Grass => new Color(0.55f, 0.85f, 0.42f, 1f),
        _ => new Color(0.7f, 0.92f, 1f, 1f),
    };

    // Common white, Uncommon green, Rare purple, Epic red, Legendary gold.
    public static Color RarityColor(SoldierRarity rarity) => rarity switch
    {
        SoldierRarity.Uncommon => new Color(0.42f, 0.88f, 0.42f, 1f),
        SoldierRarity.Rare => new Color(0.74f, 0.52f, 1f, 1f),
        SoldierRarity.Epic => new Color(1f, 0.4f, 0.36f, 1f),
        SoldierRarity.Legendary => new Color(1f, 0.82f, 0.26f, 1f),
        _ => new Color(0.95f, 0.95f, 0.95f, 1f),
    };

    // Rich-text colour tag for a colour, for mixing colours inside one label.
    public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

    // ---------- Pixel-art frames ----------

    // Panels and buttons use tiny 9-sliced sprites drawn here rather than imported, so the UI
    // needs no art assets. Each texel is drawn TexelSize reference pixels wide, which lands on
    // whole screen pixels at 1080p and up, like the 16 PPU world art.
    const float TexelSize = 2f;
    static Sprite boxSprite, buttonSprite, outlineSprite;

    // A dark outline, a light top rim and a shaded bottom rim around a slightly dimmed fill.
    static Sprite BoxSprite => boxSprite != null ? boxSprite : boxSprite = MakeSprite(new[]
    {
        "........",
        ".WWWWWW.",
        ".W####S.",
        ".W####S.",
        ".W####S.",
        ".W####S.",
        ".SSSSSS.",
        "........",
    }, 2);

    // Like the box, with a thicker shaded lip at the bottom so buttons read as raised.
    static Sprite ButtonSprite => buttonSprite != null ? buttonSprite : buttonSprite = MakeSprite(new[]
    {
        "........",
        ".WWWWWW.",
        ".W####W.",
        ".W####W.",
        ".W####W.",
        ".SSSSSS.",
        ".SSSSSS.",
        "........",
    }, 3);

    // A one-texel ring with an empty centre, laid over a widget to mark it as selected.
    static Sprite OutlineSprite => outlineSprite != null ? outlineSprite : outlineSprite = MakeSprite(new[]
    {
        "WWWW",
        "W  W",
        "W  W",
        "WWWW",
    }, 1);

    // Rows run top to bottom. '.' is the outline, 'W' white, '#' the fill, 'S' shade, ' ' clear.
    static Sprite MakeSprite(string[] rows, int border)
    {
        int size = rows.Length;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            texture.SetPixel(x, size - 1 - y, rows[y][x] switch
            {
                '.' => new Color(0.02f, 0.02f, 0.04f, 1f),
                'W' => Color.white,
                '#' => new Color(0.84f, 0.84f, 0.84f, 1f),
                'S' => new Color(0.62f, 0.62f, 0.62f, 1f),
                _ => Color.clear,
            });
        }
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    static void UseSprite(Image image, Sprite sprite, bool fillCenter = true)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.fillCenter = fillCenter;
        // Canvas reference pixels per unit is 100, like the sprite, so 1 texel is 1 reference
        // pixel before this.
        image.pixelsPerUnitMultiplier = 1f / TexelSize;
    }

    // Pixel art drawn from rows of text: 'X' is the main colour, 'D' the dark one, anything else clear.
    // Show it with Icon at a whole multiple of the row size.
    public static Sprite PixelSprite(string[] rows, Color main, Color dark)
    {
        var texture = new Texture2D(rows[0].Length, rows.Length, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        for (int y = 0; y < rows.Length; y++)
        for (int x = 0; x < rows[y].Length; x++)
            texture.SetPixel(x, rows.Length - 1 - y, rows[y][x] == 'X' ? main : rows[y][x] == 'D' ? dark : Color.clear);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // uiScale above 1 makes everything on the canvas larger, which also shrinks the layout area.
    // Expand keeps the whole reference area on screen: a screen that isn't 16:9, like the Steam
    // Deck's 16:10, gets extra canvas on one axis instead of cropping the other.
    public static Canvas CreateCanvas(string name, int sortingOrder, float uiScale = 1f)
    {
        var canvasObject = new GameObject(name);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight) / uiScale;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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
        eventSystem.AddComponent<UIFocus>();
    }

    // Makes a screen, window or drawer a layer of gamepad focus (see UIFocusScope). Focus stays under
    // root, or under host when there is none; onCancel runs on the gamepad's cancel button.
    public static UIFocusScope Focus(Component host, Action onCancel = null, Transform root = null, float delay = 0f)
    {
        var scope = host.gameObject.AddComponent<UIFocusScope>();
        scope.Root = root != null ? root : host.transform;
        scope.OnCancel = onCancel;
        scope.Delay = delay;
        return scope;
    }

    // UIFocus moves focus itself, so Unity's automatic navigation stays out of its way.
    static void NoNavigation(Selectable selectable) => selectable.navigation = new Navigation { mode = Navigation.Mode.None };

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

    // A panel with a pixel-art frame, for windows and cards.
    public static RectTransform Box(Transform parent, string name, Color? color = null, bool blocksClicks = true)
    {
        RectTransform box = Panel(parent, name, color ?? PanelColor, blocksClicks);
        UseSprite(box.GetComponent<Image>(), BoxSprite);
        return box;
    }

    // A sprite drawn as painted at a fixed size. Pick sizes that are whole multiples of the art
    // (16 or 32 for a 16 px icon) so its pixels stay square.
    public static Image Icon(Transform parent, Sprite sprite, float size)
    {
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(parent, false);
        var image = icon.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        Size(image, size, size);
        return image;
    }

    // A ring laid over a widget's edges; colour it to mark selection, or clear to hide it.
    public static Image Outline(Transform parent, Color color)
    {
        RectTransform ring = Stretch(Panel(parent, "Outline", color, blocksClicks: false));
        var image = ring.GetComponent<Image>();
        UseSprite(image, OutlineSprite, fillCenter: false);
        ring.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return image;
    }

    // A soft drop shadow, for text drawn over the world or a busy background.
    public static T Shadowed<T>(T graphic) where T : Graphic
    {
        var shadow = graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return graphic;
    }

    // A section title in small accent capitals with a rule running to the right edge.
    public static RectTransform Header(Transform parent, string text)
    {
        RectTransform row = Group(parent, "Header");
        HorizontalLayoutGroup layout = Layout<HorizontalLayoutGroup>(row, 0, 6);
        layout.padding.top = Gap;
        Label(row, text.ToUpperInvariant(), 10, color: AccentColor).fontStyle = FontStyle.Bold;
        RectTransform rule = Panel(row, "Rule", RuleColor, blocksClicks: false);
        Size(rule, height: TexelSize, flexibleWidth: 1f);
        return row;
    }

    // A meter: an inset track, a fill that follows fraction (0 to 1), and centred text over it.
    public static RectTransform LiveBar(Transform parent, Func<float> fraction, Func<string> text, Color color,
        List<Action> refreshers, float height = 14f)
    {
        RectTransform track = Box(parent, "Bar", InsetColor, blocksClicks: false);
        Size(track, height: height, flexibleWidth: 1f);
        RectTransform fill = Panel(track, "Fill", color, blocksClicks: false);
        fill.anchorMin = Vector2.zero;
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = new Vector2(TexelSize, TexelSize);
        fill.offsetMax = new Vector2(-TexelSize, -TexelSize);
        Text label = Shadowed(Label(track, text(), 10, TextAnchor.MiddleCenter));
        Stretch(label.rectTransform);
        refreshers.Add(() =>
        {
            float amount = Mathf.Clamp01(fraction());
            fill.anchorMax = new Vector2(amount, 1f);
            fill.gameObject.SetActive(amount > 0f);
            label.text = text();
        });
        return track;
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
        UseSprite(rect.GetComponent<Image>(), ButtonSprite);
        var button = rect.gameObject.AddComponent<Button>();
        NoNavigation(button);
        var colors = button.colors;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        // Opaque, so a disabled button doesn't let the map show through; the label greys out too.
        colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            AudioManager.Play(AudioKeys.UiClick);
            onClick();
        });
        Text label = Label(rect, text, 13, TextAnchor.MiddleCenter);
        // The bottom lip is part of the button, so the label sits on the face above it.
        Stretch(label.rectTransform).offsetMin = new Vector2(Gap, TexelSize * 2f);
        label.rectTransform.offsetMax = new Vector2(-Gap, 0f);
        Size(rect, width, height);
        return button;
    }

    // A button whose label, enabled state and highlighted ("selected") state follow the game.
    public static Button LiveButton(Transform parent, Func<string> text, Action onClick, List<Action> refreshers,
        Func<bool> interactable = null, Func<bool> selected = null, float width = 100f, float height = 24f)
    {
        Button button = Button(parent, text(), onClick, width, height);
        Text label = button.GetComponentInChildren<Text>();
        var image = button.GetComponent<Image>();
        refreshers.Add(() =>
        {
            label.text = text();
            if (interactable != null) button.interactable = interactable();
            bool isSelected = selected != null && selected();
            if (selected != null) image.color = isSelected ? SelectedButtonColor : ButtonColor;
            label.color = isSelected ? SelectedTextColor : button.interactable ? TextColor : DisabledTextColor;
        });
        return button;
    }

    // Hangs a gamepad button's icon on a widget, by default across its left edge so it takes no
    // space from the label. A button with no way to be pressed right now hides its icon by itself.
    public static PadIcon PadBadge(Component target, PadGlyph glyph, Func<bool> shown = null, Vector2? anchor = null, Vector2 offset = default)
    {
        Image image = Icon(target.transform, null, PadIcons.Size);
        image.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0f, 0.5f);
        rect.sizeDelta = Vector2.one * PadIcons.Size;
        rect.anchoredPosition = offset;
        var icon = image.gameObject.AddComponent<PadIcon>();
        icon.Glyph = glyph;
        icon.Shown = shown;
        icon.Widget = target.GetComponent<Selectable>();
        icon.Image = image;
        icon.Refresh();
        return icon;
    }

    // Makes a panel (a card, say) clickable, with the same hover and press tints as buttons.
    public static Button Clickable(RectTransform panel, Action onClick)
    {
        var image = panel.GetComponent<Image>();
        image.raycastTarget = true;
        var button = panel.gameObject.AddComponent<Button>();
        NoNavigation(button);
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            AudioManager.Play(AudioKeys.UiClick);
            onClick();
        });
        return button;
    }

    // A box above target with a bold title and a muted description, shown while the pointer is on
    // it, or while the gamepad focuses it. It has its own sorting override, so it draws over every
    // other widget on the screen.
    public static RectTransform HoverTip(RectTransform target, string title, string description, float width = 220f)
    {
        RectTransform tip = Box(target, "Hover Tip", blocksClicks: false);
        tip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var tipCanvas = tip.gameObject.AddComponent<Canvas>();
        tipCanvas.overrideSorting = true;
        tipCanvas.sortingOrder = short.MaxValue;
        tip.anchorMin = tip.anchorMax = new Vector2(0.5f, 1f);
        tip.pivot = new Vector2(0.5f, 0f);
        tip.sizeDelta = new Vector2(width, 0f);
        tip.anchoredPosition = new Vector2(0f, Gap);
        Layout<VerticalLayoutGroup>(tip, Pad, 2, fitContent: true);
        Label(tip, title, 13).fontStyle = FontStyle.Bold;
        if (!string.IsNullOrEmpty(description)) Label(tip, description, 11, color: MutedTextColor);
        tip.gameObject.SetActive(false);

        var trigger = target.gameObject.AddComponent<EventTrigger>();
        AddHover(EventTriggerType.PointerEnter, () => true);
        AddHover(EventTriggerType.PointerExit, () => false);
        // A mouse click selects too, and that shouldn't leave the tip up after the pointer goes.
        AddHover(EventTriggerType.Select, () => GameInput.UsingGamepad);
        AddHover(EventTriggerType.Deselect, () => false);
        void AddHover(EventTriggerType type, Func<bool> show)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => tip.gameObject.SetActive(show()));
            trigger.triggers.Add(entry);
        }
        return tip;
    }

    // Marks a button as the screen's main action: gold while it can be pressed.
    public static Button Primary(Button button, List<Action> refreshers)
    {
        var image = button.GetComponent<Image>();
        Text label = button.GetComponentInChildren<Text>();
        label.fontStyle = FontStyle.Bold;
        void Apply()
        {
            image.color = button.interactable ? SelectedButtonColor : ButtonColor;
            label.color = button.interactable ? SelectedTextColor : DisabledTextColor;
        }
        Apply();
        refreshers.Add(Apply);
        return button;
    }

    // A vertically scrolling list with a scrollbar on the right. Size the returned ScrollRect
    // like any layout child, and add rows to its content, which grows to fit them.
    public static ScrollRect ScrollList(Transform parent, string name, float spacing)
    {
        const float barWidth = 10f;
        RectTransform root = Group(parent, name);
        var scroll = root.gameObject.AddComponent<ScrollRect>();

        RectTransform viewport = Stretch(Panel(root, "Viewport", Color.clear));
        viewport.offsetMax = new Vector2(-barWidth - Gap, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Group(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        Layout<VerticalLayoutGroup>(content, 0, spacing, fitContent: true);

        RectTransform track = Box(root, "Scrollbar", InsetColor);
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(barWidth, 0f);
        RectTransform slidingArea = Stretch(Group(track, "Sliding Area"));
        RectTransform handle = Stretch(Panel(slidingArea, "Handle", MutedTextColor));
        UseSprite(handle.GetComponent<Image>(), ButtonSprite);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle;
        bar.targetGraphic = handle.GetComponent<Image>();
        var colors = bar.colors;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        bar.colors = colors;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        // The Input System UI module reports about 6 per wheel notch; this makes a notch a row or so.
        scroll.scrollSensitivity = 8f;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return scroll;
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
