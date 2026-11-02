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

    public static bool StartWave => KeyDown(Key.Space);
    public static bool Cancel => KeyDown(Key.Escape);
    public static bool MoveTower => KeyDown(Key.M);
    public static bool SellTower => KeyDown(Key.X);
    public static bool CycleSpeed => KeyDown(Key.F);
    public static bool ToggleHint => KeyDown(Key.Tab);
    public static bool AnswerYes => KeyDown(Key.Y);
    public static bool AnswerNo => KeyDown(Key.N);
    public static bool Resume => KeyDown(Key.Escape);

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
