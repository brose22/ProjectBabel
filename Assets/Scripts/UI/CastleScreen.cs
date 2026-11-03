using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Castle (item: castle), home between runs: the inventory, with captains to equip soldiers to
// and release, and setting out, which picks the party and then a location. Built in uGUI at runtime
// like the main menu; the Castle scene only holds a camera and this component.
public class CastleScreen : MonoBehaviour
{
    public const string SceneName = "Castle";

    // The party taken on a run (item: party).
    const int MaxParty = 5;
    const float InventoryWidth = 900f;
    const float InventoryBodyHeight = 380f;
    const float CaptainListWidth = 380f;
    const float CardHeight = 76f;
    const float PortraitSize = 64f;
    const float IconSize = 16f;
    const int PartyColumns = 3;

    static readonly Color BackgroundColor = new(0.05f, 0.06f, 0.09f, 1f);
    static readonly Color CardColor = new(0.17f, 0.2f, 0.28f, 1f);
    static readonly Color PickedCardColor = new(0.27f, 0.24f, 0.2f, 1f);

    [Tooltip("The captain templates, soldier classes and starting values the Castle reads.")]
    [SerializeField] MatchData data;

    Canvas canvas;
    RectTransform screen;
    readonly List<Action> refreshers = new();
    Profile profile;

    enum Tab { Captains, Soldiers }

    Tab tab;
    // The captain shown on the right of the Captains tab, and the slot whose soldier is being picked
    // (-1 while none is).
    Captain selected;
    int pickingSlot = -1;
    readonly List<Captain> party = new();

    public static void Open() => SceneManager.LoadScene(SceneName);

    // Whatever run there was has ended or been given up by now; the profile is loaded from its save
    // (or made afresh) unless it is still in memory from the run.
    void Awake()
    {
        RunState.Clear();
        if (Profile.Current == null && !ProfileSave.TryLoad(data.captainTemplates)) Profile.StartNew(data.startingGold);
        profile = Profile.Current;
        GiveStarterCaptains();
        ProfileSave.Save(profile);
    }

    void Start()
    {
        canvas = UIKit.CreateCanvas("Castle", 0);
        BuildHome();
    }

    void LateUpdate() => UIKit.Refresh(refreshers);

    // Until the starting captains are picked on the first play (#33), an empty roster gets a few
    // rolled captains, each with a few soldiers, so there is always a party to take.
    void GiveStarterCaptains()
    {
        if (profile.Roster.Count > 0) return;
        int count = Mathf.Min(Profile.MaxRoster, CaptainOffer.Size(GameSettings.CaptainsOffered, data.partySize));
        foreach (Captain captain in MatchManager.RollCaptains(data, profile.Roster, count, UnityEngine.Random.Range(int.MinValue, int.MaxValue)))
            if (profile.TryAdd(captain)) MatchManager.GiveStartingSoldiers(data, profile.Soldiers, new[] { captain });
    }

