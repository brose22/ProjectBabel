using System;
using System.Collections.Generic;

// What a list of soldiers or captains shows: one pick per filter (soldier class, element, rarity, or
// captain role, element), each All or one value. Picks combine, so a row shows only when it matches
// every one. Values are enum indices; a row with no value for a filter (a soldier in the inventory
// has no element until a captain leads it) only matches All there.
public class ListFilter
{
    public const int All = -1;

    readonly int[] picks;
    readonly int[] counts;

    // counts holds how many values each filter has, in order.
    public ListFilter(params int[] counts)
    {
        this.counts = (int[])counts.Clone();
        picks = new int[counts.Length];
        Reset();
    }

    public int Count => picks.Length;

    public int Pick(int filter) => picks[filter];

    public bool IsAll => Array.TrueForAll(picks, pick => pick == All);

    public void Reset()
    {
        for (int i = 0; i < picks.Length; i++) picks[i] = All;
    }

    public void Set(int filter, int value) =>
        picks[filter] = value >= 0 && value < counts[filter] ? value : All;

    // All, then each value in order, then back to All; step -1 goes the other way.
    public void Cycle(int filter, int step = 1)
    {
        int options = counts[filter] + 1;
        int at = picks[filter] + 1;
        picks[filter] = ((at + step) % options + options) % options - 1;
    }

    // values holds the row's value for each filter, in order; a negative value is "none".
    public bool Matches(params int[] values)
    {
        for (int i = 0; i < picks.Length; i++)
            if (picks[i] != All && (i >= values.Length || values[i] != picks[i])) return false;
        return true;
    }

    public List<T> Apply<T>(IEnumerable<T> rows, Func<T, int[]> valuesOf)
    {
        var shown = new List<T>();
        foreach (T row in rows)
            if (Matches(valuesOf(row))) shown.Add(row);
        return shown;
    }
}
