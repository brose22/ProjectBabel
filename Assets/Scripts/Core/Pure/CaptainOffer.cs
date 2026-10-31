using System;
using System.Collections.Generic;

// Which roster captains a new run offers to pick its party from.
public static class CaptainOffer
{
    // The offer size: the setting, kept between the party size and the roster size.
    public static int Size(int setting, int partySize, int rosterCount) =>
        Math.Min(rosterCount, Math.Max(partySize, setting));

    // The roster indices of a random offer of the given size, in roster order. The same seed
    // gives the same offer.
    public static List<int> Draw(int rosterCount, int size, int seed)
    {
        var indices = new List<int>(rosterCount);
        for (int i = 0; i < rosterCount; i++) indices.Add(i);
        var random = new Random(seed);
        for (int i = 0; i < Math.Min(size, rosterCount); i++)
        {
            int swap = random.Next(i, rosterCount);
            (indices[i], indices[swap]) = (indices[swap], indices[i]);
        }
        List<int> offer = indices.GetRange(0, Math.Max(0, Math.Min(size, rosterCount)));
        offer.Sort();
        return offer;
    }
}
