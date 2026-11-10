using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The map drawers: the party drawer, captain details, gifts and the soldier pickers.
public partial class RunScreens
{
    // The id of the map drawer that is open, or null when none is.
    string openDrawer;

    // Whether the shop window is showing over the world map.
    bool shopOpen;

    // Opens or closes the shop over the map by rebuilding the map screen.
    void ToggleDrawer(string id)
    {
        openDrawer = openDrawer == id ? null : id;
        // Opening a drawer shows its whole list again.
        if (openDrawer != null && drawerOpened.TryGetValue(id, out Action opened)) opened();
    }

    // What each drawer does as it opens, by id.
    readonly Dictionary<string, Action> drawerOpened = new();

    // What the Party drawer, the soldier picker and the abandon picker show. Each resets as it opens.
    readonly ListFilter partyDrawerFilter = ListFilterBar.ForCaptains();
    readonly ListFilter soldierPickerFilter = ListFilterBar.ForSoldiers();
    readonly ListFilter abandonFilter = ListFilterBar.ForSoldiers();

    static int[] SoldierValues(RunState run, SoldierItem soldier) =>
        ListFilterBar.ValuesOf(soldier, soldier.IsEquipped ? run.Profile.Find(soldier.equippedTo) : null);

    // Set when the Shop window closes, so the rebuilt map lands on its Shop button.
    bool shopClosed;

    void SetShopOpen(bool open)
    {
        shopClosed = shopOpen && !open;
        shopOpen = open;
        shownFor = (MatchState)(-1);
    }

    void BuildMapShop(RectTransform root)
    {
        RectTransform panel = UIKit.Modal(root, "Shop", 520f, new Vector2(0.5f, 0.5f), Vector2.zero,
            () => SetShopOpen(false), backdrop: DimColor).Window;
        panel.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        BuildShop(panel, () => SetShopOpen(false));
    }

    string InventorySignature(RunState run)
    {
        var parts = new List<string>();
        foreach (GiftData gift in match.Data.shopGifts)
            if (gift != null) parts.Add(run.GiftCount(gift).ToString());
        return string.Join(",", parts);
    }

