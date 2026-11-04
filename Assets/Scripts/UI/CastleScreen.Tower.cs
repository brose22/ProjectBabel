using UnityEngine;
using UnityEngine.UI;

// The location select, drawn as the Tower: each location is a floor, climbed from the bottom. A
// floor opens when a run is won on the one below it (LocationRules); floors above the last
// location are sealed, there to show the climb goes on. Clicking a floor shows it in the panel on
// the left, which sets out from there. The art is Tools/tower_art.py, which prints FloorRects.
public partial class CastleScreen
{
    const string TowerSprite = "Tower/tower_map";
    const float TowerAspect = 400f / 224f;
    const float TowerPanelWidth = 250f;
    static readonly Color FloorHighlight = new(1f, 0.85f, 0.45f, 0.12f);

    // Centre (0..1 from the bottom-left) and size of each floor on the tower art, bottom first.
    static readonly (Vector2 at, Vector2 size)[] FloorRects =
    {
        (new(0.500f, 0.237f), new(0.420f, 0.134f)),
        (new(0.500f, 0.364f), new(0.365f, 0.121f)),
        (new(0.500f, 0.480f), new(0.315f, 0.112f)),
        (new(0.500f, 0.587f), new(0.270f, 0.103f)),
        (new(0.500f, 0.685f), new(0.230f, 0.094f)),
        (new(0.500f, 0.775f), new(0.195f, 0.085f)),
        (new(0.500f, 0.855f), new(0.160f, 0.076f)),
    };

    // The floor shown in the panel (0 is the ground floor).
    int towerFloor;

    void BuildLocations()
    {
        RectTransform column = NewScreen("Locations", 0f, BuildParty);
        column.gameObject.SetActive(false);
        // The tower is outside the Castle, so its title banner is hidden here.
        titleBanner.gameObject.SetActive(false);
        screen.GetComponent<Image>().color = Color.black;

        RectTransform map = UIKit.Panel(screen, "Tower", Color.white, blocksClicks: false);
        map.SetAsFirstSibling();
        map.GetComponent<Image>().sprite = Resources.Load<Sprite>(TowerSprite);
        map.anchorMin = map.anchorMax = map.pivot = new Vector2(0.5f, 0.5f);
        var fitter = map.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = TowerAspect;

        var entries = LocationSelect.Entries(data, profile);
        if (entries.Count > FloorRects.Length)
            Debug.LogWarning($"The tower has {FloorRects.Length} floors for {entries.Count} locations; the rest aren't shown.");
        int floors = FloorRects.Length;
        int open = 0;
        for (int i = 0; i < entries.Count; i++) if (entries[i].unlocked) open = i;
        towerFloor = Mathf.Clamp(towerFloor, 0, floors - 1);
        if (towerFloor >= entries.Count || !entries[towerFloor].unlocked) towerFloor = open;

        for (int i = 0; i < floors; i++)
        {
            int floor = i;
            bool real = i < entries.Count;
            bool unlocked = real && entries[i].unlocked;
            string name = real ? entries[i].location.displayName : "Sealed";
            var (at, size) = FloorRects[i];

            RectTransform rect = UIKit.Panel(map, $"Floor {i + 1}", FloorHighlight);
            rect.anchorMin = at - size / 2f;
            rect.anchorMax = at + size / 2f;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = rect.GetComponent<Image>();
            Button button = UIKit.Clickable(rect, () => towerFloor = floor);
            UIKit.HoverTip(rect, $"Floor {i + 1}: {name}", FloorText(entries, i));
            if (i == towerFloor) screenScope.Default = () => button;
            Image ring = UIKit.Outline(rect, UIKit.AccentColor);
            refreshers.Add(() =>
            {
                ring.enabled = towerFloor == floor;
                image.color = towerFloor == floor ? FloorHighlight : Color.clear;
            });

            // A sign off the right of the floor: its number and name, a lock while it's closed.
            RectTransform sign = UIKit.Box(map, "Sign", SignColor, blocksClicks: false);
            HorizontalLayoutGroup row = UIKit.Layout<HorizontalLayoutGroup>(sign, 0, 6, fitContent: true);
            row.padding = new RectOffset(8, 8, 2, 2);
            row.childAlignment = TextAnchor.MiddleLeft;
            sign.anchorMin = sign.anchorMax = new Vector2(at.x + size.x / 2f + 0.012f, at.y);
            sign.pivot = new Vector2(0f, 0.5f);
            UIKit.Outline(sign, unlocked ? UIKit.AccentColor : UIKit.RuleColor);
            if (!unlocked) UIKit.Icon(sign, LockSprite, 14f).raycastTarget = false;
            Text label = UIKit.Label(sign, $"{i + 1}F  {name}", UIKit.BodySize, TextAnchor.MiddleLeft,
                unlocked ? UIKit.TextColor : UIKit.DisabledTextColor);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
        }

        // The panel on the left: the party, the floor picked, and Set out while it's open.
        RectTransform panel = UIKit.Box(screen, "Floor Panel", BannerColor);
        UIKit.Place(panel, new Vector2(0f, 0.5f), new Vector2(HudMargin * 2f, 0f), new Vector2(TowerPanelWidth, 0f));
        panel.pivot = new Vector2(0f, 0.5f);
        UIKit.Outline(panel, UIKit.AccentColor);
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 8, fitContent: true);
        layout.childAlignment = TextAnchor.UpperLeft;
        Title(panel, "THE TOWER").alignment = TextAnchor.MiddleLeft;
        UIKit.Label(panel, $"Party: {string.Join(", ", party.ConvertAll(c => c.displayName))}", UIKit.CaptionSize,
            color: UIKit.MutedTextColor).horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.LiveLabel(panel, () => $"FLOOR {towerFloor + 1}", refreshers, UIKit.CaptionSize, color: UIKit.AccentColor);
        Text heading = UIKit.LiveLabel(panel, () => towerFloor < entries.Count ? entries[towerFloor].location.displayName : "Sealed",
            refreshers, 20);
        heading.fontStyle = FontStyle.Bold;
        Text body = UIKit.LiveLabel(panel, () => FloorText(entries, towerFloor), refreshers, 13, color: UIKit.MutedTextColor);
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIKit.Size(body, TowerPanelWidth - UIKit.Pad * 2f).minHeight = 54f;
        bool CanSetOut() => towerFloor < entries.Count && entries[towerFloor].unlocked;
        UIKit.Primary(UIKit.LiveButton(panel, () => CanSetOut() ? "Set out" : "Locked",
            () => SetOut(entries[towerFloor].location), refreshers, CanSetOut, width: TowerPanelWidth - UIKit.Pad * 2f, height: 32f),
            refreshers);
    }

    // An open floor's description, or what opens it: a win on the floor below.
    static string FloorText(System.Collections.Generic.List<LocationSelect.Entry> entries, int floor)
    {
        if (floor >= entries.Count) return "No one has climbed this high yet. The way up is not open.";
        if (entries[floor].unlocked) return entries[floor].location.description;
        return floor > 0 ? $"Locked: win a run at {entries[floor - 1].location.displayName} to open it." : "Locked.";
    }
}
