using System.Collections.Generic;
using UnityEngine;

// State that lasts for a whole run (about 30 matches): global health, the shared Gold pool and
// each captain's XP (levels reset each run, GDD). Lives outside any scene so it survives
// loading the next match.
public class RunState
{
    public static RunState Current { get; private set; }

    readonly Dictionary<CaptainData, int> captainXp = new();
    // Each captain's highest approval seen this run, for the walkout rule.
    readonly Dictionary<CaptainData, float> bestApprovalThisRun = new();
    // Captains who have walked out; they sit out the rest of this run only (GDD).
    readonly HashSet<CaptainData> departed = new();
    // The soldier class the player picked for each captain's slots, from the party's details
    // window. A slot with no pick uses the match's default mix.
    readonly Dictionary<CaptainData, SoldierClassData[]> loadouts = new();

    // Gifts bought this run and not yet given. They don't carry over to the next run.
    readonly Dictionary<GiftData, int> gifts = new();

    public const int MaxSoldierSlots = 4;

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

    public IEnumerable<KeyValuePair<CaptainData, int>> AllXp => captainXp;
    public IEnumerable<KeyValuePair<CaptainData, float>> AllBestApprovals => bestApprovalThisRun;
    public IEnumerable<CaptainData> Departed => departed;

    public void SetBestApproval(CaptainData captain, float approval) => bestApprovalThisRun[captain] = approval;

    public void AddGold(int amount) => Gold += Mathf.Max(0, amount);

    public bool TrySpendGold(int amount)
    {
        if (amount > Gold) return false;
        Gold -= amount;
        return true;
    }

    public void TakeDamage(int amount) => Health = Mathf.Max(0, Health - amount);

    public void Heal(int amount) => Health = RunHealing.Heal(Health, MaxHealth, amount);

    public int XpOf(CaptainData captain) => captainXp.TryGetValue(captain, out int xp) ? xp : 0;

    public void AddXp(CaptainData captain, int amount)
    {
        if (amount > 0) captainXp[captain] = XpOf(captain) + amount;
    }

    // The best approval this run, starting from the captain's approval when first seen this run.
    public float BestApprovalThisRun(CaptainData captain)
    {
        float current = ApprovalStore.ApprovalOf(captain);
        if (!bestApprovalThisRun.TryGetValue(captain, out float best) || current > best)
            bestApprovalThisRun[captain] = best = current;
        return best;
    }

    // The captains picked for this run (GDD: a run brings a subset of the roster). Empty until the
    // player has picked, which is how the match knows to show the run setup screen.
    public List<CaptainData> Party { get; } = new();
    public bool HasParty => Party.Count > 0;
    // Matches finished this run; the next match is MatchesPlayed + 1.
    public int MatchesPlayed { get; private set; }

    public void SetParty(IEnumerable<CaptainData> captains)
    {
        Party.Clear();
        Party.AddRange(captains);
    }

    // Party members who haven't walked out.
    public int ActivePartyCount => Party.FindAll(c => !HasDeparted(c)).Count;

    // Captains offered to join after a won city; empty when there's nothing to choose.
    public List<CaptainData> RecruitOffer { get; } = new();

    public void OfferRecruits(IEnumerable<CaptainData> captains)
    {
        RecruitOffer.Clear();
        RecruitOffer.AddRange(captains);
    }

    // Takes one of the offered captains into the party, or none when captain is null.
    public void Recruit(CaptainData captain)
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

    public IEnumerable<KeyValuePair<CaptainData, SoldierClassData[]>> AllLoadouts => loadouts;

    // The class picked for a captain's slot, or null when the slot keeps the default.
    public SoldierClassData LoadoutClass(CaptainData captain, int slot) =>
        slot >= 0 && slot < MaxSoldierSlots && loadouts.TryGetValue(captain, out SoldierClassData[] classes) ? classes[slot] : null;

    public void SetLoadoutClass(CaptainData captain, int slot, SoldierClassData soldierClass)
    {
        if (captain == null || slot < 0 || slot >= MaxSoldierSlots) return;
        if (!loadouts.TryGetValue(captain, out SoldierClassData[] classes))
            loadouts[captain] = classes = new SoldierClassData[MaxSoldierSlots];
        classes[slot] = soldierClass;
    }

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

    public bool HasDeparted(CaptainData captain) => departed.Contains(captain);

    public void Depart(CaptainData captain) => departed.Add(captain);

    // Forgets the run in memory, so the match scene loads the saved one (or starts afresh) next.
    public static void Clear() => Current = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;
}