    // A drawer that slides in from the left, opened from the map's button column; the Close button
    // or a click beside the drawer closes it. fill adds the contents to the scrolling list.
    void BuildDrawer(RectTransform root, string id, string headerText, Action<ScrollRect> fill)
    {
        float shown = 0f;
        Action<bool> setOpen = open => openDrawer = open ? id : null;

        RectTransform blocker = UIKit.Stretch(UIKit.Panel(root, id + " Drawer Blocker", new Color(0f, 0f, 0f, DrawerDim)));
        var dismiss = blocker.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(() => setOpen(false));
        var blockerFade = blocker.gameObject.AddComponent<CanvasGroup>();

        RectTransform drawer = UIKit.Box(root, id + " Drawer");
        drawer.anchorMin = Vector2.zero;
        drawer.anchorMax = new Vector2(0f, 1f);
        drawer.pivot = new Vector2(0f, 0.5f);
        drawer.sizeDelta = new Vector2(DrawerWidth, -2f * UIKit.Pad);
        UIKit.Layout<VerticalLayoutGroup>(drawer, UIKit.Pad, 6);

        Transform title = UIKit.Group(drawer, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 6);
        Text header = UIKit.Label(title, headerText, UIKit.HeadingSize, color: UIKit.AccentColor);
        header.fontStyle = FontStyle.Bold;
        UIKit.Size(header, flexibleWidth: 1f);
        UIKit.CloseButton(title, "Close", () => setOpen(false));

        ScrollRect list = UIKit.ScrollList(drawer, "Contents", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        fill(list);
        // Focus waits for the slide, and starts on the list rather than on Close.
        UIKit.Focus(blocker, () => setOpen(false), drawer, DrawerSlideSeconds).Default =
            () => list.content.GetComponentInChildren<Button>();

        refreshers.Add(() =>
        {
            bool open = openDrawer == id;
            shown = Mathf.MoveTowards(shown, open ? 1f : 0f, Time.unscaledDeltaTime / DrawerSlideSeconds);
            float eased = shown * shown * (3f - 2f * shown);
            // Whole reference pixels, so the frame's texels stay crisp mid-slide.
            drawer.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(-DrawerWidth, 0f, eased)), 0f);
            blocker.gameObject.SetActive(shown > 0f);
            blockerFade.alpha = eased;
            drawer.gameObject.SetActive(shown > 0f);
        });
    }

    // A captain in the party drawer: the usual details, tagged with their level.
    RectTransform PartyCard(Transform list, RunState run, Captain captain)
    {
        RectTransform card = UIKit.Box(list, captain.displayName, CardColor, blocksClicks: false);
        UIKit.Size(card, flexibleWidth: 1f);
        Button cardButton = null;
        cardButton = UIKit.Clickable(card, () => ShowCaptainDetails(run, captain, cardButton));
        int level = Mathf.Max(captain.level, LevelMath.LevelForXp(run.XpOf(captain)));
        PartyCardBody(card, captain, $"LV {level}", UIKit.AccentColor, out _, out _, live: true);
        return card;
    }

    // The party card's face, shared with run setup: portrait on the left; name and tag, element
    // and role icons, bond, and special on the right.
    // With live, the bond follows approval changes; only for cards built with the screen, since
    // setup rebuilds its cards from inside a refresher, which can't add to the list it is running.
    void PartyCardBody(RectTransform card, Captain captain, string tagText, Color tagColor, out Text name, out Text tag, bool live = false)
    {
        HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(card, 6, 8);
        layout.childAlignment = TextAnchor.UpperLeft;

        // The 32 px portraits at 2x, inset like a picture frame.
        RectTransform frame = UIKit.Box(card, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.Portrait, PortraitSize);

        RectTransform info = UIKit.Group(card, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 2);
        UIKit.Size(info, flexibleWidth: 1f);

        Transform title = UIKit.Group(info, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 4);
        name = UIKit.Label(title, captain.displayName, 15);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        tag = UIKit.Label(title, tagText, 9, TextAnchor.MiddleRight, tagColor);
        tag.fontStyle = FontStyle.Bold;

        ElementData element = match.Data.ElementInfo(captain.element);
        Transform tags = UIKit.Group(info, "Tags");
        UIKit.Layout<HorizontalLayoutGroup>(tags, 0, 4);
        UIKit.Icon(tags, element != null ? element.icon : null, IconSize);
        UIKit.Label(tags, element != null ? element.displayName : captain.element.ToString(), 12, color: UIKit.ElementColor(captain.element));
        UIKit.Size(UIKit.Group(tags, "Gap"), 6f);
        UIKit.Icon(tags, match.Data.RoleIcon(captain.role), IconSize);
        UIKit.Label(tags, captain.ClassName, 12);

        string muted = UIKit.Hex(UIKit.MutedTextColor);
        string Bond() => $"<color={muted}>Bond</color> {BondMath.RankName(ApprovalStore.RankOf(captain))} " +
                         $"<color={muted}>({ApprovalStore.ApprovalOf(captain):0})</color>";
        // Live, so a gift given from the details window shows on the card behind it.
        if (live) UIKit.LiveLabel(info, Bond, refreshers, 12);
        else UIKit.Label(info, Bond(), 12);
        // How far along the bond is, so a rank-up (and the soldier slot it opens) can be seen coming.
        if (live)
            UIKit.LiveBar(info, () => (ApprovalStore.ApprovalOf(captain) - BondMath.MinApproval) / (BondMath.MaxApproval - BondMath.MinApproval),
                () => "", BondBarColor, refreshers, 6f);
        Transform special = UIKit.Group(info, "Special");
        UIKit.Layout<HorizontalLayoutGroup>(special, 0, 4);
        UIKit.Icon(special, captain.Ability != null ? captain.Ability.icon : null, IconSize);
        UIKit.Label(special, captain.Ability != null ? captain.Ability.displayName : "No special", 12);
        // The equipped soldiers, live so a change from the details window shows on the card behind it.
        if (live) UIKit.LiveLabel(info, () => SoldiersLine(captain), refreshers, 11);
    }

    // The captain's soldiers in slot order, each in its rarity's colour.
    string SoldiersLine(Captain captain)
    {
        List<SoldierItem> soldiers = RunState.Current.SoldiersOf(captain);
        string muted = UIKit.Hex(UIKit.MutedTextColor);
        if (soldiers.Count == 0) return $"<color={muted}>No soldiers</color>";
        return $"<color={muted}>Soldiers</color> " +
               string.Join(", ", soldiers.ConvertAll(s => CaptainPanelParts.SoldierName(s, match.Data.ClassData(s.soldierClass))));
    }

    // A window over the map with everything about one captain. A click beside it, or Close,
    // dismisses it and hands focus back to opener; rebuilding the screen dismisses it too.
    void ShowCaptainDetails(RunState run, Captain captain, Selectable opener)
    {
        ModalWindow details = UIKit.Modal(screen, "Captain Details", DetailsWidth, new Vector2(1f, 0.5f),
            new Vector2(-UIKit.Pad, 0f), returnFocusTo: opener);
        RectTransform window = details.Window;

        int level = Mathf.Max(captain.level, LevelMath.LevelForXp(run.XpOf(captain)));
        var detailRefreshers = new List<Action>();
        DetailsHeader(window, run, captain, level, detailRefreshers);

        UIKit.Size(UIKit.Header(window, "Stats"), flexibleWidth: 1f);
        RectTransform stats = UIKit.Group(window, "Stats");
        UIKit.Layout<HorizontalLayoutGroup>(stats, 0, 8).childAlignment = TextAnchor.UpperCenter;
        UIKit.Size(stats, flexibleWidth: 1f);
        // Power, range, speed and Defense include what the soldiers add. Small lines under the
        // values: power and defense against the level-1 base, the rest as a party ranking. Range
        // follows the soldiers, so it has none.
        float power = captain.power + match.StatBonus(captain, CaptainStat.Power);
        Stat(stats, "POWER", $"{power * LevelMath.StatFactor(level):0}", level > 1 ? $"base {captain.power:0}" : PartyRank(run, captain, c => c.power));
        Stat(stats, "RANGE", $"{match.RangeFor(captain):0.#}");
        Stat(stats, "SPEED", $"{captain.speed + match.StatBonus(captain, CaptainStat.Speed):0.##}", PartyRank(run, captain, c => c.speed));
        Stat(stats, "HEALTH", $"{captain.health}", PartyRank(run, captain, c => c.health));
        float defense = DefenseMath.AtLevel(captain.defense, captain.defensePerLevel, level) + match.StatBonus(captain, CaptainStat.Defense);
        Stat(stats, "DEFENSE", $"{defense:0.#}", level > 1 ? $"base {captain.defense:0.#}" : PartyRank(run, captain, c => c.defense));
        Stat(stats, "RESERVE", $"{captain.ResourceCapacity}", PartyRank(run, captain, c => c.ResourceCapacity));
        Stat(stats, "DRAW", $"{captain.ResourceRegen:0.#}/s", PartyRank(run, captain, c => c.ResourceRegen));

        UIKit.Size(UIKit.Header(window, "Special"), flexibleWidth: 1f);
        Transform special = UIKit.Group(window, "Special");
        UIKit.Layout<HorizontalLayoutGroup>(special, 0, 6);
        UIKit.Icon(special, captain.Ability != null ? captain.Ability.icon : null, 32f);
        Text specialName = UIKit.Label(special, captain.Ability != null ? captain.Ability.displayName : "No special ability", 14);
        specialName.fontStyle = FontStyle.Bold;
        if (captain.Ability != null && !string.IsNullOrEmpty(captain.Ability.description))
            UIKit.Label(window, captain.Ability.description, 12, color: UIKit.MutedTextColor);

        UIKit.Size(UIKit.Group(window, "Gap"), height: UIKit.Gap);
        TagLine(window, "Likes", captain.likes, UIKit.GoodColor);
        TagLine(window, "Dislikes", captain.dislikes, UIKit.BadColor);

        // The stats follow the soldiers, so the window is drawn again whenever one is changed.
        BuildLoadout(window, run, captain, () =>
        {
            details.Close(restoreFocus: false);
            ShowCaptainDetails(run, captain, opener);
        });

        UIKit.Size(UIKit.Group(window, "Gap"), height: UIKit.Gap);
        Transform buttons = UIKit.Group(window, "Buttons");
        UIKit.Layout<HorizontalLayoutGroup>(buttons, 0, 6);
        UIKit.Size(buttons, flexibleWidth: 1f);
        Button gift = null;
        gift = UIKit.Button(buttons, "Gift", () => ShowGiftWindow(run, captain, gift, () => UIKit.Refresh(detailRefreshers)));
        UIKit.Size(gift, height: UIKit.ButtonHeight, flexibleWidth: 1f);
        UIKit.CloseButton(buttons, "Close", () => details.Close());
    }

    // ---------- Inventory and gifts ----------

    // A window beside the captain window, not over it, with no second dimming layer. A click
    // beside it dismisses it and hands focus back to opener; rebuilding the screen dismisses it too.
    ModalWindow OpenSideWindow(string name, Selectable opener) =>
        UIKit.Modal(screen, name, GiftWindowWidth, new Vector2(1f, 0.5f), new Vector2(-(DetailsWidth + 2f * UIKit.Pad), 0f),
            returnFocusTo: opener, backdrop: Color.clear);

    // The window for giving a captain a gift: their portrait, what they say, and the items you
    // carry, each marked liked, disliked or neutral. onGiven runs after each gift.
    void ShowGiftWindow(RunState run, Captain captain, Selectable opener, Action onGiven)
    {
        ModalWindow modal = OpenSideWindow("Gift", opener);
        RectTransform window = modal.Window;

        RectTransform top = UIKit.Group(window, "Top");
        UIKit.Layout<HorizontalLayoutGroup>(top, 0, 12).childAlignment = TextAnchor.UpperLeft;
        UIKit.Size(top, flexibleWidth: 1f);
        RectTransform frame = UIKit.Box(top, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.Portrait, DetailsPortraitSize);

        RectTransform info = UIKit.Group(top, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 4);
        UIKit.Size(info, flexibleWidth: 1f);
        UIKit.Shadowed(UIKit.Label(info, captain.displayName, 20)).fontStyle = FontStyle.Bold;
        var live = new List<Action>();
        UIKit.LiveLabel(info, () => $"Bond {BondMath.RankName(ApprovalStore.RankOf(captain))}   Approval {ApprovalStore.ApprovalOf(captain):0}",
            live, 12, color: UIKit.MutedTextColor);
        Text speech = UIKit.Label(info, $"\"{captain.GiftGreeting}\"", 14);
        UIKit.Size(speech, flexibleWidth: 1f);

        UIKit.Size(UIKit.Header(window, "Inventory"), flexibleWidth: 1f);
        ScrollRect list = UIKit.ScrollList(window, "Items", UIKit.Gap);
        UIKit.Size(list, height: GiftListHeight, flexibleWidth: 1f);
        Action<GiftData> give = null;
        give = gift =>
        {
            GiftReaction? reaction = match.GiveGift(captain, gift);
            if (reaction == null) return;
            speech.text = $"\"{captain.GiftLine(reaction.Value)}\"";
            UIKit.Refresh(live);
            onGiven?.Invoke();
            BuildGiftRows(list.content, run, captain, give);
        };
        BuildGiftRows(list.content, run, captain, give);
        UIKit.Refresh(live);
        UIKit.CloseButton(window, "Close", () => modal.Close());
    }

    // One row per item carried. With a captain, each row says how they feel about the item and has
    // a Give button; without one, the rows are just a list.
    void BuildGiftRows(Transform list, RunState run, Captain captain, Action<GiftData> give)
    {
        UIKit.Clear(list);
        bool any = false;
        foreach (GiftData gift in match.Data.shopGifts)
        {
            int count = run.GiftCount(gift);
            if (gift == null || count <= 0) continue;
            any = true;
            RectTransform row = UIKit.Box(list, gift.displayName, CardColor, blocksClicks: false);
            UIKit.Size(row, flexibleWidth: 1f);
            UIKit.Layout<HorizontalLayoutGroup>(row, 6, 8).childAlignment = TextAnchor.MiddleLeft;
            UIKit.Icon(row, gift.icon, 32f);

            RectTransform text = UIKit.Group(row, "Text");
            UIKit.Layout<VerticalLayoutGroup>(text, 0, 1);
            UIKit.Size(text, flexibleWidth: 1f);
            UIKit.Label(text, count > 1 ? $"{gift.displayName}  x{count}" : gift.displayName, 14).fontStyle = FontStyle.Bold;
            if (captain == null)
            {
                if (!string.IsNullOrEmpty(gift.description)) UIKit.Label(text, gift.description, 11, color: UIKit.MutedTextColor);
                continue;
            }
            // With a captain, the second line says how they feel about it instead.
            GiftReaction reaction = captain.ReactionTo(gift);
            string change = $"{gift.ApprovalFor(reaction):+0;-0;0}";
            Color color = reaction == GiftReaction.Liked ? UIKit.GoodColor : reaction == GiftReaction.Disliked ? UIKit.BadColor : UIKit.MutedTextColor;
            string word = reaction == GiftReaction.Liked ? "Likes it" : reaction == GiftReaction.Disliked ? "Dislikes it" : "Neutral";
            UIKit.Label(text, $"{word}  {change}", 12, color: color);
            GiftData chosen = gift;
            UIKit.Button(row, "Give", () => give(chosen), 60f, 24f);
        }
        if (!any)
            UIKit.Label(list, captain == null ? "Nothing yet. Buy gifts at a shop." : "You have no gifts to give. Buy some at a shop.",
                13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
    }

    // Portrait, then name, level and XP, element (with its status), role and bond.
    void DetailsHeader(Transform window, RunState run, Captain captain, int level, List<Action> detailRefreshers)
    {
        RectTransform top = UIKit.Group(window, "Top");
        UIKit.Layout<HorizontalLayoutGroup>(top, 0, 12).childAlignment = TextAnchor.UpperLeft;
        UIKit.Size(top, flexibleWidth: 1f);
        RectTransform frame = UIKit.Box(top, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.Portrait, DetailsPortraitSize);

        RectTransform info = UIKit.Group(top, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 3);
        UIKit.Size(info, flexibleWidth: 1f);

        Transform title = UIKit.Group(info, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 6);
        Text name = UIKit.Shadowed(UIKit.Label(title, captain.displayName, 20));
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);

        // Nothing here changes while the window is open, so the meters are drawn once rather than
        // joining the screen's refreshers, which would outlive the window.
        // The bond meter is redrawn when a gift is given, through detailRefreshers.
        var drawOnce = detailRefreshers;
        int xp = run.XpOf(captain);
        bool maxLevel = level >= LevelMath.MaxLevel;
        int from = LevelMath.XpForLevel(level), to = LevelMath.XpForLevel(level + 1);
        UIKit.LiveBar(info, () => maxLevel ? 1f : (xp - from) / (float)Mathf.Max(1, to - from),
            () => maxLevel ? $"Level {level}  (max)" : $"Level {level}   XP {xp}/{to}", UIKit.XpBarColor, drawOnce);

        ElementData element = match.Data.ElementInfo(captain.element);
        Transform elementRow = UIKit.Group(info, "Element");
        UIKit.Layout<HorizontalLayoutGroup>(elementRow, 0, 4);
        UIKit.Icon(elementRow, element != null ? element.icon : null, IconSize);
        UIKit.Label(elementRow, element != null ? element.displayName : captain.element.ToString(), 12, color: UIKit.ElementColor(captain.element));
        if (element != null && !string.IsNullOrEmpty(element.statusName))
            UIKit.Label(elementRow, $"inflicts {element.statusName}", 12, color: UIKit.MutedTextColor);
        UIKit.Size(UIKit.Group(elementRow, "Gap"), 6f);
        UIKit.Icon(elementRow, match.Data.RoleIcon(captain.role), IconSize);
        UIKit.Label(elementRow, captain.ClassName, 12);
        UIKit.Label(info, $"Trait: {CaptainTraits.Name(captain.Trait)} - {CaptainTraits.Description(captain.Trait)}", 11, color: UIKit.MutedTextColor);
        if (RunState.Current?.RivalOf(captain) is { } rival) UIKit.Label(info, $"Rival: {rival.displayName}. Granting one's requests costs the other approval.", 11, color: UIKit.BadColor);

        UIKit.LiveBar(info, () => (ApprovalStore.ApprovalOf(captain) - BondMath.MinApproval) / (BondMath.MaxApproval - BondMath.MinApproval),
            () => $"Bond {BondMath.RankName(ApprovalStore.RankOf(captain))}   Approval {ApprovalStore.ApprovalOf(captain):0}", BondBarColor, drawOnce);
        UIKit.LiveLabel(info, () => NextRankLine(captain), drawOnce, 11, color: UIKit.MutedTextColor);
        UIKit.Refresh(drawOnce);
    }

    // What the next bond rank takes and gives, e.g. "Next: B at 65 (opens Slot 4)".
    internal static string NextRankLine(Captain captain)
    {
        int rank = ApprovalStore.RankOf(captain);
        if (rank >= BondMath.MaxRank) return "Bond is at its highest rank";
        int next = rank + 1;
        string reward = BondMath.SoldierCount(next) > BondMath.SoldierCount(rank)
            ? $"opens Slot {BondMath.SoldierCount(next)}"
            : "stronger status and charge";
        return $"Next: {BondMath.RankName(next)} at {BondMath.ApprovalForRank(next):0} ({reward})";
    }

    internal static void TagLine(Transform parent, string caption, CaptainTag[] tags, Color color)
    {
        Transform row = UIKit.Group(parent, caption);
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, 8);
        Text label = UIKit.Label(row, caption.ToUpperInvariant(), 10, color: color);
        label.fontStyle = FontStyle.Bold;
        UIKit.Size(label, 64f);
        UIKit.Label(row, string.Join(", ", tags), 12);
    }

    // The soldier equipped in each slot, saved with the run and fielded when the captain's tower is
    // placed; a slot's button opens the soldier picker. Slots the captain's bond hasn't opened yet
    // say when they open. onChanged runs after a soldier is equipped or removed.
    void BuildLoadout(Transform window, RunState run, Captain captain, Action onChanged)
    {
        UIKit.Size(UIKit.Header(window, "Soldiers"), flexibleWidth: 1f);
        UIKit.Label(window, "Soldiers take this captain's element and add a stat by class: Knight Defense, Archer Range, " +
                            "Mage Power, Rogue Speed. Click a slot to change its soldier.", 10, color: UIKit.MutedTextColor);
        int open = match.SoldierCountFor(captain);
        for (int i = 0; i < RunState.MaxSoldierSlots; i++)
        {
            int slot = i;
            Transform row = UIKit.Group(window, $"Slot {slot + 1}");
            UIKit.Layout<HorizontalLayoutGroup>(row, 0, 4);
            UIKit.Size(row, flexibleWidth: 1f);
            Text label = UIKit.Label(row, $"Slot {slot + 1}", 12, color: UIKit.MutedTextColor);
            UIKit.Size(label, 64f);
            if (slot >= open)
            {
                UIKit.Size(UIKit.Label(row, $"Opens at bond {BondMath.RankName(RankOpening(slot))}", 12, color: UIKit.DisabledTextColor),
                    height: 22f, flexibleWidth: 1f);
                continue;
            }

            SoldierItem soldier = run.Soldiers.InSlot(captain.id, slot);
            Button button = null;
            button = UIKit.Button(row, soldier != null ? SoldierSummary(captain, soldier) : "Empty", () =>
                ShowSoldierPicker(run, captain, slot, button, onChanged), height: 22f);
            UIKit.Size(button, height: 22f, flexibleWidth: 1f);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = 12;
            if (soldier == null) text.color = UIKit.DisabledTextColor;
        }
    }

    // "Rare Knight  +0.75 Defense  Melee", with a word on the captain's taste for the class.
    string SoldierSummary(Captain captain, SoldierItem soldier)
    {
        SoldierClassData soldierClass = match.Data.ClassData(soldier.soldierClass);
        string summary = $"{CaptainPanelParts.SoldierName(soldier, soldierClass)}  {CaptainPanelParts.SoldierBonus(soldier, soldierClass)}";
        if (soldierClass != null) summary += $"  {soldierClass.damageType}";
        if (soldierClass == null || !Relationships.TryGetClassTag(soldierClass, out CaptainTag tag)) return summary;
        if (Array.IndexOf(captain.likes, tag) >= 0) return summary + $"  <color={UIKit.Hex(UIKit.GoodColor)}>liked</color>";
        return Array.IndexOf(captain.dislikes, tag) >= 0 ? summary + $"  <color={UIKit.Hex(UIKit.BadColor)}>disliked</color>" : summary;
    }

    // Beside the captain window: every soldier owned, those in the inventory first, rarest first.
    // Equip puts one in the slot, moving it from another captain if it was theirs, and Remove sends
    // the slot's soldier back to the inventory. Either closes the picker and runs onChanged.
    void ShowSoldierPicker(RunState run, Captain captain, int slot, Selectable opener, Action onChanged)
    {
        ModalWindow modal = OpenSideWindow("Soldiers", opener);
        RectTransform window = modal.Window;
        UIKit.Size(UIKit.Header(window, $"{captain.displayName}  Slot {slot + 1}"), flexibleWidth: 1f);

        void Change(Action change)
        {
            change();
            RunSave.Save(run);
            modal.Close();
            onChanged?.Invoke();
        }

        soldierPickerFilter.Reset();
        ScrollRect list = null;
        void Fill()
            {
            UIKit.Clear(list.content);
            var soldiers = soldierPickerFilter.Apply(run.Soldiers.Items, s => SoldierValues(run, s));
            soldiers.Sort((a, b) => a.IsEquipped != b.IsEquipped ? a.IsEquipped.CompareTo(b.IsEquipped)
                : b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : a.soldierClass.CompareTo(b.soldierClass));
            foreach (SoldierItem soldier in soldiers)
            {
                bool here = soldier.equippedTo == captain.id && soldier.slot == slot;
                RectTransform row = UIKit.Box(list.content, soldier.id, CardColor, blocksClicks: false);
                UIKit.Size(row, flexibleWidth: 1f);
                UIKit.Layout<HorizontalLayoutGroup>(row, 6, 8).childAlignment = TextAnchor.MiddleLeft;
                SoldierClassData soldierClass = match.Data.ClassData(soldier.soldierClass);
                UIKit.Icon(row, soldierClass != null ? soldierClass.portrait : null, 32f);
    
                RectTransform text = UIKit.Group(row, "Text");
                UIKit.Layout<VerticalLayoutGroup>(text, 0, 1);
                UIKit.Size(text, flexibleWidth: 1f);
                UIKit.Label(text, SoldierSummary(captain, soldier), 13);
                UIKit.Label(text, here ? "In this slot" : WhereIs(run, soldier), 11, color: UIKit.MutedTextColor);
                SoldierItem chosen = soldier;
                if (here) UIKit.Button(row, "Remove", () => Change(() => run.Soldiers.Unequip(chosen.id)), 70f, 24f);
                else UIKit.Button(row, "Equip", () => Change(() => run.Soldiers.Equip(chosen.id, captain.id, slot)), 70f, 24f);
            }
            if (soldiers.Count == 0) ListFilterBar.Empty(list.content, run.Soldiers.Items.Count == 0, "You have no soldiers yet.");
        }
        ListFilterBar.Soldiers(window, soldierPickerFilter, Fill);
        list = UIKit.ScrollList(window, "Soldiers", UIKit.Gap);
        UIKit.Size(list, height: GiftListHeight, flexibleWidth: 1f);
        Fill();

        UIKit.CloseButton(window, "Close", () => modal.Close());
    }

    static string WhereIs(RunState run, SoldierItem soldier)
    {
        if (!soldier.IsEquipped) return "In the inventory";
        Captain owner = run.Profile.Find(soldier.equippedTo);
        return owner != null ? $"With {owner.displayName}, slot {soldier.slot + 1}" : "With another captain";
    }

    // The lowest bond rank whose soldier count reaches past this slot.
    int RankOpening(int slot)
    {
        for (int rank = 0; rank <= BondMath.MaxRank; rank++)
            if (Mathf.Max(match.Data.soldiersPerTower, BondMath.SoldierCount(rank)) > slot) return rank;
        return BondMath.MaxRank;
    }
}
