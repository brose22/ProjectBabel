using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// The run around the scene's match: starting, resuming and ending it, the setup screen's party, the
// world map, stops, and what the run gains or loses when a match ends. MatchManager owns one and
// plays the match itself.
public class RunController
{
    readonly MatchManager match;

    public RunController(MatchManager match) => this.match = match;

    MatchData data => match.Data;
    MatchState State => match.State;
    MapNode City => match.City;

    // Picks up the saved profile and run if there are any, otherwise starts fresh. A run the Castle
    // just set out on has its party but no map yet. Called once as the scene loads.
    public void Load()
    {
        if (RunState.Current == null && !RunSave.TryResume(LoadProfile(), data.shopGifts))
            StartRun();
        RunState run = RunState.Current;
        Location = LocationSelect.Of(data, run);
        if (run.HasParty && run.Map == null) run.SetMap(NewWorldMap());
    }

    // Where this run is played, or null when the match data has no locations.
    public LocationData Location { get; private set; }

    // What a lost run cost the profile: Gold, and the deployed captains with their soldiers.
    public int GoldLost { get; private set; }
    public List<Captain> CaptainsLost { get; private set; } = new();
    public int SoldiersLost { get; private set; }
    // The location a won run opened, or null.
    public LocationData LocationUnlocked { get; private set; }

    // Leaves the match for the main menu. The run keeps its last save, so Resume run restarts this stop.
    public void ExitMatch()
    {
        match.Unpause();
        MainMenu.Open();
    }

    // Ends the run: back to the setup screen with fresh health. The profile, with the roster,
    // soldiers and Gold, carries on, unless freshProfile asks for a new one (for AI runs that
    // each start from scratch).
    public void RestartRun(bool freshProfile = false)
    {
        RunSave.Delete();
        if (freshProfile)
        {
            ProfileSave.Delete();
            Profile.Clear();
        }
        else if (RunState.Current != null) ProfileSave.Save(RunState.Current.Profile);
        StartRun();
        SceneManager.LoadScene(match.gameObject.scene.name);
    }

    // Ends the run, by abandoning it or once it is won or lost, and goes home to the Castle. The
    // profile, with the roster, soldiers and Gold, is kept.
    public void ReturnToCastle()
    {
        match.Unpause();
        RunSave.Delete();
        if (RunState.Current != null) ProfileSave.Save(RunState.Current.Profile);
        CastleScreen.Open();
    }

    // The profile in memory, else the saved one, else a new one with the starting Gold.
    Profile LoadProfile()
    {
        if (Profile.Current == null && !ProfileSave.TryLoad(data.captainTemplates))
            Profile.StartNew(data.startingGold);
        return Profile.Current;
    }

    // A new run at the furthest open location, with fresh health. The Castle starts runs at the
    // location picked instead (LocationSelect.TryStartRun).
    void StartRun()
    {
        Profile profile = LoadProfile();
        RunState.StartNewRun(profile, LocationSelect.Default(data, profile)?.id, data.startingHealth);
    }

    // Back to the main menu from the setup, map or results screen. The run is already saved
    // whenever it can be resumed, so nothing is lost.
    public void ReturnToMenu()
    {
        if (!match.IsBetweenMatches && !match.IsOver) return;
        MainMenu.Open();
    }

    // AI runs: moves a run still at its setup to the location at this index, opening it (and those
    // before it) in the profile.
    public void SetupAtLocation(int index)
    {
        if (State != MatchState.Setup || index < 0 || index >= data.locations.Count || data.locations[index] == null) return;
        Profile profile = RunState.Current.Profile;
        profile.UnlockLocations(index + 1);
        RunState.StartNewRun(profile, data.locations[index].id, data.startingHealth);
        Location = data.locations[index];
    }

    // At least this many captains are offered, for an AI run that fields a bigger party than usual.
    public int SetupOfferMinimum { get; set; }

    // The captains the setup screen offers (AI runs; players pick the party at the Castle): the
    // profile's roster. A new profile first gets all of the starting captains, each with their
    // soldiers, for the AI to pick its party from; a bigger AI party tops the roster up with rolled
    // captains, each with a few soldiers.
    public IReadOnlyList<Captain> SetupOffer
    {
        get
        {
            Profile profile = RunState.Current.Profile;
            if (profile.Roster.Count == 0)
                TakeStartingCaptains(profile, RollStartingCaptains(data, Random.Range(int.MinValue, int.MaxValue)),
                    Random.Range(int.MinValue, int.MaxValue));
            int size = Mathf.Min(Profile.MaxRoster, Mathf.Max(SetupOfferMinimum, data.partySize));
            if (profile.Roster.Count < size)
                foreach (Captain captain in RollCaptains(size - profile.Roster.Count, Random.Range(int.MinValue, int.MaxValue)))
                    if (profile.TryAdd(captain)) GiveStartingSoldiers(new[] { captain });
            ProfileSave.Save(profile);
            return profile.Roster;
        }
    }

