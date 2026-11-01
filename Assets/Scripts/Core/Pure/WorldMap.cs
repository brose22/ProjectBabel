using System;
using System.Collections.Generic;

// What a world map node holds. Cities and elite cities are matches; rests, shops and events are
// stops that take the place of a match in their layer.
public enum NodeKind { City, Elite, Rest, Shop, Event }

// The chance of each special node kind on layers that allow them; the rest are plain cities.
[Serializable]
public struct NodeOdds
{
    public float elite;
    public float rest;
    public float shop;
    public float @event;

    public NodeOdds(float elite, float rest, float shop, float @event)
    {
        this.elite = elite;
        this.rest = rest;
        this.shop = shop;
        this.@event = @event;
    }

    // Picks a kind from a roll in [0, 1).
    public NodeKind Pick(double roll)
    {
        float threshold = 0f;
        if (roll < (threshold += Math.Max(0f, elite))) return NodeKind.Elite;
        if (roll < (threshold += Math.Max(0f, rest))) return NodeKind.Rest;
        if (roll < (threshold += Math.Max(0f, shop))) return NodeKind.Shop;
        if (roll < (threshold += Math.Max(0f, @event))) return NodeKind.Event;
        return NodeKind.City;
    }
}

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
    public NodeKind kind;
    // A per-node random number, e.g. to pick which event an event node holds.
    public int seed;
    public List<int> next = new();
    // Revealed by intel; hidden cities and their roads aren't shown.
    public bool revealed;
    public bool cleared;

    public bool IsMatch => kind is NodeKind.City or NodeKind.Elite;
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

    // The base is the one city in the last layer. It is usually the last node, but AddStop can
    // append nodes after it.
    public int BaseId
    {
        get
        {
            for (int i = nodes.Count - 1; i >= 0; i--)
                if (nodes[i].layer == layerCount - 1) return i;
            return nodes.Count - 1;
        }
    }

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

    // Adds a stop of the given kind as one more choice for the next step, in the same layer as
    // the current choices and leading on wherever the first of them does. Null when the next step
    // is the base or a pick is already made. For testing; generated maps never need it.
    public MapNode AddStop(NodeKind kind)
    {
        if (IsComplete || target >= 0) return null;
        List<MapNode> choices = Choices();
        if (choices.Count == 0 || choices[0].layer >= layerCount - 1) return null;
        MapNode sibling = choices[0];
        var stop = new MapNode
        {
            id = nodes.Count,
            layer = sibling.layer,
            lane = LayerSize(sibling.layer),
            layout = sibling.layout,
            kind = kind,
            seed = nodes.Count * 7919 + layerCount,
            next = new List<int>(sibling.next),
            revealed = true,
        };
        nodes.Add(stop);
        if (current >= 0) nodes[current].next.Add(stop.id);
        return stop;
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
    // The first layer, intel from the starting position and the base are revealed. Cities get
    // special kinds by odds, except in the first layer, the layer before the base and the base,
    // which stay plain cities. lateLayouts, when given, are layouts kept out of the layers before
    // lateLayoutsFromLayer, unless they are the only layouts there are.
    public static WorldMap Generate(int seed, int matchCount, int layoutCount, int minLanes, int maxLanes, int intelDepth,
        NodeOdds odds = default, IReadOnlyList<int> lateLayouts = null, int lateLayoutsFromLayer = 0)
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

        if (lateLayouts != null && lateLayouts.Count > 0) HoldBackLayouts(map, layoutCount, new HashSet<int>(lateLayouts), lateLayoutsFromLayer, random);

        foreach (MapNode node in map.nodes)
        {
            node.seed = random.Next();
            if (node.layer > 0 && node.layer < matchCount - 2) node.kind = odds.Pick(random.NextDouble());
        }

        foreach (MapNode node in layers[0]) map.RevealFrom(node.id, Math.Max(0, intelDepth - 1));
        map.Base.revealed = true;
        return map;
    }

    // A map holding one match: a plain city at the given layer, already picked, ahead of a base
    // that is never played. For AI runs that test one map.
    public static WorldMap SingleMatch(int seed, int layerCount, int layout, int layer)
    {
        layerCount = Math.Max(2, layerCount);
        var map = new WorldMap { layerCount = layerCount, target = 0 };
        map.nodes.Add(new MapNode { id = 0, layer = Math.Clamp(layer, 0, layerCount - 2), layout = layout, seed = seed, next = { 1 }, revealed = true });
        map.nodes.Add(new MapNode { id = 1, layer = layerCount - 1, layout = layout, revealed = true });
        return map;
    }

    // Moves cities before fromLayer off the held-back layouts, onto any of the others.
    static void HoldBackLayouts(WorldMap map, int layoutCount, HashSet<int> held, int fromLayer, Random random)
    {
        var open = new List<int>();
        for (int layout = 0; layout < layoutCount; layout++)
            if (!held.Contains(layout)) open.Add(layout);
        if (open.Count == 0) return;
        foreach (MapNode node in map.nodes)
            if (node.layer < fromLayer && held.Contains(node.layout)) node.layout = open[random.Next(open.Count)];
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
    // The layer a match plays as: elite cities play eliteBonus layers further on.
    public static int DifficultyLayer(MapNode node, int eliteBonus) =>
        node == null ? 0 : node.layer + (node.kind == NodeKind.Elite ? Math.Max(0, eliteBonus) : 0);

    public static float HealthMultiplier(int layer, float growthPerLayer) => 1f + Math.Max(0f, growthPerLayer) * Math.Max(0, layer);

    // Kill rewards grow with the layer so Gold keeps pace with the tougher enemies they cost to kill.
    public static float GoldMultiplier(int layer, float growthPerLayer) => 1f + Math.Max(0f, growthPerLayer) * Math.Max(0, layer);

    // The flat Gold for winning a match, grown the same way as kill rewards.
    public static int WinGold(int baseGold, int layer, float growthPerLayer) =>
        Math.Max(0, (int)Math.Round(Math.Max(0, baseGold) * GoldMultiplier(layer, growthPerLayer)));

    // Gold for clearing a wave: a base plus a step for each wave cleared so far (wave is 1-based),
    // grown with the layer like kill rewards. Later waves cost more to fight, so they pay more.
    public static int WaveClearGold(int baseGold, int goldPerWave, int wave, int layer, float growthPerLayer) =>
        Math.Max(0, (int)Math.Round((Math.Max(0, baseGold) + Math.Max(0, goldPerWave) * Math.Max(0, wave)) * GoldMultiplier(layer, growthPerLayer)));

    // Enemy counts grow by countGrowthPerLayer per layer, rounded, never below the authored count.
    public static int EnemyCount(int count, int layer, float countGrowthPerLayer) =>
        Math.Max(count, (int)Math.Round(count * (1f + Math.Max(0f, countGrowthPerLayer) * Math.Max(0, layer))));
}
