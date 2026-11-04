using System.Collections.Generic;

// New kinds go at the end: AiAbilityRules mirrors this order.
public enum RequestKind { MoreDraw, Relocate, UseSpecial, Reinforce, LowerDraw, Retreat }

// How a captain's request ends. Expired is a request still open when its wave ended: it is dismissed
// without a verdict, so it neither pleases nor angers the captain.
public enum RequestOutcome { Refused, Granted, Expired }

public static class RequestRules
{
    // Extra approval a captain gives on top of the refunded penalty when you were right to refuse.
    public const float PaybackBonus = 1f;
    // The approval message a payback shows, and the AI report counts.
    public const string PaybackReason = "admits you were right";

    // What a captain gives back after an explicit No to a bad request: the whole penalty and the bonus when the
    // wave ended with no leaks and no fallen towers, nothing otherwise.
    public static float Payback(float refusalPenalty, bool cleanWave) => cleanWave ? refusalPenalty + PaybackBonus : 0f;

    // The approval change for an outcome, given the reward for a full yes and the refusal penalty.
    public static float ApprovalChange(RequestOutcome outcome, float reward, float refusalPenalty) => outcome switch
    {
        RequestOutcome.Refused => -refusalPenalty,
        RequestOutcome.Granted => reward,
        _ => 0f,
    };
}

// What the match looks like when a request is up; everything Evaluate needs, so it runs without Unity.
public struct RequestSnapshot
{
    public RequestKind Kind;
    // Approval a Yes gives (what the player is paid for granting it).
    public float ApprovalYes;
    // Shared run Gold left.
    public int Gold;
    // Captains already drawing at High, not counting the one asking.
    public int DrawingCaptains;
    // Enemy spawns until the next elite is out this wave, or -1 when none is still to come.
    public int SpawnsToElite;
    // An elite, the boss or a leaked enemy is what made the captain ask.
    public bool ThreatPresent;
    // Road tiles the captain reaches now that the new spot would not, and the name of that area.
    public int UncoveredRoadTiles;
    public string Area;
    // The asker's own reserve, for LowerDraw.
    public int ReserveGold;
    // The asker's rival in the party and what a Yes costs them in approval (0 when there is none).
    public string RivalName;
    public float RivalCost;
}

public readonly struct RequestEffect
{
    public readonly string Text;
    // How much it matters, on the same scale as approval points.
    public readonly float Weight;
    public RequestEffect(string text, float weight) { Text = text; Weight = weight; }
}

public class RequestEvaluation
{
    public float ApprovalYes;
    public readonly List<RequestEffect> Pros = new();
    public readonly List<RequestEffect> Cons = new();

    public float ProWeight { get { float w = ApprovalYes; foreach (RequestEffect e in Pros) w += e.Weight; return w; } }
    public float ConWeight { get { float w = 0f; foreach (RequestEffect e in Cons) w += e.Weight; return w; } }
    // The cons clearly outweigh the pros: a Yes here is a mistake.
    public bool IsBad => ConWeight > ProWeight;
}

public static class RequestEvaluator
{
    // Shared Gold per drawing captain a High draw needs on hand before it stops hurting.
    public const float GoldComfortPerDrawer = 150f;
    public const float MaxConWeight = 4f;
    // Weight of each road tile a move leaves uncovered.
    public const float UncoveredTileWeight = 0.25f;
    // Reserve Gold that weighs one approval point when a captain pockets it.
    public const float PocketGoldPerPoint = 20f;
    // Weight of a lock-in (the answer can't be undone until the wave ends).
    public const float LockWeight = 0.5f;
    // Firing at nothing worth it while an elite or the boss is still to come.
    public const float WeakTargetWeight = 1f;

    public static RequestEvaluation Evaluate(RequestSnapshot s)
    {
        var e = new RequestEvaluation { ApprovalYes = s.ApprovalYes };
        switch (s.Kind)
        {
            case RequestKind.MoreDraw:
            {
                int drawers = s.DrawingCaptains + 1;
                float pressure = 1f - s.Gold / (GoldComfortPerDrawer * drawers);
                e.Pros.Add(new RequestEffect("Soldiers can be replaced", 1f));
                e.Cons.Add(new RequestEffect($"Draws Gold twice as fast: {s.Gold} Gold left, {drawers} captain{(drawers == 1 ? "" : "s")} drawing",
                    Clamp(MaxConWeight * pressure, 0f, MaxConWeight)));
                e.Cons.Add(new RequestEffect("Draw locked until the wave ends", LockWeight));
                break;
            }
            case RequestKind.UseSpecial:
                e.Pros.Add(s.ThreatPresent ? new RequestEffect("Hits the threat now", 1.5f) : new RequestEffect("Fires early", 0.5f));
                e.Cons.Add(s.SpawnsToElite >= 0
                    ? new RequestEffect($"Elite in {s.SpawnsToElite} spawn{(s.SpawnsToElite == 1 ? "" : "s")}: special on cooldown when it arrives",
                        Clamp(3.5f - 0.25f * s.SpawnsToElite, 1f, 3.5f))
                    : new RequestEffect("Special on cooldown for the rest of the wave", 0.5f));
                if (!s.ThreatPresent && s.SpawnsToElite >= 0)
                    e.Cons.Add(new RequestEffect("Only weak enemies in reach", WeakTargetWeight));
                break;
            case RequestKind.Relocate:
            case RequestKind.Reinforce:
            case RequestKind.Retreat:
                e.Pros.Add(s.Kind == RequestKind.Reinforce ? new RequestEffect("Covers the fallen captain", 1f)
                    : s.Kind == RequestKind.Retreat ? new RequestEffect("Out of harm's way", 1f) : new RequestEffect("Closer to the gate", 0.5f));
                if (s.UncoveredRoadTiles > 0)
                    e.Cons.Add(new RequestEffect($"Leaves {s.UncoveredRoadTiles} road tile{(s.UncoveredRoadTiles == 1 ? "" : "s")} uncovered{(string.IsNullOrEmpty(s.Area) ? "" : " near " + s.Area)}",
                        Clamp(UncoveredTileWeight * s.UncoveredRoadTiles, 0f, MaxConWeight)));
                e.Cons.Add(new RequestEffect("Can't be moved again until the wave ends", LockWeight));
                break;
            case RequestKind.LowerDraw:
            {
                int pocket = s.ReserveGold / 2;
                e.Cons.Add(new RequestEffect($"Captain keeps {pocket} Gold from the reserve; it leaves the match",
                    Clamp(pocket / PocketGoldPerPoint, 0f, MaxConWeight)));
                e.Cons.Add(new RequestEffect("Draw locked at Low until the wave ends", LockWeight));
                break;
            }
        }
        if (s.RivalCost > 0f) e.Cons.Add(new RequestEffect($"{s.RivalName}: -{s.RivalCost:0.#} approval", s.RivalCost));
        return e;
    }

    static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
}