    // Rolls count captains from the templates, every template once before any repeats. Names
    // already in the roster or on offer are rerolled where the template has others, so names tell
    // captains apart.
    public List<Captain> RollCaptains(int count, int seed)
    {
        RunState run = RunState.Current;
        return RollCaptains(data, run.Profile.Roster.Concat(run.Party).Concat(run.Departed), count, seed);
    }

    // The same outside a match (the Castle), with the names of the given captains taken.
    public static List<Captain> RollCaptains(MatchData data, IEnumerable<Captain> named, int count, int seed)
    {
        List<CaptainData> templates = data.captainTemplates.FindAll(t => t != null);
        return Roll(templates, CaptainOffer.Draw(templates.Count, count, seed), named);
    }

    // The starting captains offered on the first play: one of each starting role, with no two
    // sharing a name.
    public static List<Captain> RollStartingCaptains(MatchData data, int seed)
    {
        List<CaptainData> templates = data.captainTemplates.FindAll(t => t != null);
        return Roll(templates, StartingCaptains.Draw(templates.ConvertAll(t => t.role), seed), Enumerable.Empty<Captain>());
    }

    // Adds the picked starting captains to the roster, each with its soldiers equipped. The
    // caller saves the profile.
    public static void TakeStartingCaptains(Profile profile, IEnumerable<Captain> picked, int seed)
    {
        var joined = picked.Where(profile.TryAdd).ToList();
        StartingCaptains.GiveSoldiers(profile.Soldiers, joined.Select(c => c.id), seed);
    }

    static List<Captain> Roll(List<CaptainData> templates, IEnumerable<(int template, int seed)> draws, IEnumerable<Captain> named)
    {
        var taken = new HashSet<string>(named.Select(c => c.displayName));
        var rolled = new List<Captain>();
        foreach ((int template, int rollSeed) in draws)
        {
            Captain captain = templates[template].Roll(rollSeed);
            for (int retry = 1; retry <= MaxNameRerolls && taken.Contains(captain.displayName); retry++)
                captain = templates[template].Roll(rollSeed + retry);
            taken.Add(captain.displayName);
            rolled.Add(captain);
        }
        return rolled;
    }

    const int MaxNameRerolls = 20;

    // How many gates each layout has, by layout index; a missing layout counts as one.
    List<int> LayoutGateCounts()
    {
        var counts = new List<int>();
        foreach (CityLayout layout in data.layouts)
            counts.Add(layout != null ? Mathf.Max(1, layout.AllRoutes().Count) : 1);
        return counts;
    }

    // A fresh world map for the run's location.
    WorldMap NewWorldMap()
    {
        int seed = Random.Range(int.MinValue, int.MaxValue);
        return Location != null
            ? WorldMap.Generate(seed, Location.Spec, data.layouts.Count, data.intelDepth, LayoutGateCounts())
            : WorldMap.Generate(seed, data.matchesPerRun, data.layouts.Count, data.minCitiesPerLayer, data.maxCitiesPerLayer,
                data.intelDepth, data.nodeOdds, LayoutGateCounts(), data.doubleGateFirstLayer, data.tripleGateFirstLayer);
    }

    // Starts the run with the chosen roster captains and a fresh world map for its location;
    // called from the setup screen.
    public void BeginRun(IEnumerable<Captain> party)
    {
        if (State != MatchState.Setup) return;
        RunState.Current.SetParty(party.Where(RunState.Current.Profile.Owns));
        RunState.Current.SetMap(NewWorldMap());
        match.EnterMap();
        RunSave.Save(RunState.Current);
    }

    // Starts a run that is one match on the given layout, with no world map to cross. Layer sets
    // the difficulty; called by the AI run, which ends the run when the match does. Captains not
    // yet in the roster join it, with a few soldiers each, while there's room.
    public void BeginSingleMatch(IEnumerable<Captain> party, int layout, int layer)
    {
        if (State != MatchState.Setup) return;
        Profile profile = RunState.Current.Profile;
        foreach (Captain captain in party)
            if (profile.TryAdd(captain)) GiveStartingSoldiers(new[] { captain });
        RunState.Current.SetParty(party.Where(profile.Owns));
        RunState.Current.SetMap(WorldMap.SingleMatch(Random.Range(int.MinValue, int.MaxValue), data.matchesPerRun, layout, layer));
        match.ResetCaptainCycle();
        RunSave.Save(RunState.Current);
        SceneManager.LoadScene(match.gameObject.scene.name);
    }

