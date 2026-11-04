using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Great Hall: the roster as cards, three to a row like the party pick. A card opens the
// captain's window in the middle of the screen, with their level, bond, stats, special and soldier
// slots. Picking a slot slides that window left and opens the list of every soldier beside it, to
// equip one in the slot or take the slot's soldier out.
public partial class CastleScreen
{
    const float HallWindowWidth = 440f;
    const float HallPickerWidth = 440f;
    const float HallWindowHeight = 440f;
    const float HallWindowGap = 12f;
    const float HallSlotHeight = 26f;
    const float HallStatWidth = 56f;
    const float HallSlideSeconds = 0.18f;
    // How far right of where it settles the soldier list starts its slide.
    const float HallPickerTravel = 60f;
    // Opaque, so the cards never show through the windows.
    static readonly Color HallWindowColor = new(UIKit.PanelColor.r, UIKit.PanelColor.g, UIKit.PanelColor.b, 1f);
    static readonly Color HallOverlayColor = new(0f, 0f, 0f, 0.55f);
    static readonly Color HallBondBarColor = new(0.62f, 0.45f, 0.16f, 1f);

    // The captain whose window is open (null when none is), and the slot whose soldier is being
    // picked (-1 while the soldier list is closed).
    Captain hallCaptain;
    int hallSlot = -1;
    RectTransform hallOverlay, hallWindow, hallPicker;
    // The "GREAT HALL" title and the roster cards, hidden while a captain window is open.
    RectTransform hallColumn;
    CanvasGroup hallPickerFade;
    UIFocusScope hallWindowScope;
    // 0 with the captain window centred, 1 with it moved left and the soldier list beside it.
    float hallSlide;
    readonly List<Button> hallSlots = new();
    readonly List<Image> hallSlotRings = new();
    // Each roster card's button and soldier line.
    readonly Dictionary<Captain, Button> hallCardButtons = new();
    readonly Dictionary<Captain, Text> hallCards = new();

    // ---------- The roster ----------

