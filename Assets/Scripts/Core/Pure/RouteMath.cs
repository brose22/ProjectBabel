using System;
using System.Collections.Generic;

// Geometry for maps with several enemy routes that all end at the same castle, kept free of
// gameplay types so it can be unit tested.
public static class RouteMath
{
    // The route a spawn uses: the requested one, or with a negative request ("any route") each
    // spawn in turn takes the next route so enemies split evenly. A route index past the end wraps.
    public static int Pick(int requested, int routeCount, int sequence)
    {
        if (routeCount <= 0) return 0;
        int index = requested >= 0 ? requested : sequence;
        return ((index % routeCount) + routeCount) % routeCount;
    }

    // Every distinct road cell of all routes, from the cells furthest from the castle to the castle
    // itself. A cell's distance is its fewest steps to the castle along any route through it; with
    // one route this is just the route's own order. Ties keep the order the cells were first met in.
    public static List<(int x, int y)> OrderedRoadCells(IReadOnlyList<IReadOnlyList<(int x, int y)>> routes)
    {
        var steps = new Dictionary<(int x, int y), int>();
        var firstSeen = new List<(int x, int y)>();
        foreach (IReadOnlyList<(int x, int y)> route in routes)
        {
            for (int i = 0; i < route.Count; i++)
            {
                int toCastle = route.Count - 1 - i;
                if (!steps.TryGetValue(route[i], out int known))
                {
                    steps[route[i]] = toCastle;
                    firstSeen.Add(route[i]);
                }
                else if (toCastle < known) steps[route[i]] = toCastle;
            }
        }

        var order = new List<int>();
        for (int i = 0; i < firstSeen.Count; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            int byDistance = steps[firstSeen[b]].CompareTo(steps[firstSeen[a]]);
            return byDistance != 0 ? byDistance : a.CompareTo(b);
        });
        return order.ConvertAll(i => firstSeen[i]);
    }

    // The road cell nearest a position, as its route and its index along that route. Positions are
    // in cell units, with a cell's centre half a unit in. Ties keep the earliest route and cell.
    public static (int route, int index) Nearest(IReadOnlyList<IReadOnlyList<(int x, int y)>> routes, float x, float y)
    {
        (int route, int index) nearest = (0, 0);
        float best = float.MaxValue;
        for (int route = 0; route < routes.Count; route++)
        {
            for (int i = 0; i < routes[route].Count; i++)
            {
                float dx = routes[route][i].x + 0.5f - x, dy = routes[route][i].y + 0.5f - y;
                float distance = dx * dx + dy * dy;
                if (distance >= best) continue;
                best = distance;
                nearest = (route, i);
            }
        }
        return nearest;
    }

    // Which side a gate lies on compared with the map's other gates, for naming it to the player:
    // "north", "south", "east" or "west". Empty with no other gate to tell it from.
    public static string GateDirection(IReadOnlyList<(int x, int y)> gates, int gate)
    {
        if (gates.Count < 2) return "";
        float meanX = 0f, meanY = 0f;
        foreach ((int x, int y) in gates)
        {
            meanX += x;
            meanY += y;
        }
        float dx = gates[gate].x - meanX / gates.Count, dy = gates[gate].y - meanY / gates.Count;
        if (dx == 0f && dy == 0f) return "";
        if (Math.Abs(dy) >= Math.Abs(dx)) return dy > 0f ? "north" : "south";
        return dx > 0f ? "east" : "west";
    }

    // True when every route ends on the same cell, which is where the castle sits.
    public static bool EndAtTheSameCell(IReadOnlyList<IReadOnlyList<(int x, int y)>> routes)
    {
        for (int i = 1; i < routes.Count; i++)
        {
            if (routes[i].Count == 0 || routes[0].Count == 0) return false;
            if (routes[i][routes[i].Count - 1] != routes[0][routes[0].Count - 1]) return false;
        }
        return true;
    }
}
