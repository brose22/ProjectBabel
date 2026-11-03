using System;
using System.Collections.Generic;

// How a location shapes its world map (item: location). Kept free of gameplay types so the map
// generator can take it and tests can build one; LocationData fills it from the asset.
[Serializable]
public class LocationMapSettings
{
    public int matchesPerRun = 7;
    public int minCitiesPerLayer = 2;
    public int maxCitiesPerLayer = 3;
    public int doubleGateFirstLayer = 2;
    public int tripleGateFirstLayer = 5;
    public NodeOdds nodeOdds = new(0.15f, 0.12f, 0.15f);
}

// What the world map generator needs from a location: its map settings and the elements its
// enemies use, as Element values (Water 0, Fire 1, Ice 2, Grass 3).
public class LocationSpec
{
    public LocationMapSettings map = new();
    public int[] elements = Array.Empty<int>();
}

// Locations unlock in order: the profile keeps how many are open, and winning one opens the next.
// Also how a location's enemy pool and elements reshape the authored waves.
public static class LocationRules
{
    // The first location is always open.
    public static int Unlocked(int unlockedCount, int locationCount) => Math.Clamp(unlockedCount, Math.Min(1, locationCount), locationCount);

    public static bool IsUnlocked(int index, int unlockedCount, int locationCount) =>
        index >= 0 && index < Unlocked(unlockedCount, locationCount);

    // How many are open after winning the location at index: at least up to the next one. Winning
    // an earlier location again never locks anything.
    public static int UnlockedAfterWin(int wonIndex, int unlockedCount, int locationCount)
    {
        int unlocked = Unlocked(unlockedCount, locationCount);
        if (wonIndex < 0 || wonIndex >= locationCount) return unlocked;
        return Math.Max(unlocked, Math.Min(locationCount, wonIndex + 2));
    }

    // The furthest open location, where a run starts when nobody picked one.
    public static int Furthest(int unlockedCount, int locationCount) => Math.Max(0, Unlocked(unlockedCount, locationCount) - 1);

    // The index of the location with this id, or -1.
    public static int IndexOf(IReadOnlyList<string> ids, string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] == id) return i;
        return -1;
    }

    // An enemy the location doesn't field is played by the pool enemy nearest it in strength (max
    // health), the earlier one on a tie. -1 for an empty pool.
    public static int StandIn(float strength, IReadOnlyList<float> poolStrengths)
    {
        int best = -1;
        float bestGap = float.MaxValue;
        for (int i = 0; i < poolStrengths.Count; i++)
        {
            float gap = Math.Abs(poolStrengths[i] - strength);
            if (gap < bestGap)
            {
                best = i;
                bestGap = gap;
            }
        }
        return best;
    }

    // An enemy keeps its own element when the location uses it. Otherwise it takes the city's
    // element, so each city leans to one of the location's elements. With no elements set, or no
    // city element, every enemy keeps its own.
    public static int EnemyElement(int own, IReadOnlyList<int> locationElements, int cityElement)
    {
        if (locationElements == null || locationElements.Count == 0 || cityElement < 0) return own;
        foreach (int element in locationElements)
            if (element == own) return own;
        return cityElement;
    }

    // The element a city leans to, picked by its seed from the location's elements; -1 when the
    // location sets none.
    public static int CityElement(int seed, IReadOnlyList<int> locationElements) =>
        locationElements == null || locationElements.Count == 0 ? -1 : locationElements[(seed & int.MaxValue) % locationElements.Count];
}
