using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The AI Run's on-screen panel: what it is doing, a speed pick and a Stop button; when the batch
// ends it becomes the "report ready" screen. Lives on the AiPlayer so it survives scene loads.
public class AiOverlay
{
    static readonly float[] Speeds = { 1f, 2f, 4f, 8f, 16f, 32f };
    // Six speed buttons of 52 px, five 4 px gaps and the panel's 8 px padding each side.
    const float PanelWidth = 6 * 52f + 5 * 4f + 16f;
    const float ToggleHeight = 24f;

    readonly Canvas canvas;
    readonly List<Action> refreshers = new();
    RectTransform doneScreen;

    public AiOverlay(Transform parent, Func<string> status, Func<string> decision, Func<float> speed,
        Action<float> setSpeed, Action stop)
    {
        canvas = UIKit.CreateCanvas("AI Run Overlay", 100);
        canvas.transform.SetParent(parent, false);

        RectTransform panel = UIKit.Box(canvas.transform, "AI Run Panel");
        // The panel sits above the toggle button in the bottom-right corner.
        UIKit.Place(panel, new Vector2(1f, 0f), new Vector2(-8f, 8f + ToggleHeight + 4f), new Vector2(PanelWidth, 0f));
        Button toggle = UIKit.Button(canvas.transform, "AI", () => panel.gameObject.SetActive(!panel.gameObject.activeSelf), 60f, ToggleHeight);
        UIKit.Place((RectTransform)toggle.transform, new Vector2(1f, 0f), new Vector2(-8f, 8f), new Vector2(60f, ToggleHeight));
        UIKit.Layout<VerticalLayoutGroup>(panel, 8, 4, fitContent: true);
        UIKit.Label(panel, "AI RUN", 14, TextAnchor.MiddleLeft, UIKit.SelectedButtonColor);
        UIKit.LiveLabel(panel, status, refreshers, 12);
        UIKit.LiveLabel(panel, decision, refreshers, 11, color: UIKit.MutedTextColor);

        RectTransform speeds = UIKit.Group(panel, "Speed");
        UIKit.Layout<HorizontalLayoutGroup>(speeds, 0, 4, fitContent: true);
        foreach (float value in Speeds)
        {
            float chosen = value;
            UIKit.LiveButton(speeds, () => $"{chosen:0}x", () => setSpeed(chosen), refreshers,
                selected: () => Mathf.Approximately(speed(), chosen), width: 52f, height: 24f);
        }
        UIKit.Button(panel, "Stop and write report", stop, PanelWidth - 16f, 26f);
    }

    public void Refresh() => UIKit.Refresh(refreshers);

    // Replaces the panel's role with the finished screen.
    public void ShowDone(string markdownPath, string csvPath, string summary, Action toMenu)
    {
        if (doneScreen != null) UnityEngine.Object.Destroy(doneScreen.gameObject);
        doneScreen = UIKit.Stretch(UIKit.Panel(canvas.transform, "AI Run Done", new Color(0.02f, 0.02f, 0.04f, 0.94f)));
        UIKit.Focus(doneScreen);
        RectTransform column = UIKit.Group(doneScreen, "Column");
        UIKit.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 10, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;
        UIKit.Label(column, "AI RUN COMPLETE", 28, TextAnchor.MiddleCenter);
        UIKit.Label(column, summary, 14, TextAnchor.MiddleCenter);
        UIKit.Label(column, string.IsNullOrEmpty(markdownPath) ? "The report could not be written; see the Console." : $"Report: {markdownPath}\nWaves CSV: {csvPath}",
            12, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        UIKit.Button(column, "Main menu", toMenu, 200f, 32f);
    }
}
