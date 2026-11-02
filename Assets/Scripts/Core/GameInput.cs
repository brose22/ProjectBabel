using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Every game action in one place, so screens ask for "start the wave" rather than for a key. Also
// tracks which kind of device was used last: the gamepad, or the mouse, keyboard and touch together.
// Whichever one moved last is active, and the mouse cursor only shows while it isn't the gamepad.
public static class GameInput
{
    // Squared pixels of movement in a frame before the mouse counts as used, so a nudged desk doesn't
    // take the game away from the gamepad.
    const float MouseMoveThreshold = 4f;

    static int trackedFrame = -1;
    static bool usingGamepad;

    public static bool UsingGamepad
    {
        get
        {
            Track();
            return usingGamepad;
        }
    }

    public static bool StartWave => KeyDown(Key.Space) || Pressed(WorldPad?.buttonNorth);
    public static bool Cancel => KeyDown(Key.Escape) || Pressed(WorldPad?.buttonEast);
    public static bool MoveTower => KeyDown(Key.M) || Pressed(WorldPad?.buttonWest);
    // On the gamepad, selling is in the captain widget, where it can't be pressed by accident.
    public static bool SellTower => KeyDown(Key.X);
    public static bool CycleSpeed => KeyDown(Key.F) || Pressed(WorldPad?.selectButton);
    public static bool ToggleHint => KeyDown(Key.Tab) || Pressed(WorldPad?.leftStickButton);
    public static bool AnswerYes => KeyDown(Key.Y) || Pressed(WorldPad?.buttonSouth);
    public static bool AnswerNo => KeyDown(Key.N) || Pressed(WorldPad?.buttonEast);
    public static bool Pause => Pressed(WorldPad?.startButton);
    public static bool Resume => KeyDown(Key.Escape) || Pressed(Pad?.startButton) || Pressed(Pad?.buttonEast);
    public static bool UseSpecial => Pressed(WorldPad?.rightTrigger);

    // Steps through the party with the shoulder buttons: -1, 1, or 0.
    public static int PartyStep => (Pressed(WorldPad?.rightShoulder) ? 1 : 0) - (Pressed(WorldPad?.leftShoulder) ? 1 : 0);

    // Arrow keys, as a direction for the camera.
    public static Vector2 PanKeys
    {
        get
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;
            return new Vector2((keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f));
        }
    }

    // The menus' back button. Confirm goes through the UI input module to the focused button.
    public static bool UiCancel => Pressed(Pad?.buttonEast);

    // Left stick and D-pad together, for stepping through menus and tiles.
    public static Vector2 PadMove => Pad == null ? Vector2.zero : Vector2.ClampMagnitude(Pad.leftStick.ReadValue() + Pad.dpad.ReadValue(), 1f);

    // Right stick up and down, for scrolling a list without moving focus.
    public static float PadScroll => Pad != null ? Pad.rightStick.ReadValue().y : 0f;

    // Places or selects on the tile under the gamepad's cursor.
    public static bool ConfirmTile => Pressed(WorldPad?.buttonSouth);

    public static bool Recenter => Pressed(WorldPad?.rightStickButton);

    // The right stick pans the camera, or zooms it (up and down) with the left trigger held.
    public static Vector2 PadPan => Pad != null && !Pad.leftTrigger.isPressed ? Pad.rightStick.ReadValue() : Vector2.zero;
    public static float PadZoom => Pad != null && Pad.leftTrigger.isPressed ? Pad.rightStick.ReadValue().y : 0f;

    static Gamepad Pad => Gamepad.current;

    // The gamepad for match actions: none while a menu, window or the captain widget has its focus.
    static Gamepad WorldPad => UIFocusScope.Top == null ? Gamepad.current : null;

    static bool Pressed(ButtonControl button) => button != null && button.wasPressedThisFrame;

    // For the testing panels, whose keys are not game actions.
    public static bool KeyDown(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }

    // Domain reload is off for Play mode, so a session can start with the last one's device.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDevice()
    {
        trackedFrame = -1;
        usingGamepad = false;
    }

    // At most once a frame, on the first question asked.
    static void Track()
    {
        if (trackedFrame == Time.frameCount) return;
        trackedFrame = Time.frameCount;

        bool wanted = usingGamepad;
        if (GamepadUsed()) wanted = true;
        else if (PointerOrKeysUsed()) wanted = false;
        if (wanted == usingGamepad) return;
        usingGamepad = wanted;
        Cursor.visible = !usingGamepad;
    }

    // Sticks and triggers count too: their directions are buttons with a press threshold.
    static bool GamepadUsed()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return false;
        foreach (InputControl control in pad.allControls)
            if (control is ButtonControl button && button.wasPressedThisFrame) return true;
        return false;
    }

    static bool PointerOrKeysUsed()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > MouseMoveThreshold || mouse.leftButton.wasPressedThisFrame ||
                              mouse.rightButton.wasPressedThisFrame || mouse.scroll.ReadValue() != Vector2.zero)) return true;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
        Touchscreen touch = Touchscreen.current;
        return touch != null && touch.primaryTouch.press.wasPressedThisFrame;
    }
}
