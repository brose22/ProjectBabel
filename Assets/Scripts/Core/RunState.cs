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
    // Captains who have walked out this run. They have left the roster for good; this keeps them
    // in the party list so the run's screens can still say they left.
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

    public void SetParty(IEnumerable<Captain> captains)
    {
        Party.Clear();
        Party.AddRange(captains);
    }

    // Party members who haven't walked out.
    public int ActivePartyCount => Party.FindAll(c => !HasDeparted(c)).Count;

    // Captains offered to join after a won city; empty when there's nothing to choose.
    public List<Captain> RecruitOffer { get; } = new();

    public void OfferRecruits(IEnumerable<Captain> captains)
    {
        RecruitOffer.Clear();
        RecruitOffer.AddRange(captains);
    }

    // Takes one of the offered captains into the roster and the party, or none when captain is
    // null. Nobody joins while the roster is full.
    public void Recruit(Captain captain)
    {
        if (captain != null && RecruitOffer.Contains(captain) && !Party.Contains(captain) && Profile.TryAdd(captain)) Party.Add(captain);
        RecruitOffer.Clear();
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

    // A captain who walks out leaves the roster for good, and their soldiers go back to the inventory.
    public void Depart(Captain captain)
    {
        if (captain == null) return;
        departed.Add(captain);
        Profile.Release(captain);
    }

    // Forgets the run in memory, so the match scene loads the saved one (or starts afresh) next.
    public static void Clear() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;
}
