using System;

// A captain's personality, rolled once with the rest of the captain. It decides which bad requests
// they make. Steady is first so it is the default.
public enum CaptainTrait { Steady, GlorySeeker, Greedy, Cautious }

public static class CaptainTraits
{
    public const int Count = 4;
    // Share of captains who roll Steady; the rest split evenly over the other three.
    public const double SteadyChance = 0.3;

    public static CaptainTrait Roll(Random random)
    {
        if (random.NextDouble() < SteadyChance) return CaptainTrait.Steady;
        return (CaptainTrait)(1 + random.Next(Count - 1));
    }

    // Captains saved before traits existed get one from their id, so it is the same on every load.
    public static CaptainTrait ForLegacyId(string id)
    {
        int hash = 17;
        if (id != null) foreach (char c in id) hash = unchecked(hash * 31 + c);
        return Roll(new Random(hash));
    }

    public static string Name(CaptainTrait trait) => trait switch
    {
        CaptainTrait.GlorySeeker => "Glory-seeker",
        CaptainTrait.Greedy => "Greedy",
        CaptainTrait.Cautious => "Cautious",
        _ => "Steady",
    };

    public static string Description(CaptainTrait trait) => trait switch
    {
        CaptainTrait.GlorySeeker => "Asks to fire the special at weak enemies, even with an elite or the boss still to come.",
        CaptainTrait.Greedy => "Asks for High draw even when Gold is short.",
        CaptainTrait.Cautious => "Asks to fall back away from the gate when the fight turns.",
        _ => "Only makes sound requests.",
    };
}
