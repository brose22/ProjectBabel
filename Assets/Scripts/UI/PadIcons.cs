using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Gamepad buttons by position, as the Input System names them: South is the bottom face button
// whatever it is labelled, so the art for a Nintendo pad is what swaps A and B.
public enum PadGlyph
{
    South, East, West, North,
    LeftShoulder, RightShoulder, LeftTrigger, RightTrigger,
    LeftStick, RightStick, LeftStickPress, RightStickPress,
    View, Menu,
}

// The sets of button art. Unity can't tell apart any other pads the art covers, so they show Xbox.
public enum PadFamily { Xbox, PlayStation, Switch, SteamDeck }

// Button art for the gamepad in hand, from the PadIconLibrary. Show it at Size reference pixels.
public static class PadIcons
{
    public const int Size = 20;

    static PadIconLibrary library;

    public static Sprite Get(PadGlyph glyph)
    {
        if (library == null) library = Resources.Load<PadIconLibrary>(nameof(PadIconLibrary));
        return library != null ? library.Sprite(GameInput.PadFamily, glyph) : null;
    }
}

// A gamepad button drawn on a widget, showing what that button does. Only visible while the gamepad
// is the active device, and it follows the gamepad in hand. Added with UIKit.PadBadge.
public class PadIcon : MonoBehaviour
{
    static readonly List<PadIcon> active = new();

    public PadGlyph Glyph { get; set; }
    // Extra condition for showing, if any.
    public Func<bool> Shown { get; set; }
    // A disabled button can't be pressed, so it shows no icon.
    public Selectable Widget { get; set; }
    public Image Image { get; set; }

    public void Refresh()
    {
        bool show = GameInput.UsingGamepad && (Widget == null || Widget.interactable) && (Shown == null || Shown());
        Image.enabled = show;
        if (show) Image.sprite = PadIcons.Get(Glyph);
    }

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);
    void LateUpdate() => Refresh();

    // Whether a pad button already presses this widget, so gamepad focus needn't stop on it.
    public static bool IsMapped(Selectable widget)
    {
        foreach (PadIcon icon in active)
            if (icon.Widget == widget && (icon.Shown == null || icon.Shown())) return true;
        return false;
    }
}