    // Takes an offered soldier into the soldier inventory (or none, for null) and saves the choice.
    public void TakeSoldier(SoldierItem soldier)
    {
        RunState.Current.TakeSoldier(soldier);
        RunSave.Save(RunState.Current);
    }

    // A won city offers a few newly rolled soldiers to pick one from. Elite cities roll rarity more
    // than once and keep the best, for better odds.
    public void OfferSoldiers()
    {
        int rarityRolls = match.IsEliteMatch ? Mathf.Max(1, data.eliteRewardRarityRolls) : 1;
        RunState.Current.OfferSoldiers(SoldierRules.RollOffer(Random.Range(int.MinValue, int.MaxValue),
            Mathf.Max(0, data.rewardSoldierChoices), rarityRolls));
    }

    // Plays the picked city: reloads the scene, which lays out that city's map and waves.
    public void ChooseCity(int id)
    {
        if (State != MatchState.Map || !RunState.Current.Map.TryChoose(id)) return;
        SceneManager.LoadScene(match.gameObject.scene.name);
    }

    // ---------- Stops ----------

    public int RestAmount => RunHealing.RestAmount(RunState.Current.MaxHealth, data.restHealFraction);

    // Rests restore part of the run's health, then the player moves on.
    public void Rest()
    {
        if (State != MatchState.Stop || City.kind != NodeKind.Rest) return;
        RunState.Current.Heal(RestAmount);
        LeaveStop();
    }

    // Whether the shop can be shopped at now: from the world map.
    public bool ShopOpen => State == MatchState.Map;

    // The gifts on the shelves: shopSize of the full list, picked by the shop node's seed, or by
    // the run's first node when shopping from the map.
    public List<GiftData> ShopStock
    {
        get
        {
            var stock = new List<GiftData>();
            if (!ShopOpen) return stock;
            int seed = State == MatchState.Stop ? City.seed : RunState.Current.Map.nodes[0].seed;
            foreach (int index in global::ShopStock.Pick(data.shopGifts.Count, data.shopSize, seed))
                if (data.shopGifts[index] != null) stock.Add(data.shopGifts[index]);
            return stock;
        }
    }

    // A shop item the player can pay for right now.
    public bool CanBuyGift(GiftData gift) =>
        ShopOpen && gift != null && RunState.Current.Gold >= gift.cost && ShopStock.Contains(gift);

    // Buys a gift at the shop for Gold; it goes into the run's inventory. Saved straight away so
    // quitting at the shop keeps it.
    public void BuyGift(GiftData gift)
    {
        if (!CanBuyGift(gift) || !RunState.Current.TrySpendGold(gift.cost)) return;
        RunState.Current.AddGift(gift);
        RunSave.Save(RunState.Current);
    }

    // Whether a captain can be given an item from the inventory: they're in the party and the
    // player has one.
    public bool CanGiveGift(Captain captain, GiftData gift) =>
        captain != null && gift != null && RunState.Current.Party.Contains(captain) && RunState.Current.GiftCount(gift) > 0;

    // Gives a captain one item, changing their approval by how they feel about it. Returns the
    // reaction, or null when the gift couldn't be given.
    public GiftReaction? GiveGift(Captain captain, GiftData gift)
    {
        if (!CanGiveGift(captain, gift) || !RunState.Current.TryRemoveGift(gift)) return null;
        GiftReaction reaction = captain.ReactionTo(gift);
        ApprovalStore.Change(captain, gift.ApprovalFor(reaction));
        RunSave.Save(RunState.Current);
        return reaction;
    }

    // The event at this event node, picked from the pool by the node's seed; null with no events.
    public EventData StopEvent =>
        State == MatchState.Stop && City.kind == NodeKind.Event && data.events.Count > 0 ? data.events[City.seed % data.events.Count] : null;

    public bool CanTakeEventChoice(EventChoice choice) =>
        StopEvent != null && choice != null && PendingRecruit == null && choice.CanTake(RunState.Current.Gold, RunState.Current.Health);

    // A captain an event recruited into a full roster, waiting on ReleaseForRecruit.
    public Captain PendingRecruit => RunState.Current?.PendingRecruit;

