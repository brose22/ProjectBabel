using System;
using UnityEngine;
using UnityEngine.UI;

// The soldier reward after a won city.
public partial class RunScreens
{
    // After a won city: pick one of the offered soldiers into the inventory, or skip.
    void BuildSoldierReward()
    {
        RunState run = RunState.Current;
        SoldierItem chosen = null;

        RectTransform root = NewScreen("Soldier Reward");
        RectTransform column = UIKit.Group(root, "Column");
        UIKit.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 10, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;

        UIKit.Label(column, "SOLDIERS OFFER TO JOIN", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, "Take one into your soldier inventory, or go on without.", 14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform row = UIKit.Group(column, "Soldiers");
        var rowLayout = row.gameObject.AddComponent<GridLayoutGroup>();
        rowLayout.cellSize = new Vector2(205f, SoldierCardHeight);
        rowLayout.spacing = new Vector2(8f, 8f);
        rowLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        rowLayout.constraintCount = 1;
        rowLayout.childAlignment = TextAnchor.UpperCenter;
        int count = run.SoldierOffer.Count;
        UIKit.Size(row, count * 205f + (count - 1) * 8f, SoldierCardHeight);
        foreach (SoldierItem soldier in run.SoldierOffer)
            BuildSoldierCard(row, soldier, () => chosen = soldier, () => chosen == soldier);

        RectTransform buttons = UIKit.Group(column, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 10, fitContent: true);
        UIKit.Primary(UIKit.LiveButton(buttons, () => chosen != null ? $"Take the {chosen.rarity} {ClassName(chosen)}" : "Pick a soldier",
            () => match.Run.TakeSoldier(chosen), refreshers, () => chosen != null, width: 240f, height: 32f), refreshers);
        UIKit.Button(buttons, "Skip", () => match.Run.TakeSoldier(null), 140f, 32f);
    }

    const float SoldierCardHeight = 96f;

    string ClassName(SoldierItem soldier)
    {
        SoldierClassData soldierClass = match.Data.ClassData(soldier.soldierClass);
        return soldierClass != null ? soldierClass.displayName : soldier.soldierClass.ToString();
    }

    // Like a captain card: picked cards keep their dark face and get a gold ring.
    void BuildSoldierCard(Transform parent, SoldierItem soldier, Action onClick, Func<bool> selected)
    {
        Button card = UIKit.Button(parent, "", onClick);
        UIKit.Clear(card.transform);
        var image = card.GetComponent<Image>();
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(card, 8, 2);
        layout.padding.top = 6;
        layout.childAlignment = TextAnchor.UpperCenter;
        SoldierClassData soldierClass = match.Data.ClassData(soldier.soldierClass);
        UIKit.Icon(card.transform, soldierClass != null ? soldierClass.portrait : null, 40f);
        UIKit.Label(card.transform, CaptainPanelParts.SoldierName(soldier, soldierClass), 14, TextAnchor.MiddleCenter);
        string detail = CaptainPanelParts.SoldierBonus(soldier, soldierClass) + (soldierClass != null ? $"  {soldierClass.damageType}" : "");
        UIKit.Label(card.transform, detail, 11, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        Image ring = UIKit.Outline(card.transform, UIKit.AccentColor);
        ring.rectTransform.offsetMin = new Vector2(2f, 2f);
        ring.rectTransform.offsetMax = new Vector2(-2f, -2f);
        refreshers.Add(() =>
        {
            bool isPicked = selected();
            image.color = isPicked ? PickedCardColor : CardColor;
            ring.enabled = isPicked;
        });
    }
}
