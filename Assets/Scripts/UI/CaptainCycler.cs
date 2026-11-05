using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The captain cycler beside the mouse or the gamepad's tile cursor: the party captain a click
// places, flanked by the previous and next ones, each dimmed and tagged once on the field. The
// wheel or Q/E (the shoulder buttons) step it; PlacementController does the stepping and placing.
// Shown only over the battlefield, never over the HUD or a menu, and never blocks a click.
public class CaptainCycler
{
    // The 32 px portraits at 1x, so their pixels stay square.
    const float PortraitSize = 32f;
    const float SlotWidth = 52f;
    const float SelectedWidth = 84f;
    // Down and right of the pointer, clear of the hovered tile's frame.
    static readonly Vector2 PointerOffset = new(26f, -26f);
    static readonly Color BackColor = new(0.06f, 0.07f, 0.1f, 0.8f);
    static readonly Color PlacedTint = new(0.45f, 0.45f, 0.5f, 1f);

    readonly MatchManager match;
    readonly PlacementController placement;
    readonly RectTransform canvasRect;
    readonly RectTransform root;
    readonly Slot previous, selected, next;
    readonly Text leftKey, rightKey;

    class Slot
    {
        public RectTransform Root;
        public Image Portrait;
        public Text Caption;
    }

    public CaptainCycler(Canvas canvas, MatchManager match, PlacementController placement)
    {
        this.match = match;
        this.placement = placement;
        canvasRect = (RectTransform)canvas.transform;
        root = UIKit.Box(canvas.transform, "Captain Cycler", BackColor, blocksClicks: false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0f, 1f);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(root, 4, 4, fitContent: true);
        layout.childAlignment = TextAnchor.MiddleCenter;

        leftKey = KeyHint(PadGlyph.LeftShoulder, "Q");
        previous = MakeSlot(SlotWidth);
        selected = MakeSlot(SelectedWidth);
        next = MakeSlot(SlotWidth);
        rightKey = KeyHint(PadGlyph.RightShoulder, "E");

        Image ring = UIKit.Outline(selected.Root.GetChild(0), UIKit.AccentColor);
        ring.rectTransform.offsetMin = ring.rectTransform.offsetMax = Vector2.zero;
        selected.Caption.fontStyle = FontStyle.Bold;
        root.gameObject.SetActive(false);
    }

    // Q and E on the keyboard, the shoulder button icons on the gamepad.
    Text KeyHint(PadGlyph glyph, string key)
    {
        RectTransform holder = UIKit.Group(root, key);
        UIKit.Size(holder, 16f, PortraitSize);
        Text label = UIKit.Shadowed(UIKit.Label(holder, key, 12, TextAnchor.MiddleCenter, UIKit.AccentColor));
        UIKit.Stretch(label.rectTransform);
        UIKit.PadBadge(holder, glyph, () => GameInput.UsingGamepad, new Vector2(0.5f, 0.5f));
        return label;
    }

    Slot MakeSlot(float width)
    {
        RectTransform slot = UIKit.Group(root, "Slot");
        UIKit.Layout<VerticalLayoutGroup>(slot, 0, 2).childAlignment = TextAnchor.MiddleCenter;
        UIKit.Size(slot, width);
        RectTransform frame = UIKit.Box(slot, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0).childAlignment = TextAnchor.MiddleCenter;
        UIKit.Size(frame, PortraitSize + 4f, PortraitSize + 4f);
        Image portrait = UIKit.Icon(frame, null, PortraitSize);
        Text caption = UIKit.Shadowed(UIKit.Label(slot, "", 10, TextAnchor.MiddleCenter));
        caption.horizontalOverflow = HorizontalWrapMode.Overflow;
        return new Slot { Root = slot, Portrait = portrait, Caption = caption };
    }

    public void Update()
    {
        Captain current = match.SelectedCaptain;
        // Gone for good once the first wave starts: State leaves Preparing then and WavesCleared
        // stays above 0 after it.
        bool beforeFirstWave = match.State == MatchState.Preparing && match.WavesCleared == 0;
        bool shown = beforeFirstWave && current != null && placement.PointerOnField && !placement.IsMoving && !match.IsPaused;
        if (root.gameObject.activeSelf != shown) root.gameObject.SetActive(shown);
        if (!shown) return;

        var party = RunState.Current.Party;
        (int before, int after) = CaptainCycle.Neighbours(party, party.IndexOf(current));
        Fill(previous, before >= 0 ? party[before] : null, false);
        Fill(selected, current, true);
        Fill(next, after >= 0 ? party[after] : null, false);
        bool pad = GameInput.UsingGamepad;
        leftKey.enabled = rightKey.enabled = !pad;
        Follow(pad);
    }

    // Neighbours show only a portrait; the selected one adds their name, or that they're placed.
    static void Fill(Slot slot, Captain captain, bool isSelected)
    {
        slot.Root.gameObject.SetActive(captain != null || isSelected);
        if (captain == null) return;
        bool placed = MatchManager.IsOnField(captain);
        slot.Portrait.sprite = captain.Portrait;
        slot.Portrait.enabled = captain.Portrait != null;
        slot.Portrait.color = placed ? PlacedTint : Color.white;
        string text = isSelected ? (placed ? "On field" : captain.displayName) : placed ? "placed" : "";
        if (slot.Caption.text != text) slot.Caption.text = text;
        slot.Caption.color = placed ? UIKit.MutedTextColor : isSelected ? UIKit.AccentColor : UIKit.TextColor;
    }

    // Beside the mouse, or the gamepad's tile, kept inside the screen.
    void Follow(bool pad)
    {
        Camera cam = Camera.main;
        Vector2 screen = pad || Mouse.current == null
            ? (cam != null ? (Vector2)cam.WorldToScreenPoint(placement.PointerWorld) : Vector2.zero)
            : Mouse.current.position.ReadValue();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        Vector2 half = canvasRect.rect.size / 2f;
        Vector2 size = root.rect.size;
        Vector2 position = local + PointerOffset;
        // Flip to the other side of the pointer rather than run off the right or bottom edge.
        if (position.x + size.x > half.x) position.x = local.x - PointerOffset.x - size.x;
        if (position.y - size.y < -half.y) position.y = local.y - PointerOffset.y + size.y;
        root.anchoredPosition = position;
    }
}