    void BuildCaptainsTab(Transform body)
    {
        RectTransform panel = UIKit.Box(body, "Roster");
        UIKit.Size(panel, height: InventoryBodyHeight, flexibleWidth: 1f);
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 6);
        Header(panel, "CAPTAINS", $"{profile.Roster.Count}/{Profile.MaxRoster}");
        ScrollRect list = null;
        void Fill()
        {
            hallCards.Clear();
            hallCardButtons.Clear();
            CaptainGrid(list, hallFilter, HallCard);
        }
        ListFilterBar.Captains(panel, hallFilter, Fill);
        list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        Fill();
        UIKit.Label(panel, "Pick a captain to see them in full and change their soldiers.", 11, TextAnchor.MiddleCenter,
            UIKit.MutedTextColor);
    }

    void HallCard(Transform row, Captain captain)
    {
        RectTransform card = CaptainCard(row, captain, "", () => OpenHallWindow(captain), false, out _, out _, out Text soldiers);
        UIKit.Size(card, 0f, CardHeight, 1f);
        Button button = card.GetComponent<Button>();
        hallCards[captain] = soldiers;
        hallCardButtons[captain] = button;
        if (captain == selected) screenScope.Default = () => button;
    }

    // ---------- The captain window ----------

    // A dimmed layer under the HUD with the captain's window in its middle. A click beside the
    // windows, or the gamepad's cancel button, closes the soldier list first and then the window.
    void OpenHallWindow(Captain captain)
    {
        DestroyHallWindow();
        selected = hallCaptain = captain;
        hallSlot = -1;
        hallSlide = 0f;
        if (hallColumn != null) hallColumn.gameObject.SetActive(false);

        hallOverlay = UIKit.Stretch(UIKit.Panel(screen, "Captain Window", HallOverlayColor));
        hallOverlay.offsetMax = new Vector2(0f, -HudHeight);
        var dismiss = hallOverlay.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(HallCancel);
        hallWindowScope = UIKit.Focus(hallOverlay, HallCancel);

        hallWindow = UIKit.Box(hallOverlay, "Window", HallWindowColor);
        // Swallows clicks on the window, which would otherwise reach the overlay and close it.
        hallWindow.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        UIKit.Place(hallWindow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HallWindowWidth, HallWindowHeight));
        UIKit.Layout<VerticalLayoutGroup>(hallWindow, 10, 5);
        BuildHallWindow();
        hallWindowScope.Default = () => hallSlots.Count > 0 ? hallSlots[0] : null;
    }

    void DestroyHallWindow()
    {
        if (hallOverlay != null) Destroy(hallOverlay.gameObject);
        hallOverlay = hallWindow = hallPicker = null;
        hallSlots.Clear();
        hallSlotRings.Clear();
    }

    // Closes the window and puts focus back on the captain's card.
    void CloseHallWindow()
    {
        Captain captain = hallCaptain;
        DestroyHallWindow();
        hallCaptain = null;
        hallSlot = -1;
        if (hallColumn != null) hallColumn.gameObject.SetActive(true);
        if (captain != null && hallCardButtons.TryGetValue(captain, out Button card) && card != null) screenScope.Request(card);
    }

    void HallCancel()
    {
        if (hallSlot >= 0) CloseHallPicker();
        else CloseHallWindow();
    }

    // Empties the window and draws the captain in it again, after a soldier changes. It adds no
    // screen refreshers, which would outlive the window.
    void BuildHallWindow()
    {
        if (hallWindow == null || hallCaptain == null) return;
        foreach (Transform child in hallWindow)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        hallSlots.Clear();
        hallSlotRings.Clear();
        Captain captain = hallCaptain;
        int level = Level(captain);
        HallWindowHeader(hallWindow, captain, level);

        // Power, range, speed and defense include what the fielded soldiers add.
        int open = MatchManager.SoldierCountFor(data, captain);
        List<SoldierItem> fielded = profile.SoldiersOf(captain).FindAll(s => s.slot < open && data.ClassData(s.soldierClass) != null);
        float Bonus(CaptainStat stat) => data.StatBonus(fielded, stat);
        RectTransform stats = UIKit.Group(hallWindow, "Stats");
        UIKit.Layout<HorizontalLayoutGroup>(stats, 0, 4).childAlignment = TextAnchor.UpperCenter;
        UIKit.Size(stats, flexibleWidth: 1f);
        HallStat(stats, "POWER", $"{(captain.power + Bonus(CaptainStat.Power)) * LevelMath.StatFactor(level):0}");
        float range = TowerReach.Range(fielded.ConvertAll(s => data.ClassData(s.soldierClass).reach), captain.range + Bonus(CaptainStat.Range));
        HallStat(stats, "RANGE", $"{range:0.#}");
        HallStat(stats, "SPEED", $"{captain.speed + Bonus(CaptainStat.Speed):0.##}");
        HallStat(stats, "HEALTH", $"{captain.health}");
        HallStat(stats, "DEFENSE", $"{DefenseMath.AtLevel(captain.defense, captain.defensePerLevel, level) + Bonus(CaptainStat.Defense):0.#}");
        HallStat(stats, "RESERVE", $"{captain.ResourceCapacity}");
        HallStat(stats, "DRAW", $"{captain.ResourceRegen:0.#}/s");

        Transform special = UIKit.Group(hallWindow, "Special");
        UIKit.Layout<HorizontalLayoutGroup>(special, 0, 6);
        UIKit.Icon(special, captain.Ability != null ? captain.Ability.icon : null, 24f);
        UIKit.Label(special, captain.Ability != null ? captain.Ability.displayName : "No special ability", 13).fontStyle = FontStyle.Bold;
        if (!string.IsNullOrEmpty(captain.AbilityDescription))
            UIKit.Label(hallWindow, captain.AbilityDescription, 11, color: UIKit.MutedTextColor);
        RunScreens.TagLine(hallWindow, "Likes", captain.likes, UIKit.GoodColor);
        RunScreens.TagLine(hallWindow, "Dislikes", captain.dislikes, UIKit.BadColor);

        UIKit.Size(UIKit.Header(hallWindow, "SOLDIERS", $"{open}/{SoldierInventory.SlotsPerCaptain} open"), flexibleWidth: 1f);
        for (int i = 0; i < SoldierInventory.SlotsPerCaptain; i++) HallSlotRow(hallWindow, captain, i, open);

        UIKit.Size(UIKit.Group(hallWindow, "Spacer"), flexibleWidth: 1f).flexibleHeight = 1f;
        Transform buttons = UIKit.Group(hallWindow, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 6);
        UIKit.Size(buttons, flexibleWidth: 1f);
        Button release = UIKit.Button(buttons, $"Release {captain.displayName}", () => BuildConfirmRelease(captain), height: 26f);
        UIKit.Size(release, height: 26f, flexibleWidth: 1f);
        release.GetComponent<Image>().color = UIKit.BadColor;
        Button close = UIKit.Button(buttons, "Close", CloseHallWindow, height: 26f);
        UIKit.Size(close, height: 26f, flexibleWidth: 1f);
        UIKit.PadBadge(close, PadGlyph.East);
    }

    // Portrait, name, the level and bond meters, element and class, and what the next bond rank gives.
    void HallWindowHeader(Transform window, Captain captain, int level)
    {
        RectTransform top = UIKit.Group(window, "Top");
        UIKit.Layout<HorizontalLayoutGroup>(top, 0, 10).childAlignment = TextAnchor.UpperLeft;
        UIKit.Size(top, flexibleWidth: 1f);
        RectTransform frame = UIKit.Box(top, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.Portrait, PortraitSize);

        RectTransform info = UIKit.Group(top, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 3);
        UIKit.Size(info, flexibleWidth: 1f);
        Text name = UIKit.Shadowed(UIKit.Label(info, captain.displayName, 18));
        name.fontStyle = FontStyle.Bold;

        // Nothing here changes while the window is open, so the meters are drawn once.
        var drawOnce = new List<Action>();
        int xp = Mathf.Max(captain.xp, LevelMath.XpForLevel(level));
        bool maxLevel = level >= LevelMath.MaxLevel;
        int from = LevelMath.XpForLevel(level), to = LevelMath.XpForLevel(level + 1);
        UIKit.LiveBar(info, () => maxLevel ? 1f : (xp - from) / (float)Mathf.Max(1, to - from),
            () => maxLevel ? $"Level {level}  (max)" : $"Level {level}   XP {xp}/{to}", UIKit.XpBarColor, drawOnce);

        ElementData element = data.ElementInfo(captain.element);
        Transform elementRow = UIKit.Group(info, "Element");
        UIKit.Layout<HorizontalLayoutGroup>(elementRow, 0, 4);
        UIKit.Icon(elementRow, element != null ? element.icon : null, IconSize);
        UIKit.Label(elementRow, element != null ? element.displayName : captain.element.ToString(), 12, color: UIKit.ElementColor(captain.element));
        UIKit.Size(UIKit.Group(elementRow, "Gap"), 6f);
        UIKit.Icon(elementRow, data.RoleIcon(captain.role), IconSize);
        UIKit.Label(elementRow, captain.ClassName, 12);
        UIKit.Label(info, $"Trait: {CaptainTraits.Name(captain.Trait)} - {CaptainTraits.Description(captain.Trait)}", 11, color: UIKit.MutedTextColor);

        UIKit.LiveBar(info, () => (ApprovalStore.ApprovalOf(captain) - BondMath.MinApproval) / (BondMath.MaxApproval - BondMath.MinApproval),
            () => $"Bond {BondMath.RankName(ApprovalStore.RankOf(captain))}   Approval {ApprovalStore.ApprovalOf(captain):0}", HallBondBarColor, drawOnce);
        UIKit.Label(info, RunScreens.NextRankLine(captain), 11, color: UIKit.MutedTextColor);
        UIKit.Refresh(drawOnce);
    }

    static void HallStat(Transform parent, string caption, string value)
    {
        RectTransform stat = UIKit.Group(parent, caption);
        UIKit.Layout<VerticalLayoutGroup>(stat, 0, -1f).childAlignment = TextAnchor.UpperCenter;
        UIKit.Size(stat, HallStatWidth);
        UIKit.Label(stat, caption, 9, TextAnchor.MiddleCenter, UIKit.MutedTextColor).fontStyle = FontStyle.Bold;
        UIKit.Label(stat, value, 15, TextAnchor.MiddleCenter);
    }

    // The slot's name and its soldier, a button that opens the soldier list. A gold ring marks the
    // slot while the list is open for it. A slot the bond hasn't opened is a padlock and the rank
    // that opens it, with no button, so neither the mouse nor the gamepad can pick it. Open slots
    // always come first, so hallSlots and hallSlotRings stay indexed by slot.
    void HallSlotRow(Transform window, Captain captain, int slot, int open)
    {
        RectTransform row = UIKit.Group(window, $"Slot {slot + 1}");
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 6);
        UIKit.Size(row, flexibleWidth: 1f);
        UIKit.Size(UIKit.Label(row, CaptainPanelParts.SlotLabel(slot), 12,
            color: slot < open ? UIKit.MutedTextColor : UIKit.DisabledTextColor), 110f);
        if (slot >= open)
        {
            RectTransform locked = UIKit.Box(row, "Locked", UIKit.InsetColor, blocksClicks: false);
            UIKit.Size(locked, height: HallSlotHeight, flexibleWidth: 1f);
            HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(locked, 4, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            UIKit.Icon(locked, LockSprite, 16f).raycastTarget = false;
            UIKit.Label(locked, $"Opens at bond {BondMath.RankName(CaptainPanelParts.RankOpening(slot))}", 11, TextAnchor.MiddleLeft,
                UIKit.DisabledTextColor);
            return;
        }
        SoldierItem soldier = profile.Soldiers.InSlot(captain.id, slot);
        Button button = UIKit.Button(row, soldier != null ? SoldierSummary(soldier) : "Empty", () => OpenHallPicker(slot), height: HallSlotHeight);
        UIKit.Size(button, height: HallSlotHeight, flexibleWidth: 1f);
        if (soldier == null) button.GetComponentInChildren<Text>().color = UIKit.DisabledTextColor;
        Image ring = UIKit.Outline(button.transform, UIKit.AccentColor);
        ring.enabled = slot == hallSlot;
        hallSlots.Add(button);
        hallSlotRings.Add(ring);
    }

    // ---------- The soldier list ----------

    // Every soldier owned, those in the inventory first, rarest first, in a window that slides in
    // beside the captain's. Picking a row puts that soldier in the slot, moving it from another captain
    // if it was theirs; picking the slot's own soldier sends it back to the inventory. Either closes
    // the list.
    void OpenHallPicker(int slot)
    {
        if (hallOverlay == null || hallCaptain == null) return;
        if (hallPicker != null) Destroy(hallPicker.gameObject);
        Captain captain = hallCaptain;
        hallSlot = slot;
        for (int i = 0; i < hallSlotRings.Count; i++) hallSlotRings[i].enabled = i == slot;

        hallPicker = UIKit.Box(hallOverlay, "Soldier List", HallWindowColor);
        hallPicker.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        UIKit.Place(hallPicker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HallPickerWidth, HallWindowHeight));
        hallPickerFade = hallPicker.gameObject.AddComponent<CanvasGroup>();
        hallPickerFade.alpha = 0f;
        UIKit.Layout<VerticalLayoutGroup>(hallPicker, 10, 5);
        // Focus waits for the slide, then stays in the list until it closes.
        UIFocusScope scope = UIKit.Focus(hallPicker, CloseHallPicker, delay: HallSlideSeconds);

        UIKit.Size(UIKit.Header(hallPicker, "SOLDIERS", CaptainPanelParts.SlotLabel(slot)), flexibleWidth: 1f);
        UIKit.Label(hallPicker, $"Choose who fights in {captain.displayName}'s {CaptainPanelParts.SlotLabel(slot)}. " +
                                "A soldier with another captain moves over.", 11, color: UIKit.MutedTextColor);

        void Change(Action change)
        {
            change();
            ProfileSave.Save(profile);
            // Soldiers can move between captains, so every card's soldier line is redone.
            foreach (var (each, line) in hallCards)
                if (line != null) line.text = SoldiersLine(each);
            BuildHallWindow();
            CloseHallPicker();
        }

        hallPickerFilter.Reset();
        ScrollRect list = null;
        Button first = null;
        void Fill()
        {
            UIKit.Clear(list.content);
            first = null;
            List<SoldierItem> shown = FilteredSoldiers(hallPickerFilter);
            foreach (SoldierItem soldier in shown)
            {
                SoldierItem chosen = soldier;
                bool here = soldier.equippedTo == captain.id && soldier.slot == slot;
                RectTransform row = SoldierRow(list.content, soldier, here ? "In this slot. Select to remove." : WhereIs(soldier));
                Button action = UIKit.Clickable(row, here ? () => Change(() => profile.Soldiers.Unequip(chosen.id))
                    : () => Change(() => profile.Soldiers.Equip(chosen.id, captain.id, slot)));
                // Who has the soldier now, at the row's right end; not on the row that removes it.
                Captain owner = here || !soldier.IsEquipped ? null : profile.Find(soldier.equippedTo);
                if (owner != null) HallOwnerPortrait(row, owner);
                first ??= action;
            }
            if (shown.Count == 0) ListFilterBar.Empty(list.content, profile.Soldiers.Items.Count == 0, "You have no soldiers yet.");
        }
        ListFilterBar.Soldiers(hallPicker, hallPickerFilter, Fill);
        list = UIKit.ScrollList(hallPicker, "Soldiers", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        Fill();

        Button back = UIKit.Button(hallPicker, "Back", CloseHallPicker, height: 26f);
        UIKit.Size(back, height: 26f, flexibleWidth: 1f);
        UIKit.PadBadge(back, PadGlyph.East);
        scope.Default = () => first != null ? first : back;
        PositionHall();
    }

    // A small framed portrait; the row's flexible text pushes it to the right end.
    static void HallOwnerPortrait(RectTransform row, Captain owner)
    {
        RectTransform frame = UIKit.Box(row, "Owner", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, owner.Portrait, UIKit.ItemIconSize);
    }

    // Starts the list sliding out and the captain window back to the middle, with focus on the slot.
    // The list stops taking focus and clicks at once and is destroyed when the slide ends.
    void CloseHallPicker()
    {
        if (hallSlot < 0) return;
        int slot = hallSlot;
        hallSlot = -1;
        foreach (Image ring in hallSlotRings) ring.enabled = false;
        if (hallPicker != null)
        {
            if (hallPicker.TryGetComponent(out UIFocusScope scope)) scope.enabled = false;
            hallPickerFade.interactable = false;
            hallPickerFade.blocksRaycasts = false;
        }
        if (slot < hallSlots.Count && hallWindowScope != null) hallWindowScope.Request(hallSlots[slot]);
    }

    // Runs the slide on unscaled time each frame while a captain window is open.
    void AnimateHall()
    {
        if (hallWindow == null) return;
        float target = hallSlot >= 0 ? 1f : 0f;
        if (Mathf.Approximately(hallSlide, target) && (target > 0f || hallPicker == null)) return;
        hallSlide = Mathf.MoveTowards(hallSlide, target, Time.unscaledDeltaTime / HallSlideSeconds);
        PositionHall();
        if (hallSlide <= 0f && hallPicker != null)
        {
            Destroy(hallPicker.gameObject);
            hallPicker = null;
        }
    }

    // The captain window moves left by half the list's width, so the pair stays centred.
    void PositionHall()
    {
        if (hallWindow == null) return;
        float eased = Mathf.SmoothStep(0f, 1f, hallSlide);
        hallWindow.anchoredPosition = new Vector2(-(HallPickerWidth + HallWindowGap) / 2f * eased, 0f);
        if (hallPicker == null) return;
        hallPicker.anchoredPosition = new Vector2((HallWindowWidth + HallWindowGap) / 2f + HallPickerTravel * (1f - eased), 0f);
        hallPickerFade.alpha = eased;
    }
}
