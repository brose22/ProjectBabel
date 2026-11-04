using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Castle (item: castle), home between runs: the shop (CastleScreen.Shop.cs), the inventory, with
// captains to equip soldiers to and release, and setting out, which picks the party and then a
// location. Built in uGUI at runtime like the main menu; the Castle scene only holds a camera and
// this component. The first time the game is played, the starting captains are picked here first
// (CastleScreen.Starting.cs).
public partial class CastleScreen : MonoBehaviour
{
    public const string SceneName = "Castle";

    // The party taken on a run (item: party).
    const int MaxParty = 5;
    const float InventoryWidth = 900f;
    const float InventoryBodyHeight = 370f;
    const float CaptainListWidth = 380f;
    const float CardHeight = 76f;
    const float PortraitSize = 64f;
    const float IconSize = 16f;
    const int PartyColumns = 3;

    static readonly Color CardColor = UIKit.ItemRowColor;
    static readonly Color PickedCardColor = new(0.27f, 0.24f, 0.2f, 1f);

    [Tooltip("The captain templates, soldier classes and starting values the Castle reads.")]
    [SerializeField] MatchData data;

    Canvas canvas;
    RectTransform screen;
    readonly List<Action> refreshers = new();
    Profile profile;

    enum Tab { Captains, Soldiers }

    Tab tab;
    // The captain last opened in the Great Hall, whose card takes focus when the hall is built.
    Captain selected;
    readonly List<Captain> party = new();

    public static void Open() => SceneManager.LoadScene(SceneName);

    // Whatever run there was has ended or been given up by now; the profile is loaded from its save
    // (or made afresh) unless it is still in memory from the run. A new profile isn't saved until
    // its starting captains are picked, so quitting before then offers them again.
    void Awake()
    {
        RunState.Clear();
        if (Profile.Current == null && !ProfileSave.TryLoad(data.captainTemplates)) Profile.StartNew(data.startingGold);
        profile = Profile.Current;
        if (!NeedsStartingCaptains) ProfileSave.Save(profile);
    }

    void Start()
    {
        canvas = UIKit.CreateCanvas("Castle", 0);
        BuildTown();
        BuildHud();
        if (NeedsStartingCaptains) BuildStartingCaptains();
        else BuildHome();
    }

    void LateUpdate()
    {
        UIKit.Refresh(refreshers);
        UIKit.Refresh(hudRefreshers);
        ShopFeedback();
        AnimateHall();
        if (screenFade != null && screenFade.alpha < 1f)
            screenFade.alpha = Mathf.Clamp01(screenFade.alpha + Time.unscaledDeltaTime / FadeSeconds);
    }

    // Every screen's Back sits in the same place: centred in a strip along the bottom, which the
    // column is kept above.
    const float FooterHeight = 52f;
    static readonly Vector2 BackSize = new(160f, 32f);
    const float FadeSeconds = 0.12f;
    CanvasGroup screenFade;
    UIFocusScope screenScope;

