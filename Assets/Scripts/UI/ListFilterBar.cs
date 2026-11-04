using System;
using UnityEngine;
using UnityEngine.UI;

// The row of filter buttons over a list of soldiers or captains (see ListFilter). Each button shows
// its filter's pick and steps to the next on a click or the gamepad's confirm, All after the last;
// Reset puts every filter back to All. changed refills the list, and nothing else is rebuilt, so
// gamepad focus stays on the button.
public static class ListFilterBar
{
    const float Height = 22f;
    const int FontSize = 11;

    static readonly string[] SoldierNames = { "Class", "Element", "Rarity" };
    static readonly string[] CaptainNames = { "Role", "Element" };

    public static ListFilter ForSoldiers() => new(Enum.GetValues(typeof(SoldierClass)).Length,
        Enum.GetValues(typeof(Element)).Length, Enum.GetValues(typeof(SoldierRarity)).Length);

    public static ListFilter ForCaptains() => new(Enum.GetValues(typeof(CaptainRole)).Length, Enum.GetValues(typeof(Element)).Length);

    // A soldier takes the element of the captain leading it, so one in the inventory has none.
    public static int[] ValuesOf(SoldierItem soldier, Captain leader) =>
        new[] { (int)soldier.soldierClass, leader != null ? (int)leader.element : ListFilter.All, (int)soldier.rarity };

    public static int[] ValuesOf(Captain captain) => new[] { (int)captain.role, (int)captain.element };

    public static RectTransform Soldiers(Transform parent, ListFilter filter, Action changed) =>
        Build(parent, filter, changed, SoldierNames, typeof(SoldierClass), typeof(Element), typeof(SoldierRarity));

    public static RectTransform Captains(Transform parent, ListFilter filter, Action changed) =>
        Build(parent, filter, changed, CaptainNames, typeof(CaptainRole), typeof(Element));

    // The line in an empty list: noneText when there is nothing to filter, otherwise that the filters hide everything.
    public static void Empty(Transform list, bool noneAtAll, string noneText)
    {
        UIKit.Label(list, noneAtAll ? noneText : "Nothing matches these filters.", 13, TextAnchor.MiddleCenter,
            UIKit.MutedTextColor);
    }

    static RectTransform Build(Transform parent, ListFilter filter, Action changed, string[] names, params Type[] enums)
    {
        RectTransform bar = UIKit.Group(parent, "Filters");
        UIKit.Layout<HorizontalLayoutGroup>(bar, 0, 4);
        UIKit.Size(bar, height: Height, flexibleWidth: 1f);
        var labels = new Text[enums.Length];

        void Show()
        {
            for (int i = 0; i < enums.Length; i++)
            {
                int pick = filter.Pick(i);
                string value = pick == ListFilter.All ? "All" : Enum.GetName(enums[i], pick);
                labels[i].text = $"{names[i]}: {value}";
                labels[i].color = pick == ListFilter.All ? UIKit.TextColor : UIKit.AccentColor;
            }
        }

        for (int i = 0; i < enums.Length; i++)
        {
            int index = i;
            Button button = UIKit.Button(bar, "", () =>
            {
                filter.Cycle(index);
                Show();
                changed();
            }, height: Height);
            UIKit.Size(button, height: Height, flexibleWidth: 1f);
            labels[i] = button.GetComponentInChildren<Text>();
            labels[i].fontSize = FontSize;
        }
        Button reset = UIKit.Button(bar, "Reset", () =>
        {
            if (filter.IsAll) return;
            filter.Reset();
            Show();
            changed();
        }, 54f, Height);
        reset.GetComponentInChildren<Text>().fontSize = FontSize;
        Show();
        return bar;
    }
}
