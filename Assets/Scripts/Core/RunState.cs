using System.Collections.Generic;
using UnityEngine;

// State that lasts for one run (item: run): the location, its world map, global health and the
// party picked from the profile's roster. Gold, soldiers and the captains themselves belong to the
// Profile; the run reaches them through it. Lives outside any scene so it survives loading the
// next match.
public class RunState
{
    public static RunState Current { get; private set; }

    // Each captain's highest approval seen this run, for the walkout rule.
    readonly Dictionary<Captain, float> bestApprovalThisRun = new();
    // Each captain's bond rank when it was first seen this run, which caps how far it can rise.
    readonly Dictionary<Captain, int> startRanks = new();
    // Captains who have walked out this run. They have left the roster and the party for good; this
    // only remembers who, for the run's reports.
    readonly HashSet<Captain> departed = new();
    // Gifts bought this run and not yet given. They don't carry over to the next run.
    readonly Dictionary<GiftData, int> gifts = new();

    public const int MaxSoldierSlots = SoldierInventory.SlotsPerCaptain;

    public Profile Profile { get; private set; }

    // The soldiers the player owns and the captains they're equipped to, held by the profile.
    public SoldierInventory Soldiers => Profile.Soldiers;

    // The id of the location this run is at (see LocationData), or null for a match without locations.
    public string Location { get; private set; }
    public int Health { get; private set; }
    public int MaxHealth { get; private set; }
    // The profile's Gold: it carries over between runs.
    public int Gold => Profile.Gold;
    public bool IsDefeated => Health <= 0;

    public static RunState StartNewRun(Profile profile, string location, int health)
    {
        Current = new RunState { Profile = profile, Location = location, Health = health, MaxHealth = health };
        return Current;
    }

    // A saved run picked up again (see RunSave), which restores the rest.
    public static RunState Resume(Profile profile, string location, int health, int maxHealth, int matchesPlayed)
    {
        Current = new RunState { Profile = profile, Location = location, Health = health, MaxHealth = maxHealth, MatchesPlayed = matchesPlayed };
        return Current;
    }

    public IEnumerable<KeyValuePair<Captain, float>> AllBestApprovals => bestApprovalThisRun;
    public IEnumerable<Captain> Departed => departed;

    public IEnumerable<KeyValuePair<Captain, int>> AllStartRanks => startRanks;

    public void SetBestApproval(Captain captain, float approval) => bestApprovalThisRun[captain] = approval;
    public void SetStartRank(Captain captain, int rank) => startRanks[captain] = rank;

    // The rank the captain had when this run first touched its approval. Every approval change
    // asks for this before applying itself, so the first ask sees the rank the run began with.
    public int StartRankOf(Captain captain)
    {
        if (!startRanks.TryGetValue(captain, out int rank))
            startRanks[captain] = rank = ApprovalStore.RankOf(captain);
        return rank;
    }

    public void AddGold(int amount) => Profile.AddGold(amount);

    public bool TrySpendGold(int amount) => Profile.TrySpendGold(amount);

    public void TakeDamage(int amount) => Health = Mathf.Max(0, Health - amount);

    public void Heal(int amount) => Health = RunHealing.Heal(Health, MaxHealth, amount);

    public int XpOf(Captain captain) => captain != null ? captain.xp : 0;

    public void AddXp(Captain captain, int amount)
    {
        if (captain != null && amount > 0) captain.xp += amount;
    }

    // The best approval this run, starting from the captain's approval when first seen this run.
    public float BestApprovalThisRun(Captain captain)
    {
        float current = ApprovalStore.ApprovalOf(captain);
        if (!bestApprovalThisRun.TryGetValue(captain, out float best) || current > best)
            bestApprovalThisRun[captain] = best = current;
        return best;
    }

    // The captains picked from the roster for this run, up to 5. Empty until the player has
    // picked, which is how the match knows to show the run setup screen.
    public List<Captain> Party { get; } = new();
    public bool HasParty => Party.Count > 0;

    // Matches finished this run; the next match is MatchesPlayed + 1.
    public int MatchesPlayed { get; private set; }

    // The run's rival pair, by captain id, so it saves as plain data. Null when the party has none.
    public string RivalA { get; private set; }
    public string RivalB { get; private set; }

    // The other half of the captain's rival pair, while both are still in the party.
    public Captain RivalOf(Captain captain)
    {
        if (captain == null || RivalA == null) return null;
        string other = captain.id == RivalA ? RivalB : captain.id == RivalB ? RivalA : null;
        return other == null ? null : Party.Find(c => c.id == other);
    }

    // Restores a saved pair; ignored unless both are in the party.
    public void SetRivals(string a, string b)
    {
        if (Party.Exists(c => c.id == a) && Party.Exists(c => c.id == b) && a != b) { RivalA = a; RivalB = b; }
    }