    // Applies an event choice to the run and moves on.
    public void TakeEventChoice(EventChoice choice)
    {
        if (!CanTakeEventChoice(choice)) return;
        RunState run = RunState.Current;
        if (choice.gold >= 0) run.AddGold(choice.gold);
        else run.TrySpendGold(-choice.gold);
        if (choice.health >= 0) run.Heal(choice.health);
        else run.TakeDamage(-choice.health);
        foreach (Captain captain in run.Party)
        {
            run.AddXp(captain, choice.xp);
            if (choice.approval != 0f) ApprovalStore.Change(captain, choice.approval);
        }
        // A newcomer for a full roster stays on the stop until the player picks who to release.
        if (choice.recruit && !RecruitFromEvent()) return;
        LeaveStop();
    }

    // Rolls a captain for an event to recruit. Returns false while they wait on a full roster.
    bool RecruitFromEvent()
    {
        RunState run = RunState.Current;
        Captain captain = RollCaptains(1, Random.Range(int.MinValue, int.MaxValue)).FirstOrDefault();
        if (run.RecruitFromEvent(captain, data.maxPartySize)) GiveStartingSoldiers(new[] { captain });
        return run.PendingRecruit == null;
    }

    // Settles the pending recruit by releasing either them or a captain from the roster, whose
    // soldiers go back to the inventory, then moves on from the event.
    public void ReleaseForRecruit(Captain released)
    {
        Captain newcomer = PendingRecruit;
        if (newcomer == null || released == null) return;
        if (RunState.Current.ReleaseFor(released, data.maxPartySize)) GiveStartingSoldiers(new[] { newcomer });
        LeaveStop();
    }

    // Moves on from a rest, shop or event: it counts as visited and the map opens again.
    public void LeaveStop()
    {
        if (State != MatchState.Stop) return;
        RunState.Current.CompleteStop(data.intelDepth);
        RunSave.Save(RunState.Current);
        SceneManager.LoadScene(match.gameObject.scene.name);
    }

    // After a win, back to the world map. Health, Gold, XP, the party and any walkouts carry over
    // because they live in RunState.
    public void ReturnToMap()
    {
        if (State != MatchState.Won) return;
        SceneManager.LoadScene(match.gameObject.scene.name);
    }

    // A captain joining the roster outside the starting pick (a recruit, an AI run's extra
    // captains) with no soldiers brings a few random ones, equipped in its first slots.
    void GiveStartingSoldiers(IEnumerable<Captain> captains) => GiveStartingSoldiers(data, RunState.Current.Soldiers, captains);

    public static void GiveStartingSoldiers(MatchData data, SoldierInventory inventory, IEnumerable<Captain> captains)
    {
        foreach (Captain captain in captains)
        {
            if (captain == null || inventory.EquippedTo(captain.id).Count > 0) continue;
            for (int i = 0; i < data.startingSoldiersPerCaptain; i++)
            {
                int slot = inventory.FirstEmptySlot(captain.id, MatchManager.SoldierCountFor(data, captain));
                if (slot < 0) break;
                SoldierItem soldier = SoldierRules.Roll(Random.Range(int.MinValue, int.MaxValue));
                inventory.Add(soldier);
                inventory.Equip(soldier.id, captain.id, slot);
            }
        }
    }

    // Cheats: fill the run's Gold or health.
    public void CheatAddGold(int amount) => RunState.Current.AddGold(amount);

    public void CheatHealFully() => RunState.Current.Heal(RunState.Current.MaxHealth);

    // Cheat: reveals the whole world map and redraws it.
    public bool CanCheatRevealMap => RunState.Current.Map != null && RunState.Current.Map.nodes.Exists(n => !n.revealed);

    public void CheatRevealMap()
    {
        if (!CanCheatRevealMap) return;
        foreach (MapNode node in RunState.Current.Map.nodes) node.revealed = true;
        if (State != MatchState.Map) return;
        RunSave.Save(RunState.Current);
        SceneManager.LoadScene(match.gameObject.scene.name);
    }

    // A won run opens the next location; the party keeps its levels and bonds.
    public void RecordRunWon()
    {
        Profile profile = RunState.Current.Profile;
        int before = profile.LocationsUnlocked;
        LocationSelect.RecordWin(data, profile, RunState.Current.Location);
        if (profile.LocationsUnlocked > before && profile.LocationsUnlocked <= data.locations.Count)
            LocationUnlocked = data.locations[profile.LocationsUnlocked - 1];
    }

    // A lost run costs a share of the Gold and every captain deployed this match, with their
    // soldiers. Undeployed party captains go home safely.
    public void LoseRun()
    {
        RunState run = RunState.Current;
        CaptainsLost = run.Profile.LoseRun(match.Deployed, out int goldLost, out int soldiersLost);
        GoldLost = goldLost;
        SoldiersLost = soldiersLost;
    }
}