    // A full-screen page with a centred column; the gamepad's cancel button runs back, and so does
    // the Back button, unless backButton is off (a confirm, which has its own Cancel). A new screen
    // fades in on unscaled time; it takes clicks and the cancel button while it does. Rebuilding the
    // same screen (a purchase, a tab) doesn't fade.
    RectTransform NewScreen(string name, float width, Action back, bool backButton = true)
    {
        refreshers.Clear();
        titleBanner.gameObject.SetActive(true);
        bool fade = screen == null || screen.name != name;
        if (screen != null) Destroy(screen.gameObject);
        screen = UIKit.Stretch(UIKit.Panel(canvas.transform, name, DimColor));
        hud.SetAsLastSibling();
        if (toast != null) toast.transform.SetAsLastSibling();
        screenScope = UIKit.Focus(screen, back);
        screenFade = screen.gameObject.AddComponent<CanvasGroup>();
        screenFade.alpha = fade ? 0f : 1f;
        RectTransform content = UIKit.Stretch(UIKit.Group(screen, "Content"));
        content.offsetMin = new Vector2(0f, FooterHeight);
        content.offsetMax = new Vector2(0f, -HudHeight);
        if (back != null && backButton)
        {
            Button button = UIKit.Button(screen, "Back", back);
            UIKit.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0f, (FooterHeight - BackSize.y) / 2f), BackSize);
            UIKit.PadBadge(button, PadGlyph.East);
        }
        RectTransform column = UIKit.Group(content, "Column");
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


    static RectTransform Row(Transform parent, string name, float spacing)
    {
        RectTransform row = UIKit.Group(parent, name);
        UIKit.Layout<HorizontalLayoutGroup>(row, 0, spacing, fitContent: true);
        return row;
    }

    // ---------- Inventory (the Great Hall and the Barracks) ----------

    void BuildInventory()
    {
        if (selected != null && !profile.Owns(selected)) selected = null;
        if (selected == null && profile.Roster.Count > 0) selected = profile.Roster[0];
        if (hallCaptain != null && !profile.Owns(hallCaptain)) hallCaptain = null;
        RectTransform column = NewScreen("Inventory", InventoryWidth, BuildHome);
        hallColumn = tab == Tab.Captains ? column : null;
        UIKit.Label(column, tab == Tab.Captains ? "GREAT HALL" : "BARRACKS", UIKit.TitleSize, TextAnchor.MiddleCenter);

        RectTransform body = UIKit.Group(column, "Body");
        UIKit.Layout<HorizontalLayoutGroup>(body, 0, 10).childControlHeight = true;
        UIKit.Size(body, InventoryWidth, InventoryBodyHeight);
        if (tab == Tab.Captains) BuildCaptainsTab(body);
        else BuildSoldiersTab(body);
        // Coming back from the release confirm reopens the captain's window.
        if (tab == Tab.Captains && hallCaptain != null) OpenHallWindow(hallCaptain);
    }

    // Every soldier owned and who has it; picking one opens its stats, where an equipped one can be unequipped.
    void BuildSoldiersTab(Transform body)
    {
        RectTransform panel = UIKit.Box(body, "Soldiers");
        UIKit.Size(panel, height: InventoryBodyHeight, flexibleWidth: 1f);
        UIKit.Layout<VerticalLayoutGroup>(panel, UIKit.Pad, 6);
        int free = profile.Soldiers.Unequipped.Count;
        Header(panel, $"SOLDIERS", $"{profile.Soldiers.Items.Count} owned, {free} in the inventory");
        ScrollRect list = null;
        void Fill()
        {
            UIKit.Clear(list.content);
            List<SoldierItem> shown = FilteredSoldiers(barracksFilter);
            foreach (SoldierItem soldier in shown)
            {
                SoldierItem chosen = soldier;
                RectTransform row = SoldierRow(list.content, soldier, WhereIs(soldier));
                Button pick = null;
                pick = UIKit.Clickable(row, () => OpenSoldierInfo(pick, chosen));
            }
            if (shown.Count == 0) ListFilterBar.Empty(list.content, profile.Soldiers.Items.Count == 0, "You have no soldiers yet.");
        }
        ListFilterBar.Soldiers(panel, barracksFilter, Fill);
        list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        Fill();
        UIKit.Label(panel, "Equip soldiers from a captain in the Great Hall.", 11, TextAnchor.MiddleCenter, UIKit.MutedTextColor);
    }

    // What the Barracks, the Great Hall, its soldier list and the party pick show. Each resets when
    // its screen or list opens.
    readonly ListFilter barracksFilter = ListFilterBar.ForSoldiers();
    readonly ListFilter hallFilter = ListFilterBar.ForCaptains();
    readonly ListFilter hallPickerFilter = ListFilterBar.ForSoldiers();
    readonly ListFilter partyFilter = ListFilterBar.ForCaptains();

    List<SoldierItem> FilteredSoldiers(ListFilter filter) =>
        filter.Apply(SortedSoldiers(), s => ListFilterBar.ValuesOf(s, s.IsEquipped ? profile.Find(s.equippedTo) : null));

    List<SoldierItem> SortedSoldiers()
    {
        var soldiers = new List<SoldierItem>(profile.Soldiers.Items);
        soldiers.Sort((a, b) => a.IsEquipped != b.IsEquipped ? a.IsEquipped.CompareTo(b.IsEquipped)
            : b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : a.soldierClass.CompareTo(b.soldierClass));
        return soldiers;
    }

    // Class portrait and rarity pip, "Rare Knight" in its rarity's colour, and its bonus, damage type
    // and where (a line the caller picks) under it.
    RectTransform SoldierRow(Transform list, SoldierItem soldier, string where)
    {
        SoldierClassData soldierClass = data.ClassData(soldier.soldierClass);
        string name = $"{soldier.rarity} {(soldierClass != null ? soldierClass.displayName : soldier.soldierClass.ToString())}";
        string detail = CaptainPanelParts.SoldierBonus(soldier, soldierClass) + (soldierClass != null ? $"  {soldierClass.damageType}" : "");
        return UIKit.ItemRow(list, soldier.id, soldierClass != null ? soldierClass.portrait : null, name,
            UIKit.RarityColor(soldier.rarity), $"{detail}   <color={UIKit.Hex(UIKit.TextColor)}>{where}</color>", soldier.rarity);
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
        RectTransform column = NewScreen("Confirm", 460f, BuildInventory, backButton: false);
        UIKit.Label(column, $"RELEASE {captain.displayName.ToUpperInvariant()}?", 24, TextAnchor.MiddleCenter);
        UIKit.Label(column, "They leave your roster for good. Their soldiers go back to the inventory.", 14,
            TextAnchor.MiddleCenter, UIKit.MutedTextColor);
        RectTransform buttons = Row(column, "Buttons", 10);
        Button release = UIKit.Button(buttons, "Release", () =>
        {
            profile.Release(captain);
            party.Remove(captain);
            ProfileSave.Save(profile);
            selected = hallCaptain = null;
            BuildInventory();
        }, BackSize.x, BackSize.y);
        release.GetComponent<Image>().color = UIKit.BadColor;
        release.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        // Cancel takes focus first, so a stray confirm press doesn't release anyone.
        Button cancel = UIKit.Button(buttons, "Cancel", BuildInventory, BackSize.x, BackSize.y);
        UIKit.PadBadge(cancel, PadGlyph.East);
        screenScope.Default = () => cancel;
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
        // The cards' refreshers live apart from the screen's, so refilling the list drops the old ones.
        var cardRefreshers = new List<Action>();
        refreshers.Add(() => UIKit.Refresh(cardRefreshers));
        ScrollRect list = null;
        void Fill()
        {
            cardRefreshers.Clear();
            CaptainGrid(list, partyFilter, (row, captain) => PartyCard(cardRefreshers, row, captain));
        }
        ListFilterBar.Captains(panel, partyFilter, Fill);
        list = UIKit.ScrollList(panel, "List", UIKit.Gap);
        UIKit.Size(list, flexibleWidth: 1f).flexibleHeight = 1f;
        Fill();

        // Disabled until someone is picked, with the reason beside it (a blank the same width on the
        // other side keeps the button centred).
        RectTransform buttons = Row(column, "Buttons", 10);
        UIKit.Size(UIKit.Group(buttons, "Balance"), 200f);
        UIKit.Primary(UIKit.LiveButton(buttons, () => "Choose a location", BuildLocations, refreshers,
            () => party.Count > 0, width: 220f, height: 32f), refreshers);
        UIKit.Size(UIKit.LiveLabel(buttons, () => party.Count > 0 ? "" : "Pick at least one captain first.", refreshers,
            UIKit.CaptionSize, color: UIKit.BadColor), 200f);
    }

    // Picked cards get a gold ring, name and tag; the rest fade once the party is full.
    // The roster's captains that pass filter, PartyColumns cards to a row; card adds one to the row
    // it is given.
    void CaptainGrid(ScrollRect list, ListFilter filter, Action<Transform, Captain> card)
    {
        UIKit.Clear(list.content);
        List<Captain> shown = filter.Apply(profile.Roster, ListFilterBar.ValuesOf);
        for (int i = 0; i < shown.Count; i += PartyColumns)
        {
            RectTransform row = UIKit.Group(list.content, "Row");
            UIKit.Layout<HorizontalLayoutGroup>(row, 0, UIKit.Gap);
            UIKit.Size(row, flexibleWidth: 1f);
            for (int j = i; j < i + PartyColumns; j++)
            {
                // A blank cell keeps a short last row's cards at the same width as the rest.
                if (j >= shown.Count)
                {
                    UIKit.Size(UIKit.Group(row, "Blank"), 0f, flexibleWidth: 1f);
                    continue;
                }
                card(row, shown[j]);
            }
        }
        if (shown.Count == 0) ListFilterBar.Empty(list.content, profile.Roster.Count == 0, "No captains in the roster.");
    }

    // The Gate: a fresh party pick shows every captain.
    void OpenParty()
    {
        partyFilter.Reset();
        BuildParty();
    }

    void PartyCard(List<Action> cardRefreshers, Transform row, Captain captain)
    {
        RectTransform card = CaptainCard(row, captain, "", () =>
        {
            if (!party.Remove(captain) && party.Count < PartyLimit) party.Add(captain);
        }, false, out Text name, out Text tag);
        UIKit.Size(card, 0f, CardHeight, 1f);
        var image = card.GetComponent<Image>();
        var fade = card.gameObject.AddComponent<CanvasGroup>();
        Image ring = UIKit.Outline(card, UIKit.AccentColor);
        ring.rectTransform.offsetMin = new Vector2(2f, 2f);
        ring.rectTransform.offsetMax = new Vector2(-2f, -2f);
        cardRefreshers.Add(() =>
        {
            bool picked = party.Contains(captain);
            image.color = picked ? PickedCardColor : CardColor;
            ring.enabled = picked;
            tag.enabled = picked;
            // The order picked, which is the order they join the run in.
            if (picked) tag.text = $"PICK {party.IndexOf(captain) + 1}";
            name.color = picked ? UIKit.AccentColor : UIKit.TextColor;
            fade.alpha = picked || party.Count < PartyLimit ? 1f : 0.45f;
        });
    }

    const float LockIconSize = 24f;
    static Sprite lockSprite;

    // A padlock in pixel art, for locked rows.
    static Sprite LockSprite => lockSprite != null ? lockSprite : lockSprite = UIKit.PixelSprite(new[]
    {
        "..XXXX..",
        ".X....X.",
        ".X....X.",
        "XXXXXXXX",
        "XXXDDXXX",
        "XXXDDXXX",
        "XXXXXXXX",
        "XXXXXXXX",
    }, UIKit.MutedTextColor, UIKit.InsetColor);

    // Starts a run at the location with the picked party; the match makes its world map.
    void SetOut(LocationData location)
    {
        if (party.Count == 0 || !LocationSelect.TryStartRun(data, profile, location)) return;
        RunState.Current.SetParty(party);
        PackForRun(RunState.Current);
        ProfileSave.Save(profile);
        SceneManager.LoadScene(MainMenu.MatchSceneName);
    }

    // ---------- Cards ----------

    static void Header(Transform parent, string title, string hint = null, bool below = false) => UIKit.Header(parent, title, hint, below);

    RectTransform CaptainCard(Transform parent, Captain captain, string tagText, Action onClick, bool highlighted) =>
        CaptainCard(parent, captain, tagText, onClick, highlighted, out _, out _, out _);

    // Portrait on the left; name and tag, element and role, bond and soldiers on the right.
    RectTransform CaptainCard(Transform parent, Captain captain, string tagText, Action onClick, bool highlighted, out Text name, out Text tag)
        => CaptainCard(parent, captain, tagText, onClick, highlighted, out name, out tag, out _);

    RectTransform CaptainCard(Transform parent, Captain captain, string tagText, Action onClick, bool highlighted, out Text name, out Text tag,
        out Text soldiers)
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
        tag = UIKit.Label(title, tagText, UIKit.CaptionSize, TextAnchor.MiddleRight, UIKit.AccentColor);
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
        soldiers = UIKit.Label(info, SoldiersLine(captain), 11);
        return card;
    }

    // The level the captain has reached, counting XP not yet levelled up.
    static int Level(Captain captain) => Mathf.Max(captain.level, LevelMath.LevelForXp(captain.xp));

    // The captain's soldiers in slot order, each class in its rarity's colour (the colour carries the
    // rarity, so the line fits on one row of a card).
    string SoldiersLine(Captain captain)
    {
        // A captain on offer isn't owned yet; its soldiers were rolled with the offer.
        List<SoldierItem> soldiers = profile.Owns(captain) ? profile.SoldiersOf(captain)
            : startingSoldiers.TryGetValue(captain.id, out List<SoldierItem> offered) ? offered : new List<SoldierItem>();
        string muted = UIKit.Hex(UIKit.MutedTextColor);
        if (soldiers.Count == 0) return $"<color={muted}>No soldiers</color>";
        return $"<color={muted}>Soldiers</color> " +
               string.Join(", ", soldiers.ConvertAll(s =>
               {
                   SoldierClassData soldierClass = data.ClassData(s.soldierClass);
                   string name = soldierClass != null ? soldierClass.displayName : s.soldierClass.ToString();
                   return $"<color={UIKit.Hex(UIKit.RarityColor(s.rarity))}>{name}</color>";
               }));
    }
}
