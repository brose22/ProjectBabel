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

    int enabledFrame;
    float enabledTime;

    // Layout only settles at the end of the frame a screen is built in.
    public bool Ready => Time.frameCount > enabledFrame && Time.unscaledTime >= enabledTime + Delay;

    void OnEnable()
    {
        active.Add(this);
        enabledFrame = Time.frameCount;
        enabledTime = Time.unscaledTime;
    }

    void OnDisable() => active.Remove(this);
}
