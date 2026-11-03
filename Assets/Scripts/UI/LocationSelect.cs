using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Castle's list of locations to set out for (item: location). Until the locations themselves
// land (#32), Options is a placeholder list of three, unlocked in order by the profile's count; the
// Castle only needs each one's name, a line about it, and whether it is unlocked.
public static class LocationSelect
{
    public readonly struct Option
    {
        public readonly string name;
        public readonly string description;
        public readonly bool unlocked;

        public Option(string name, string description, bool unlocked)
        {
            this.name = name;
            this.description = description;
            this.unlocked = unlocked;
        }
    }

    const int PlaceholderCount = 3;

    // Every location in unlock order; the index is the one a run starts at (RunState.Location).
    public static List<Option> Options(MatchData data, Profile profile)
    {
        var options = new List<Option>();
        for (int i = 0; i < PlaceholderCount; i++)
            options.Add(new Option($"Location {i + 1}", i == 0 ? "The first location." : $"Win location {i} to unlock.",
                i < profile.LocationsUnlocked));
        return options;
    }

    // One row per location: its name and line, and a button that sets out for it while unlocked.
    public static void Build(Transform parent, MatchData data, Profile profile, Action<int> setOut, float width)
    {
        List<Option> options = Options(data, profile);
        for (int i = 0; i < options.Count; i++)
        {
            Option option = options[i];
            int index = i;
            RectTransform row = UIKit.Box(parent, option.name, option.unlocked ? null : UIKit.InsetColor, blocksClicks: false);
            UIKit.Size(row, width);
            UIKit.Layout<HorizontalLayoutGroup>(row, 8, 8).childAlignment = TextAnchor.MiddleLeft;
            RectTransform text = UIKit.Group(row, "Text");
            UIKit.Layout<VerticalLayoutGroup>(text, 0, 1);
            UIKit.Size(text, flexibleWidth: 1f);
            Text name = UIKit.Label(text, option.name, 15, color: option.unlocked ? UIKit.TextColor : UIKit.DisabledTextColor);
            name.fontStyle = FontStyle.Bold;
            UIKit.Label(text, option.description, 11, color: UIKit.MutedTextColor);
            Button button = UIKit.Button(row, option.unlocked ? "Set out" : "Locked", () => setOut(index), 110f, 28f);
            button.interactable = option.unlocked;
        }
    }
}