    void PickRivals()
    {
        RivalA = RivalB = null;
        var traits = Party.ConvertAll(c => c.Trait);
        var pair = RivalRules.PickPair(traits, new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue)));
        if (pair == null) return;
        RivalA = Party[pair.Value.Item1].id;
        RivalB = Party[pair.Value.Item2].id;
    }

    public void SetParty(IEnumerable<Captain> captains)
    {
        Party.Clear();
        Party.AddRange(captains);
        // A new run, or a party changed so its pair is gone, gets a rival pair.
        if (RivalOf(Party.Count > 0 ? Party.Find(c => c.id == RivalA) : null) == null) PickRivals();
    }

    // Captains who leave are taken out of the party, so every party member is active.
    public int ActivePartyCount => Party.Count;

    // Soldiers offered as the reward for a won city; empty when there's nothing to choose.
    public List<SoldierItem> SoldierOffer { get; } = new();

    public void OfferSoldiers(IEnumerable<SoldierItem> soldiers)
    {
        SoldierOffer.Clear();
        SoldierOffer.AddRange(soldiers);
    }

    // Takes one of the offered soldiers into the soldier inventory, or none when soldier is null
    // (skipped). Either way the offer is over.
    public void TakeSoldier(SoldierItem soldier)
    {
        if (soldier != null && SoldierOffer.Contains(soldier)) Soldiers.Add(soldier);
        SoldierOffer.Clear();
    }

    // A captain an event recruited while the roster was full, waiting for the player to release
    // either them or a captain already in the roster. Not saved: quitting first loses the newcomer.
    public Captain PendingRecruit { get; private set; }

    // A captain joins from an event: into the roster, and the party while it has room. With a full
    // roster they wait as the pending recruit until ReleaseFor picks who goes. Returns whether they
    // joined now.
    public bool RecruitFromEvent(Captain captain, int maxPartySize)
    {
        if (captain == null) return false;
        if (RosterRules.NeedsRelease(Profile.Roster.Count, Profile.MaxRoster))
        {
            PendingRecruit = captain;
            return false;
        }
        return JoinFromEvent(captain, null, maxPartySize);
    }

    // Settles the pending recruit: released leaves the roster for good (the newcomer, or a captain
    // already there, who also leaves the party), and the newcomer joins when there's room.
    public bool ReleaseFor(Captain released, int maxPartySize)
    {
        Captain newcomer = PendingRecruit;
        PendingRecruit = null;
        return newcomer != null && JoinFromEvent(newcomer, released, maxPartySize);
    }

    bool JoinFromEvent(Captain newcomer, Captain released, int maxPartySize)
    {
        bool joined = Profile.Join(newcomer, released);
        if (released != null && !Profile.Owns(released)) PartyRules.Leave(Party, null, released);
        if (joined && ActivePartyCount < maxPartySize) Party.Add(newcomer);
        return joined;
    }

    // The run's world map, made when the run begins.
    public WorldMap Map { get; private set; }

    public void SetMap(WorldMap map) => Map = map;

    // A won match: the target city falls and its intel reveals the roads ahead.
    public void CompleteMatch(int intelDepth)
    {
        MatchesPlayed++;
        Map?.ClearTarget(intelDepth);
    }

    // A rest, shop or event left behind: it counts as visited and its intel reveals the roads
    // ahead, but it isn't a match.
    public void CompleteStop(int intelDepth) => Map?.ClearTarget(intelDepth);

    // The captain's soldiers in slot order.
    public List<SoldierItem> SoldiersOf(Captain captain) => Profile.SoldiersOf(captain);

    public IEnumerable<KeyValuePair<GiftData, int>> AllGifts => gifts;

    public int GiftCount(GiftData gift) => gift != null && gifts.TryGetValue(gift, out int count) ? count : 0;

    public void AddGift(GiftData gift, int count = 1)
    {
        if (gift != null && count > 0) gifts[gift] = GiftCount(gift) + count;
    }

    public bool TryRemoveGift(GiftData gift)
    {
        int count = GiftCount(gift);
        if (count <= 0) return false;
        if (count == 1) gifts.Remove(gift);
        else gifts[gift] = count - 1;
        return true;
    }

    public bool HasDeparted(Captain captain) => departed.Contains(captain);

    // A captain who walks out leaves the party and the roster for good, and their soldiers go back
    // to the inventory.
    public void Depart(Captain captain)
    {
        if (captain == null) return;
        departed.Add(captain);
        PartyRules.Leave(Party, null, captain);
        Profile.Release(captain);
    }

    // Forgets the run in memory, so the match scene loads the saved one (or starts afresh) next.
    public static void Clear() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;
}
