using System;
using System.Collections.Generic;

// One city on the world map. Layer 0 is the first match; the last layer holds only the enemy base.
[Serializable]
public class MapNode
{
    public int id;
    public int layer;
    // Position within the layer, top to bottom, for drawing the map.
    public int lane;
    // Index of the city layout template this match is played on.
    public int layout;
    public List<int> next = new();
    // Revealed by intel; hidden cities and their roads aren't shown.
    public bool revealed;
    public bool cleared;
}

// A run's overworld: a branching graph of cities, layer by layer, where every branch leads to the
// enemy base in the last layer. Kept free of gameplay types so it can be unit tested and saved
// with JsonUtility.
[Serializable]
public class WorldMap
{
    public List<MapNode> nodes = new();
    public int layerCount;
    // The last city won, or -1 before the first match.
    public int current = -1;
    // The city picked for the next match, or -1 while the player is choosing.
    public int target = -1;

    public int BaseId => nodes.Count - 1;
    public MapNode Base => nodes[BaseId];
    public MapNode Current => current >= 0 ? nodes[current] : null;
    public MapNode Target => target >= 0 ? nodes[target] : null;
    public bool IsComplete => Base.cleared;

    // Cities the player can pick next: the first layer at the start, then the roads out of the last city won.
    public List<MapNode> Choices()
    {
        var choices = new List<MapNode>();
        if (IsComplete) return choices;
        if (current < 0)
        {
            foreach (MapNode node in nodes)
                if (node.layer == 0) choices.Add(node);
        }
        else
        {
            foreach (int id in nodes[current].next) choices.Add(nodes[id]);
        }
        return choices;
    }

    public int LayerSize(int layer)
    {
        int size = 0;
        foreach (MapNode node in nodes)
            if (node.layer == layer) size++;
        return size;
    }

    public bool TryChoose(int id)
    {
        if (!Choices().Exists(n => n.id == id)) return false;
        target = id;
        return true;
    }

    // Marks the target city won, moves the player there and reveals the branches ahead with the intel
    // gathered (intelDepth layers of roads beyond it).
    public void ClearTarget(int intelDepth)
    {
        if (target < 0) return;
        nodes[target].cleared = true;
        current = target;
        target = -1;
        RevealFrom(current, intelDepth);
    }

    public void RevealFrom(int id, int depth)
    {
        var frontier = new List<int> { id };
        nodes[id].revealed = true;
        for (int step = 0; step < depth && frontier.Count > 0; step++)
        {
            var nextFrontier = new List<int>();
            foreach (int from in frontier)
                foreach (int to in nodes[from].next)
                {
                    if (!nodes[to].revealed) nodes[to].revealed = true;
                    if (!nextFrontier.Contains(to)) nextFrontier.Add(to);
                }
            frontier = nextFrontier;
        }
    }

    // Builds a map of matchCount layers (the last is the base). Each earlier layer has minLanes to
    // maxLanes cities. Roads between neighbouring layers never cross, and every city has a road in
    // (past the first layer) and a road out (before the base), so every branch reaches the base.
    // The first layer, intel from the starting position and the base are revealed.
    public static WorldMap Generate(int seed, int matchCount, int layoutCount, int minLanes, int maxLanes, int intelDepth)
    {
        var random = new Random(seed);
        matchCount = Math.Max(2, matchCount);
        layoutCount = Math.Max(1, layoutCount);
        minLanes = Math.Max(1, minLanes);
        maxLanes = Math.Max(minLanes, maxLanes);

        var map = new WorldMap { layerCount = matchCount };
        var layers = new List<List<MapNode>>();
        for (int layer = 0; layer < matchCount; layer++)
        {
            int size = layer == matchCount - 1 ? 1 : random.Next(minLanes, maxLanes + 1);
            var nodes = new List<MapNode>();
            for (int lane = 0; lane < size; lane++)
            {
                var node = new MapNode { id = map.nodes.Count, layer = layer, lane = lane, layout = random.Next(layoutCount) };
                map.nodes.Add(node);
                nodes.Add(node);
            }
            layers.Add(nodes);
        }

        for (int layer = 0; layer < matchCount - 1; layer++)
            Connect(layers[layer], layers[layer + 1], random);

        // Try not to play the same layout twice in a row on the most common road.
        if (layoutCount > 1)
            foreach (MapNode node in map.nodes)
                foreach (int id in node.next)
                    if (map.nodes[id].layout == node.layout && map.nodes[id].layer < matchCount - 1)
                        map.nodes[id].layout = (node.layout + 1 + random.Next(layoutCount - 1)) % layoutCount;

        foreach (MapNode node in layers[0]) map.RevealFrom(node.id, Math.Max(0, intelDepth - 1));
        map.Base.revealed = true;
        return map;
    }

    // Walks both layers top to bottom, linking the current pair and then stepping one or both
    // sides down. The result is a staircase: no crossings and nobody left unlinked.
    static void Connect(List<MapNode> from, List<MapNode> to, Random random)
    {
        int i = 0, j = 0;
        Link(from[i], to[j]);
        while (i < from.Count - 1 || j < to.Count - 1)
        {
            bool canStepFrom = i < from.Count - 1, canStepTo = j < to.Count - 1;
            int roll = random.Next(3);
            if (canStepFrom && canStepTo && roll == 0) { i++; j++; }
            else if (canStepFrom && (!canStepTo || roll == 1)) i++;
            else j++;
            Link(from[i], to[j]);
        }
    }

    static void Link(MapNode from, MapNode to)
    {
        if (!from.next.Contains(to.id)) from.next.Add(to.id);
    }
}

// How much harder matches get further into the run. Layer 0 plays the match data as authored.
public static class RunScaling
{
    public static float HealthMultiplier(int layer, float growthPerLayer) => 1f + Math.Max(0f, growthPerLayer) * Math.Max(0, layer);

    // Enemy counts grow by countGrowthPerLayer per layer, rounded, never below the authored count.
    public static int EnemyCount(int count, int layer, float countGrowthPerLayer) =>
        Math.Max(count, (int)Math.Round(count * (1f + Math.Max(0f, countGrowthPerLayer) * Math.Max(0, layer))));
}
