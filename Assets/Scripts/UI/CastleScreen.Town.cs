using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Castle's home: a walled town drawn over the whole screen, with a building for each place the
// player can go. Every other screen opens as a panel over the dimmed town. The title banner and the
// Gold, Captains and Soldiers chips sit on top of everything, on every screen.
public partial class CastleScreen
{
    const string TownFolder = "Town/";
    const float TownAspect = 400f / 224f;
    const float BuildingSize = 112f;
    const float HudHeight = 60f;
    const float HudMargin = 8f;
    static readonly Color DimColor = new(0.02f, 0.03f, 0.05f, 0.78f);
    static readonly Color SignColor = new(0.12f, 0.09f, 0.06f, 0.9f);
    static readonly Color BannerColor = new(0.12f, 0.09f, 0.06f, 0.94f);

    RectTransform town;
    RectTransform hud;
    Text hudGold;
    readonly List<Action> hudRefreshers = new();

    // Name, sprite, centre on the map (0..1 from the bottom-left), hover tip and what it opens.
    (string name, string sprite, Vector2 at, string tip, Action open)[] Buildings() => new (string, string, Vector2, string, Action)[]
    {
        ("Great Hall", "great_hall", new(0.13f, 0.73f), "Your captains, their soldiers and their loadouts.", () => OpenRoster(Tab.Captains)),
        ("Barracks", "barracks", new(0.32f, 0.73f), "Every soldier you own, and who leads them.", () => OpenRoster(Tab.Soldiers)),
        ("Tavern", "tavern", new(0.66f, 0.73f), "Hire new captains.", () => OpenShop(ShopTab.Captains)),
        ("Training Yard", "training_yard", new(0.91f, 0.75f), "Recruit new soldiers.", () => OpenShop(ShopTab.Soldiers)),
        ("Storehouse", "storehouse", new(0.2f, 0.3f), "The items packed for your next run.", BuildStorehouse),
        ("Market", "market", new(0.8f, 0.3f), "Buy gifts for your next run.", () => OpenShop(ShopTab.Gifts)),
        ("Gate", "gate", new(0.5f, 0.17f), "Choose a party and set out.", BuildParty),
    };

    void OpenRoster(Tab chosen)
    {
        tab = chosen;
        pickingSlot = -1;
        BuildInventory();
    }

    void OpenShop(ShopTab chosen)
    {
        shopTab = chosen;
        BuildShop();
    }

    static Sprite TownSprite(string name) => Resources.Load<Sprite>(TownFolder + name);

    // The map is scaled to cover the screen, keeping its shape; the buildings are placed on it, so
    // they stay on their lots at any aspect ratio.
    void BuildTown()
    {
        town = UIKit.Stretch(UIKit.Panel(canvas.transform, "Town", Color.black));
        UIKit.Focus(town);
        RectTransform map = UIKit.Panel(town, "Map", Color.white, blocksClicks: false);
        map.GetComponent<Image>().sprite = TownSprite("town_map");
        map.anchorMin = map.anchorMax = map.pivot = new Vector2(0.5f, 0.5f);
        var fitter = map.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = TownAspect;

        foreach (var (name, sprite, at, tip, open) in Buildings())
        {
            RectTransform building = UIKit.Panel(map, name, Color.white);
            Image image = building.GetComponent<Image>();
            image.sprite = TownSprite(sprite);
            image.preserveAspect = true;
            building.anchorMin = building.anchorMax = at;
            building.pivot = new Vector2(0.5f, 0.5f);
            building.sizeDelta = new Vector2(BuildingSize, BuildingSize);
            UIKit.Clickable(building, open);
            UIKit.HoverTip(building, name, tip);

            RectTransform sign = UIKit.Box(building, "Sign", SignColor, blocksClicks: false);
            UIKit.Layout<HorizontalLayoutGroup>(sign, 0, 0, fitContent: true).padding = new RectOffset(8, 8, 2, 2);
            UIKit.Place(sign, new Vector2(0.5f, 0f), new Vector2(0f, -10f), Vector2.zero);
            UIKit.Outline(sign, UIKit.RuleColor);
            Text label = UIKit.Label(sign, name, UIKit.BodySize, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
        }

        Button menu = UIKit.Button(town, "Main menu", MainMenu.Open, 120f, 28f);
        UIKit.Place((RectTransform)menu.transform, new Vector2(0f, 1f), new Vector2(HudMargin, -HudMargin), new Vector2(120f, 28f));
    }

    // The title banner (top centre) and the stat chips (top right), drawn over every screen.
    void BuildHud()
    {
        hud = UIKit.Stretch(UIKit.Group(canvas.transform, "Hud"));

        RectTransform banner = UIKit.Box(hud, "Title Banner", BannerColor, blocksClicks: false);
        UIKit.Place(banner, new Vector2(0.5f, 1f), new Vector2(0f, -HudMargin), new Vector2(230f, 40f));
        UIKit.Outline(banner, UIKit.AccentColor);
        Text title = Title(banner, "THE CASTLE");
        title.fontSize = 24;
        title.raycastTarget = false;
        UIKit.Stretch(title.rectTransform);

        RectTransform chips = Row(hud, "Stats", 6);
        UIKit.Place(chips, new Vector2(1f, 1f), new Vector2(-HudMargin, -HudMargin), Vector2.zero);
        hudGold = StatChip(chips, "Gold", () => profile.Gold.ToString(), UIKit.AccentColor);
        StatChip(chips, "Captains", () => $"{profile.Roster.Count}/{Profile.MaxRoster}", UIKit.TextColor);
        StatChip(chips, "Soldiers", () => profile.Soldiers.Items.Count.ToString(), UIKit.TextColor);
    }

    // A small inset tile: the figure large, its name under it.
    Text StatChip(Transform parent, string name, Func<string> value, Color color)
    {
        RectTransform chip = UIKit.Box(parent, name, UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<VerticalLayoutGroup>(chip, 4, 0).childAlignment = TextAnchor.MiddleCenter;
        UIKit.Size(chip, 78f, 44f);
        Text figure = UIKit.LiveLabel(chip, value, hudRefreshers, 18, TextAnchor.MiddleCenter, color);
        figure.fontStyle = FontStyle.Bold;
        UIKit.Label(chip, name, UIKit.CaptionSize, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        return figure;
    }

    // Home is the town itself: whatever panel was open over it closes.
    void BuildHome()
    {
        refreshers.Clear();
        if (screen != null) Destroy(screen.gameObject);
        screen = null;
        screenFade = null;
    }

    // ---------- Storehouse ----------

    void BuildStorehouse()
    {
        RectTransform column = NewScreen("Storehouse", InventoryWidth, BuildHome);
        UIKit.Label(column, "STOREHOUSE", UIKit.TitleSize, TextAnchor.MiddleCenter);
        RectTransform panel = UIKit.Box(column, "Items");
        UIKit.Size(panel, InventoryWidth, InventoryBodyHeight);
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 6);
        Header(panel, "ITEMS", "Packed for your next run. They go with the party when it sets out.", below: true);
        ScrollRect list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        var listed = new HashSet<string>();
        foreach (GiftData gift in data.shopGifts)
        {
            if (gift == null || !listed.Add(gift.name)) continue;
            int packed = profile.PackedGift(gift.name);
            if (packed > 0) UIKit.ItemRow(list.content, gift.displayName, gift.icon, gift.displayName, UIKit.TextColor, $"x{packed}");
        }
        if (listed.Count == 0 || list.content.childCount == 0)
            UIKit.Label(list.content, "The storehouse is empty. Buy gifts at the Market.", 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
    }
}
