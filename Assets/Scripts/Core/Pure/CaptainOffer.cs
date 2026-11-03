using System;
using System.Collections.Generic;

// Which captains an offer (recruits, extra AI captains) rolls: a template for each one and the
// seed it's rolled with. The starting captains are drawn by StartingCaptains instead.
public static class CaptainOffer
{
    // The template index and roll seed of each captain in an offer of the given size. Every
    // template is used once before any repeats, and the offer is in template order. The same seed
    // gives the same offer.
    public static List<(int template, int seed)> Draw(int templateCount, int size, int seed)
    {
        var offer = new List<(int template, int seed)>();
        if (templateCount <= 0) return offer;
        var random = new Random(seed);
        var round = new List<int>();
        for (int i = 0; i < size; i++)
        {
            if (round.Count == 0)
                for (int t = 0; t < templateCount; t++) round.Add(t);
            int pick = random.Next(round.Count);
            offer.Add((round[pick], random.Next()));
            round.RemoveAt(pick);
        }
        offer.Sort((a, b) => a.template.CompareTo(b.template));
        return offer;
    }
}
