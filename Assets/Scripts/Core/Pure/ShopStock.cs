using System;
using System.Collections.Generic;

// Which items a shop has on its shelves: a few of the full list, picked by the shop's own seed so
// the same shop always stocks the same items.
public static class ShopStock
{
    // Up to count distinct indices into a list of total items, in ascending order.
    public static List<int> Pick(int total, int count, int seed)
    {
        var indices = new List<int>();
        for (int i = 0; i < total; i++) indices.Add(i);
        var random = new Random(seed);
        // Partial Fisher-Yates: the first count entries are the pick.
        int take = Math.Max(0, Math.Min(count, total));
        for (int i = 0; i < take; i++)
        {
            int j = random.Next(i, total);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        indices.RemoveRange(take, total - take);
        indices.Sort();
        return indices;
    }
}
