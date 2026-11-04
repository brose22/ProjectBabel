using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One layer of gamepad focus: a screen, window or drawer. Scopes stack in the order they were
// enabled, and UIFocus keeps focus inside the newest, so a window can't lose it to the screen behind.
// Scopes are added with UIKit.Focus and go away with the object that hosts them.
public class UIFocusScope : MonoBehaviour
{
    static readonly List<UIFocusScope> active = new();

    public static UIFocusScope Top => active.Count > 0 ? active[^1] : null;

    // Focus stays on the buttons under this transform.
    public Transform Root { get; set; }
    // What the cancel button does here, if anything.
    public Action OnCancel { get; set; }
    // Where focus starts, when the screen has a better place than its first button.
    public Func<Selectable> Default { get; set; }
    // Seconds to wait before taking focus, for a panel that slides in.
    public float Delay { get; set; }
    // Where focus was, for coming back when the window above closes.
    public Selectable Remembered { get; set; }

    Selectable requested;
    int requestedFrame;
    int enabledFrame;
    float enabledTime;

    // Layout only settles at the end of the frame a screen is built in.
    public bool Ready => Time.frameCount > enabledFrame && Time.unscaledTime >= enabledTime + Delay;

    // Moves focus to a button next frame, after the layout it was built in settles: for a screen that
    // rebuilds part of itself rather than the whole screen.
    public void Request(Selectable selectable)
    {
        requested = selectable;
        requestedFrame = Time.frameCount;
    }

    public bool HasRequest => requested != null;

    // Hands focus back to a button once the window over it closes: the newest layer holding the
    // button remembers it and lands on it.
    public static void Return(Selectable selectable)
    {
        if (selectable == null) return;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            UIFocusScope scope = active[i];
            if (scope.Root == null || !selectable.transform.IsChildOf(scope.Root)) continue;
            scope.Remembered = selectable;
            scope.Request(selectable);
            return;
        }
    }

    // The requested button once its frame has passed, or null while it waits.
    public Selectable TakeRequest()
    {
        if (Time.frameCount <= requestedFrame) return null;
        Selectable selectable = requested;
        requested = null;
        return selectable;
    }

    void OnEnable()
    {
        active.Add(this);
        enabledFrame = Time.frameCount;
        enabledTime = Time.unscaledTime;
    }

    void OnDisable() => active.Remove(this);
}