    // A full-screen page with a centred column; the gamepad's cancel button runs back.
    RectTransform NewScreen(string name, float width, Action back)
    {
        refreshers.Clear();
        if (screen != null) Destroy(screen.gameObject);
        screen = UIKit.Stretch(UIKit.Panel(canvas.transform, name, BackgroundColor));
        UIKit.Focus(screen, back);
        RectTransform column = UIKit.Group(screen, "Column");
        UIKit.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0f));
        VerticalLayoutGroup layout = UIKit.Layout<VerticalLayoutGroup>(column, 0, 10, fitContent: true);
        layout.childAlignment = TextAnchor.UpperCenter;
        return column;
    }

    static Text Title(Transform parent, string text)
    {
        Text title = UIKit.Shadowed(UIKit.Label(parent, text, 30, TextAnchor.MiddleCenter));
        title.fontStyle = FontStyle.Bold;
        return title;
    }

    string Summary() =>
        $"<color={UIKit.Hex(UIKit.AccentColor)}>{profile.Gold} Gold</color>    Captains {profile.Roster.Count}/{Profile.MaxRoster}    Soldiers {profile.Soldiers.Items.Count}";

    static RectTransform Row(Transform parent, string name, float spacing)
    {
        RectTransform row = UIKit.Group(parent, name);
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, spacing, fitContent: true);
        return row;
    }

    // ---------- Home ----------

    void BuildHome()
    {
        RectTransform column = NewScreen("Castle", 400f, null);
        Title(column, "THE CASTLE");
        UIKit.Label(column, Summary(), 14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        UIKit.Size(UIKit.Group(column, "Gap"), height: 16f);

        UIKit.Primary(UIKit.Button(column, "Set out", BuildParty, 240f, 34f), refreshers);
        UIKit.Button(column, "Inventory", () => { pickingSlot = -1; BuildInventory(); }, 240f, 34f);
        UIKit.Button(column, "Main menu", MainMenu.Open, 240f, 34f);
    }

    // ---------- Inventory ----------

    void BuildInventory()
    {
        if (selected != null && !profile.Owns(selected)) selected = null;
        if (selected == null && profile.Roster.Count > 0) selected = profile.Roster[0];
        // Cancel steps back out of the soldier picker first.
        RectTransform column = NewScreen(pickingSlot >= 0 ? "Inventory Picker" : "Inventory", InventoryWidth,
            pickingSlot >= 0 ? () => { pickingSlot = -1; BuildInventory(); } : BuildHome);
        UIKit.Label(column, "INVENTORY", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, Summary(), 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform tabs = Row(column, "Tabs", 6);
        foreach (Tab each in Enum.GetValues(typeof(Tab)))
        {
            Tab chosen = each;
            UIKit.LiveButton(tabs, () => chosen.ToString(), () => { tab = chosen; pickingSlot = -1; BuildInventory(); }, refreshers,
                selected: () => chosen == tab, width: 140f, height: 30f);
        }

        RectTransform body = UIKit.Group(column, "Body");
        UIKit.Layout<HorizontalLayoutGroup>(body, 0, 10).childControlHeight = true;
        UIKit.Size(body, InventoryWidth, InventoryBodyHeight);
        if (tab == Tab.Captains) BuildCaptainsTab(body);
        else BuildSoldiersTab(body);

        UIKit.PadBadge(UIKit.Button(column, "Back", BuildHome, 200f, 32f), PadGlyph.East);
    }

    // The roster on the left; the picked captain's soldier slots (or the soldier picker for one of
    // them) on the right.
    void BuildCaptainsTab(Transform body)
    {
        RectTransform panel = UIKit.Box(body, "Roster");
        UIKit.Size(panel, CaptainListWidth, InventoryBodyHeight, flexibleWidth: 0f);
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 6);
        Header(panel, $"CAPTAINS   <color={UIKit.Hex(UIKit.MutedTextColor)}>{profile.Roster.Count}/{Profile.MaxRoster}</color>");
        ScrollRect list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        foreach (Captain captain in profile.Roster)
        {
            Captain each = captain;
            CaptainCard(list.content, captain, captain == selected ? "SHOWN" : $"LV {Level(captain)}", () =>
            {
                selected = each;
                pickingSlot = -1;
                BuildInventory();
            }, captain == selected);
        }
        if (profile.Roster.Count == 0)
            UIKit.Label(list.content, "No captains in the roster.", 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform side = UIKit.Box(body, "Captain");
        UIKit.Size(side, height: InventoryBodyHeight, flexibleWidth: 1f);
        UIKit.Layout<VerticalLayoutGroup>(side, UIKit.Pad, 6);
        if (selected == null) return;
        if (pickingSlot >= 0) BuildSoldierPicker(side, selected, pickingSlot);
        else BuildLoadout(side, selected);
    }

    // The captain's 4 slots, each a button that opens the soldier picker, and Release.
    void BuildLoadout(Transform side, Captain captain)
    {
        Header(side, $"{captain.displayName.ToUpperInvariant()}   <color={UIKit.Hex(UIKit.MutedTextColor)}>LV {Level(captain)}  {captain.ClassName}</color>");
        UIKit.Label(side, "Soldiers take this captain's element and add a stat by class: Knight Defense, Archer Range, " +
                          "Mage Power, Rogue Speed. A slot fights once the captain's bond opens it.", 11, color: UIKit.MutedTextColor);
        int open = MatchManager.SoldierCountFor(data, captain);
        for (int i = 0; i < SoldierInventory.SlotsPerCaptain; i++)
        {
            int slot = i;
            RectTransform row = UIKit.Group(side, $"Slot {slot + 1}");
            UIKit.Layout<HorizontalLayoutGroup>(row, 0, 6);
            UIKit.Size(row, flexibleWidth: 1f);
            string label = CaptainPanelParts.SlotLabel(slot) + (slot >= open ? "\n<size=9>not open yet</size>" : "");
            UIKit.Size(UIKit.Label(row, label, 12, color: slot < open ? UIKit.MutedTextColor : UIKit.DisabledTextColor), 80f);
            SoldierItem soldier = profile.Soldiers.InSlot(captain.id, slot);
            Button button = UIKit.Button(row, soldier != null ? SoldierSummary(soldier) : "Empty", () =>
            {
                pickingSlot = slot;
                BuildInventory();
            }, height: 28f);
            UIKit.Size(button, height: 28f, flexibleWidth: 1f);
            if (soldier == null) button.GetComponentInChildren<Text>().color = UIKit.DisabledTextColor;
        }

        UIKit.Size(UIKit.Group(side, "Spacer"), flexibleWidth: 1f).flexibleHeight = 1f;
        Button release = UIKit.Button(side, $"Release {captain.displayName}", () => BuildConfirmRelease(captain), height: 26f);
        UIKit.Size(release, height: 26f, flexibleWidth: 1f);
        release.GetComponent<Image>().color = UIKit.BadColor;
    }

    // Every soldier owned, those in the inventory first, rarest first. Equip puts one in the slot,
    // moving it from another captain if it was theirs; Remove sends the slot's soldier back.
    void BuildSoldierPicker(Transform side, Captain captain, int slot)
    {
        Header(side, $"{captain.displayName.ToUpperInvariant()}   <color={UIKit.Hex(UIKit.MutedTextColor)}>{CaptainPanelParts.SlotLabel(slot)}</color>");
        void Change(Action change)
        {
            change();
            ProfileSave.Save(profile);
            pickingSlot = -1;
            BuildInventory();
        }

        ScrollRect list = UIKit.ScrollList(side, "Soldiers", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        foreach (SoldierItem soldier in SortedSoldiers())
        {
            SoldierItem chosen = soldier;
            bool here = soldier.equippedTo == captain.id && soldier.slot == slot;
            RectTransform row = SoldierRow(list.content, soldier, here ? "In this slot" : WhereIs(soldier));
            if (here) UIKit.Button(row, "Remove", () => Change(() => profile.Soldiers.Unequip(chosen.id)), 70f, 24f);
            else UIKit.Button(row, "Equip", () => Change(() => profile.Soldiers.Equip(chosen.id, captain.id, slot)), 70f, 24f);
        }
        if (profile.Soldiers.Items.Count == 0)
            UIKit.Label(list.content, "You have no soldiers yet.", 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        Button back = UIKit.Button(side, "Back", () => { pickingSlot = -1; BuildInventory(); }, height: 26f);
        UIKit.Size(back, height: 26f, flexibleWidth: 1f);
    }

    // Every soldier owned and who has it; an equipped one can be sent back to the inventory.
    void BuildSoldiersTab(Transform body)
    {
        RectTransform panel = UIKit.Box(body, "Soldiers");
        UIKit.Size(panel, height: InventoryBodyHeight, flexibleWidth: 1f);
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 6);
        int free = profile.Soldiers.Unequipped.Count;
        Header(panel, $"SOLDIERS   <color={UIKit.Hex(UIKit.MutedTextColor)}>{profile.Soldiers.Items.Count} owned, {free} in the inventory</color>");
        ScrollRect list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        foreach (SoldierItem soldier in SortedSoldiers())
        {
            SoldierItem chosen = soldier;
            RectTransform row = SoldierRow(list.content, soldier, WhereIs(soldier));
            if (!soldier.IsEquipped) continue;
            UIKit.Button(row, "Unequip", () =>
            {
                profile.Soldiers.Unequip(chosen.id);
                ProfileSave.Save(profile);
                BuildInventory();
            }, 80f, 24f);
        }
        if (profile.Soldiers.Items.Count == 0)
            UIKit.Label(list.content, "You have no soldiers yet.", 13, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        UIKit.Label(panel, "Equip soldiers from a captain on the Captains tab.", 11, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
    }

    List<SoldierItem> SortedSoldiers()
    {
        var soldiers = new List<SoldierItem>(profile.Soldiers.Items);
        soldiers.Sort((a, b) => a.IsEquipped != b.IsEquipped ? a.IsEquipped.CompareTo(b.IsEquipped)
            : b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : a.soldierClass.CompareTo(b.soldierClass));
        return soldiers;
    }

    // Class portrait, name and bonus, and a line under them; buttons go on the end.
    RectTransform SoldierRow(Transform list, SoldierItem soldier, string where)
    {
        RectTransform row = UIKit.Box(list, soldier.id, CardColor, blocksClicks: false);
        UIKit.Size(row, flexibleWidth: 1f);
        UIKit.Layout<HorizontalLayoutGroup>(row, 6, 8).childAlignment = TextAnchor.MiddleLeft;
        SoldierClassData soldierClass = data.ClassData(soldier.soldierClass);
        UIKit.Icon(row, soldierClass != null ? soldierClass.portrait : null, 32f);
        RectTransform text = UIKit.Group(row, "Text");
        UIKit.Layout<VerticalLayoutGroup>(text, 0, 1);
        UIKit.Size(text, flexibleWidth: 1f);
        UIKit.Label(text, SoldierSummary(soldier), 13);
        UIKit.Label(text, where, 11, color: UIKit.MutedTextColor);
        return row;
    }

    // "Rare Knight  +0.75 Defense  Melee".
    string SoldierSummary(SoldierItem soldier)
    {
        SoldierClassData soldierClass = data.ClassData(soldier.soldierClass);
        string summary = $"{CaptainPanelParts.SoldierName(soldier, soldierClass)}  {CaptainPanelParts.SoldierBonus(soldier, soldierClass)}";
        return soldierClass != null ? summary + $"  {soldierClass.damageType}" : summary;
    }

    string WhereIs(SoldierItem soldier)
    {
        if (!soldier.IsEquipped) return "In the inventory";
        Captain owner = profile.Find(soldier.equippedTo);
        return owner != null ? $"With {owner.displayName}, slot {soldier.slot + 1}" : "With another captain";
    }

    void BuildConfirmRelease(Captain captain)
    {
        RectTransform column = NewScreen("Confirm", 460f, BuildInventory);
        UIKit.Label(column, $"RELEASE {captain.displayName.ToUpperInvariant()}?", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, "They leave your roster for good. Their soldiers go back to the inventory.", 14,
            TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        RectTransform buttons = Row(column, "Buttons", 10);
        Button release = UIKit.Button(buttons, "Release", () =>
        {
            profile.Release(captain);
            party.Remove(captain);
            ProfileSave.Save(profile);
            selected = null;
            BuildInventory();
        }, 180f, 32f);
        release.GetComponent<Image>().color = UIKit.BadColor;
        UIKit.PadBadge(UIKit.Button(buttons, "Cancel", BuildInventory, 140f, 32f), PadGlyph.East);
    }

    // ---------- Setting out ----------

    int PartyLimit => Mathf.Min(MaxParty, data.maxPartySize, profile.Roster.Count);

    // The roster as cards, three to a row; clicking one adds or removes them.
    void BuildParty()
    {
        party.RemoveAll(c => !profile.Owns(c));
        RectTransform column = NewScreen("Party", InventoryWidth, BuildHome);
        UIKit.Label(column, "CHOOSE YOUR PARTY", 24, TextAnchor.MiddleCenter);
        UIKit.LiveLabel(column, () => $"Up to {PartyLimit} captains   <color={UIKit.Hex(UIKit.AccentColor)}>{party.Count}/{PartyLimit} picked</color>",
            refreshers, 14, TextAnchor.MiddleCenter, UIKit.MutedTextColor);

        RectTransform panel = UIKit.Box(column, "Roster");
        UIKit.Size(panel, InventoryWidth, InventoryBodyHeight);
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 6);
        ScrollRect list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        for (int i = 0; i < profile.Roster.Count; i += PartyColumns)
        {
            RectTransform row = UIKit.Group(list.content, "Row");
            UIKit.Layout<HorizontalLayoutGroup>(row, 0, UIKit.Gap);
            UIKit.Size(row, flexibleWidth: 1f);
            for (int j = i; j < i + PartyColumns; j++)
            {
                // A blank cell keeps a short last row's cards at the same width as the rest.
                if (j >= profile.Roster.Count)
                {
                    UIKit.Size(UIKit.Group(row, "Blank"), 0f, flexibleWidth: 1f);
                    continue;
                }
                Captain captain = profile.Roster[j];
                PartyCard(row, captain);
            }
        }

        RectTransform buttons = Row(column, "Buttons", 10);
        UIKit.Primary(UIKit.LiveButton(buttons, () => party.Count > 0 ? "Choose a location" : "Pick a captain", BuildLocations, refreshers,
            () => party.Count > 0, width: 220f, height: 32f), refreshers);
        UIKit.PadBadge(UIKit.Button(buttons, "Back", BuildHome, 140f, 32f), PadGlyph.East);
    }

    // Picked cards get a gold ring, name and tag; the rest fade once the party is full.
    void PartyCard(Transform row, Captain captain)
    {
        RectTransform card = CaptainCard(row, captain, "IN PARTY", () =>
        {
            if (!party.Remove(captain) && party.Count < PartyLimit) party.Add(captain);
        }, false, out Text name, out Text tag);
        UIKit.Size(card, 0f, CardHeight, 1f);
        var image = card.GetComponent<Image>();
        var fade = card.gameObject.AddComponent<CanvasGroup>();
        Image ring = UIKit.Outline(card, UIKit.AccentColor);
        ring.rectTransform.offsetMin = new Vector2(2f, 2f);
        ring.rectTransform.offsetMax = new Vector2(-2f, -2f);
        refreshers.Add(() =>
        {
            bool picked = party.Contains(captain);
            image.color = picked ? PickedCardColor : CardColor;
            ring.enabled = picked;
            tag.enabled = picked;
            name.color = picked ? UIKit.AccentColor : UIKit.TextColor;
            fade.alpha = picked || party.Count < PartyLimit ? 1f : 0.45f;
        });
    }

    void BuildLocations()
    {
        RectTransform column = NewScreen("Locations", 520f, BuildParty);
        UIKit.Label(column, "CHOOSE A LOCATION", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, $"Party: {string.Join(", ", party.ConvertAll(c => c.displayName))}", 13, TextAnchor.MiddleCenter,
            UIKit.MutedTextColor);
        // One row per location, in unlock order: its name and description, and a button that sets
        // out for it while it is open.
        foreach (LocationSelect.Entry entry in LocationSelect.Entries(data, profile))
        {
            LocationData location = entry.location;
            RectTransform row = UIKit.Box(column, location.displayName, entry.unlocked ? null : UIKit.InsetColor, blocksClicks: false);
            UIKit.Size(row, 520f);
            UIKit.Layout<HorizontalLayoutGroup>(row, 8, 8).childAlignment = TextAnchor.MiddleLeft;
            RectTransform text = UIKit.Group(row, "Text");
            UIKit.Layout<VerticalLayoutGroup>(text, 0, 1);
            UIKit.Size(text, flexibleWidth: 1f);
            Text name = UIKit.Label(text, location.displayName, 15, color: entry.unlocked ? UIKit.TextColor : UIKit.DisabledTextColor);
            name.fontStyle = FontStyle.Bold;
            UIKit.Label(text, entry.unlocked ? location.description : "Win the location before it to open this one.", 11,
                color: UIKit.MutedTextColor);
            Button button = UIKit.Button(row, entry.unlocked ? "Set out" : "Locked", () => SetOut(location), 110f, 28f);
            button.interactable = entry.unlocked;
        }
        UIKit.PadBadge(UIKit.Button(column, "Back", BuildParty, 200f, 32f), PadGlyph.East);
    }

    // Starts a run at the location with the picked party; the match makes its world map.
    void SetOut(LocationData location)
    {
        if (party.Count == 0 || !LocationSelect.TryStartRun(data, profile, location)) return;
        RunState.Current.SetParty(party);
        ProfileSave.Save(profile);
        SceneManager.LoadScene(MainMenu.MatchSceneName);
    }

    // ---------- Cards ----------

    static void Header(Transform parent, string text) => UIKit.Label(parent, text, 10, color: UIKit.AccentColor).fontStyle = FontStyle.Bold;

    RectTransform CaptainCard(Transform parent, Captain captain, string tagText, Action onClick, bool highlighted) =>
        CaptainCard(parent, captain, tagText, onClick, highlighted, out _, out _);

    // Portrait on the left; name and tag, element and role, bond and soldiers on the right.
    RectTransform CaptainCard(Transform parent, Captain captain, string tagText, Action onClick, bool highlighted, out Text name, out Text tag)
    {
        RectTransform card = UIKit.Box(parent, captain.displayName, highlighted ? PickedCardColor : CardColor, blocksClicks: false);
        UIKit.Size(card, height: CardHeight, flexibleWidth: 1f);
        UIKit.Clickable(card, onClick);
        HorizontalLayoutGroup layout = UIKit.Layout<HorizontalLayoutGroup>(card, 6, 8);
        layout.childAlignment = TextAnchor.UpperLeft;

        RectTransform frame = UIKit.Box(card, "Portrait", UIKit.InsetColor, blocksClicks: false);
        UIKit.Layout<HorizontalLayoutGroup>(frame, 2, 0);
        UIKit.Icon(frame, captain.Portrait, PortraitSize);

        RectTransform info = UIKit.Group(card, "Info");
        UIKit.Layout<VerticalLayoutGroup>(info, 0, 2);
        UIKit.Size(info, flexibleWidth: 1f);
        Transform title = UIKit.Group(info, "Title");
        UIKit.Layout<HorizontalLayoutGroup>(title, 0, 4);
        name = UIKit.Label(title, captain.displayName, 14, color: highlighted ? UIKit.AccentColor : UIKit.TextColor);
        name.fontStyle = FontStyle.Bold;
        UIKit.Size(name, flexibleWidth: 1f);
        tag = UIKit.Label(title, tagText, 9, TextAnchor.MiddleRight, UIKit.AccentColor);
        tag.fontStyle = FontStyle.Bold;

        ElementData element = data.ElementInfo(captain.element);
        Transform tags = UIKit.Group(info, "Tags");
        UIKit.Layout<HorizontalLayoutGroup>(tags, 0, 4);
        UIKit.Icon(tags, element != null ? element.icon : null, IconSize);
        UIKit.Label(tags, element != null ? element.displayName : captain.element.ToString(), 12, color: UIKit.ElementColor(captain.element));
        UIKit.Icon(tags, data.RoleIcon(captain.role), IconSize);
        UIKit.Label(tags, captain.ClassName, 12);

        string muted = UIKit.Hex(UIKit.MutedTextColor);
        UIKit.Label(info, $"<color={muted}>Lv</color> {Level(captain)}   <color={muted}>Bond</color> {BondMath.RankName(ApprovalStore.RankOf(captain))}", 12);
        UIKit.Label(info, SoldiersLine(captain), 11);
        return card;
    }

    // The level the captain has reached, counting XP not yet levelled up.
    static int Level(Captain captain) => Mathf.Max(captain.level, LevelMath.LevelForXp(captain.xp));

    // The captain's soldiers in slot order, each in its rarity's colour.
    string SoldiersLine(Captain captain)
    {
        List<SoldierItem> soldiers = profile.SoldiersOf(captain);
        string muted = UIKit.Hex(UIKit.MutedTextColor);
        if (soldiers.Count == 0) return $"<color={muted}>No soldiers</color>";
        return $"<color={muted}>Soldiers</color> " +
               string.Join(", ", soldiers.ConvertAll(s => CaptainPanelParts.SoldierName(s, data.ClassData(s.soldierClass))));
    }
}
