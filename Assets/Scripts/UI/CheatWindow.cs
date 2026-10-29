using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Testing cheats, toggled with F1. Only added in the editor and development builds (see
// MatchManager). Built in uGUI at runtime like the other screens; add a cheat with AddCheat.
public class CheatWindow : MonoBehaviour
{
    MatchManager match;
    Canvas canvas;
    RectTransform panel;
    readonly List<Action> refreshers = new();

    void Awake() => match = GetComponent<MatchManager>();

    void Start()
    {
        // Above the run screens so cheats work on the results and map too.
        canvas = UIKit.CreateCanvas("Cheats", 30);
        panel = UIKit.Panel(canvas.transform, "Cheat Window", UIKit.PanelColor);
        UIKit.Place(panel, new Vector2(0f, 1f), new Vector2(8f, -40f), new Vector2(200f, 0f));
        UIKit.Layout<VerticalLayoutGroup>(panel, 8, 6, fitContent: true);

        UIKit.Label(panel, "CHEATS  [F1]", 14);
        AddCheat("Win match", match.CheatWinMatch, () => match.CanCheatWin);
        AddCheat("Kill the boss", match.CheatKillBoss, () => match.CanCheatKillBoss);
        AddCheat("+500 Gold", () => match.CheatAddGold(500), () => RunState.Current != null);
        AddCheat("Full health", match.CheatHealFully, () => RunState.Current != null && RunState.Current.Health < RunState.Current.MaxHealth);
        AddCheat("Reveal the map", match.CheatRevealMap, () => match.CanCheatRevealMap);

        panel.gameObject.SetActive(false);
    }

    void AddCheat(string label, Action apply, Func<bool> available) =>
        UIKit.LiveButton(panel, () => label, apply, refreshers, available, height: 26f);

    void Update()
    {
        if (panel == null) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) panel.gameObject.SetActive(!panel.gameObject.activeSelf);
        if (panel.gameObject.activeSelf) UIKit.Refresh(refreshers);
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
