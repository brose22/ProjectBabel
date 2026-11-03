using System.Collections.Generic;
using UnityEngine;

// State that lasts for a whole run (about 30 matches): global health, the shared Gold pool and
// the run's captains. Each Captain carries its own XP and bond. Lives outside any scene so it
// survives loading the next match.
public class RunState
{
    public static RunState Current { get; private set; }

    // Each captain's highest approval seen this run, for the walkout rule.
    readonly Dictionary<Captain, float> bestApprovalThisRun = new();
    // Each captain's bond rank when it was first seen this run, which caps how far it can rise.
    readonly Dictionary<Captain, int> startRanks = new();
    // Captains who have walked out; they sit out the rest of this run only (GDD).
    readonly HashSet<Captain> departed = new();
    // Gifts bought this run and not yet given. They don't carry over to the next run.
    readonly Dictionary<GiftData, int> gifts = new();

    public const int MaxSoldierSlots = SoldierInventory.SlotsPerCaptain;

    // The soldiers the player owns and the captains they're equipped to. Lives in the run until
    // the profile holds it (#29).
    public SoldierInventory Soldiers { get; private set; } = new();

    public int Health { get; private set; }
    public int MaxHealth { get; private set; }
    public int Gold { get; private set; }
    public bool IsDefeated => Health <= 0;

    public static RunState StartNewRun(int health, int gold)
    {
        Current = new RunState { Health = health, MaxHealth = health, Gold = gold };
        return Current;
    }

    // A saved run picked up again (see RunSave), which restores the rest.
    public static RunState Resume(int health, int maxHealth, int gold, int matchesPlayed)
    {
        Current = new RunState { Health = health, MaxHealth = maxHealth, Gold = gold, MatchesPlayed = matchesPlayed };
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

    public void AddGold(int amount) => Gold += Mathf.Max(0, amount);

    public bool TrySpendGold(int amount)
    {
        if (amount > Gold) return false;
        Gold -= amount;
        return true;
    }

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

    // The captains picked for this run (GDD: a run brings a subset of the roster). Empty until the
    // player has picked, which is how the match knows to show the run setup screen.
    public List<Captain> Party { get; } = new();
    public bool HasParty => Party.Count > 0;

    // The captains rolled for this run's setup screen to pick the party from. Empty until rolled.
    // Not saved: a run only saves once its party is picked.
    public List<Captain> SetupOffer { get; } = new();
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

    // Takes one of the offered captains into the party, or none when captain is null.
    public void Recruit(Captain captain)
    {
        if (captain != null && RecruitOffer.Contains(captain) && !Party.Contains(captain)) Party.Add(captain);
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

    public void SetSoldiers(SoldierInventory inventory) => Soldiers = inventory ?? new SoldierInventory();

    // The captain's soldiers in slot order.
    public List<SoldierItem> SoldiersOf(Captain captain) =>
        captain != null ? Soldiers.EquippedTo(captain.id) : new List<SoldierItem>();

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

    // A captain who walks out leaves their soldiers behind in the inventory.
    public void Depart(Captain captain)
    {
        departed.Add(captain);
        if (captain != null) Soldiers.UnequipAll(captain.id);
    }

    // Forgets the run in memory, so the match scene loads the saved one (or starts afresh) next.
    public static void Clear() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;
}
