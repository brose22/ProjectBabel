using System.Linq;
using NUnit.Framework;

public class WorldMapTests
{
    static WorldMap Map(int seed = 1, int matches = 7, int intelDepth = 2) =>
        WorldMap.Generate(seed, matches, 4, 2, 3, intelDepth);

    [Test]
    public void Generate_EndsInASingleBase()
    {
        WorldMap map = Map();
        Assert.That(map.layerCount, Is.EqualTo(7));
        Assert.That(map.LayerSize(6), Is.EqualTo(1));
        Assert.That(map.Base.layer, Is.EqualTo(6));
        Assert.That(map.Base.next, Is.Empty);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(42)]
    [TestCase(1234)]
    public void Generate_EveryCityHasRoadsInAndOut(int seed)
    {
        WorldMap map = Map(seed);
        foreach (MapNode node in map.nodes)
        {
            Assert.That(map.LayerSize(node.layer), Is.InRange(1, 3));
            if (node.id != map.BaseId) Assert.That(node.next, Is.Not.Empty, $"city {node.id} has no road out");
            if (node.layer > 0) Assert.That(map.nodes.Any(n => n.next.Contains(node.id)), $"city {node.id} has no road in");
            foreach (int next in node.next) Assert.That(map.nodes[next].layer, Is.EqualTo(node.layer + 1));
        }
    }

    [TestCase(5)]
    [TestCase(77)]
    public void Generate_RoadsNeverCross(int seed)
    {
        WorldMap map = Map(seed);
        var roads = map.nodes.SelectMany(n => n.next.Select(to => (from: n, to: map.nodes[to]))).ToList();
        foreach (var a in roads)
            foreach (var b in roads)
                if (a.from.layer == b.from.layer && a.from.lane < b.from.lane)
                    Assert.That(a.to.lane, Is.LessThanOrEqualTo(b.to.lane));
    }

    [Test]
    public void Generate_IsTheSameForTheSameSeed()
    {
        WorldMap a = Map(9), b = Map(9);
        Assert.That(a.nodes.Select(n => (n.layer, n.lane, n.layout, string.Join(",", n.next))),
            Is.EqualTo(b.nodes.Select(n => (n.layer, n.lane, n.layout, string.Join(",", n.next)))));
    }

    [Test]
    public void Generate_RevealsTheFirstLayerAndTheBase()
    {
        WorldMap map = Map(intelDepth: 1);
        foreach (MapNode node in map.nodes)
            Assert.That(node.revealed, Is.EqualTo(node.layer == 0 || node.id == map.BaseId), $"city {node.id}");
    }

    [Test]
    public void Choices_StartWithTheFirstLayer()
    {
        WorldMap map = Map();
        Assert.That(map.Choices().All(n => n.layer == 0));
        Assert.That(map.Choices().Count, Is.EqualTo(map.LayerSize(0)));
    }

    [Test]
    public void AddStop_AtTheStartJoinsTheFirstLayer()
    {
        WorldMap map = Map();
        int before = map.Choices().Count;
        MapNode shop = map.AddStop(NodeKind.Shop);
        Assert.That(shop, Is.Not.Null);
        Assert.That(shop.kind, Is.EqualTo(NodeKind.Shop));
        Assert.That(map.Choices().Count, Is.EqualTo(before + 1));
        Assert.That(map.Choices().Contains(shop));
        Assert.That(shop.next, Is.Not.Empty);
        Assert.That(map.TryChoose(shop.id), Is.True);
    }

    [Test]
    public void AddStop_AfterAWinIsReachableFromTheCurrentCity()
    {
        WorldMap map = Map();
        map.TryChoose(0);
        map.ClearTarget(2);
        MapNode shop = map.AddStop(NodeKind.Shop);
        Assert.That(map.nodes[map.current].next, Does.Contain(shop.id));
        Assert.That(shop.layer, Is.EqualTo(map.nodes[map.current].layer + 1));
        Assert.That(map.BaseId, Is.Not.EqualTo(shop.id));
        Assert.That(map.Base.layer, Is.EqualTo(map.layerCount - 1));
    }

    [Test]
    public void AddStop_RefusesWhenTheBaseIsNext()
    {
        WorldMap map = Map(matches: 2);
        map.TryChoose(map.Choices()[0].id);
        map.ClearTarget(2);
        Assert.That(map.AddStop(NodeKind.Shop), Is.Null);
    }

    [Test]
    public void TryChoose_OnlyAcceptsReachableCities()
    {
        WorldMap map = Map();
        Assert.That(map.TryChoose(map.BaseId), Is.False);
        Assert.That(map.TryChoose(0), Is.True);
        Assert.That(map.target, Is.EqualTo(0));
    }

    [Test]
    public void ClearTarget_MovesOnAndRevealsTheRoadsAhead()
    {
        WorldMap map = Map(intelDepth: 2);
        map.TryChoose(0);
        map.ClearTarget(2);

        Assert.That(map.current, Is.EqualTo(0));
        Assert.That(map.target, Is.EqualTo(-1));
        Assert.That(map.nodes[0].cleared);
        foreach (int next in map.nodes[0].next)
        {
            Assert.That(map.nodes[next].revealed);
            foreach (int after in map.nodes[next].next) Assert.That(map.nodes[after].revealed);
        }
        Assert.That(map.Choices().Select(n => n.id), Is.EquivalentTo(map.nodes[0].next));
    }

    [Test]
    public void ClearingTheBase_CompletesTheMap()
    {
        WorldMap map = Map(matches: 3);
        while (!map.IsComplete)
        {
            Assert.That(map.TryChoose(map.Choices()[0].id));
            map.ClearTarget(2);
        }
        Assert.That(map.current, Is.EqualTo(map.BaseId));
        Assert.That(map.Choices(), Is.Empty);
    }

    [Test]
    public void RunScaling_LeavesTheFirstLayerAsAuthored()
    {
        Assert.That(RunScaling.HealthMultiplier(0, 0.15f), Is.EqualTo(1f));
        Assert.That(RunScaling.EnemyCount(4, 0, 0.1f), Is.EqualTo(4));
    }

    [Test]
    public void RunScaling_GrowsWithLayer()
    {
        Assert.That(RunScaling.HealthMultiplier(4, 0.15f), Is.EqualTo(1.6f).Within(1e-5f));
        Assert.That(RunScaling.EnemyCount(4, 5, 0.1f), Is.EqualTo(6));
        Assert.That(RunScaling.EnemyCount(1, 1, 0.1f), Is.EqualTo(1));
    }

    [Test]
    public void RunScaling_ElitesPlayFurtherOn()
    {
        Assert.That(RunScaling.DifficultyLayer(new MapNode { layer = 3 }, 2), Is.EqualTo(3));
        Assert.That(RunScaling.DifficultyLayer(new MapNode { layer = 3, kind = NodeKind.Elite }, 2), Is.EqualTo(5));
        Assert.That(RunScaling.DifficultyLayer(null, 2), Is.EqualTo(0));
    }

    [Test]
    public void Generate_WithoutOdds_MakesOnlyCities()
    {
        Assert.That(Map().nodes.All(n => n.kind == NodeKind.City));
    }

    [TestCase(3)]
    [TestCase(8)]
    [TestCase(21)]
    public void Generate_KeepsTheFirstLastAndPreBaseLayersPlain(int seed)
    {
        WorldMap map = WorldMap.Generate(seed, 9, 4, 2, 3, 2, new NodeOdds(0.25f, 0.25f, 0.25f, 0.25f));
        foreach (MapNode node in map.nodes)
            if (node.layer == 0 || node.layer >= map.layerCount - 2)
                Assert.That(node.kind, Is.EqualTo(NodeKind.City), $"city {node.id} on layer {node.layer}");
        Assert.That(map.nodes.Where(n => n.layer > 0 && n.layer < map.layerCount - 2).All(n => n.kind != NodeKind.City));
    }

    [Test]
    public void Generate_KindsAreTheSameForTheSameSeed()
    {
        var odds = new NodeOdds(0.2f, 0.2f, 0.2f, 0.2f);
        WorldMap a = WorldMap.Generate(4, 9, 4, 2, 3, 2, odds), b = WorldMap.Generate(4, 9, 4, 2, 3, 2, odds);
        Assert.That(a.nodes.Select(n => (n.kind, n.seed)), Is.EqualTo(b.nodes.Select(n => (n.kind, n.seed))));
    }

    [Test]
    public void NodeOdds_PickFollowsTheThresholds()
    {
        var odds = new NodeOdds(0.1f, 0.2f, 0.3f, 0.1f);
        Assert.That(odds.Pick(0.05), Is.EqualTo(NodeKind.Elite));
        Assert.That(odds.Pick(0.15), Is.EqualTo(NodeKind.Rest));
        Assert.That(odds.Pick(0.45), Is.EqualTo(NodeKind.Shop));
        Assert.That(odds.Pick(0.65), Is.EqualTo(NodeKind.Event));
        Assert.That(odds.Pick(0.75), Is.EqualTo(NodeKind.City));
    }

    [Test]
    public void MapNode_OnlyCitiesAndElitesAreMatches()
    {
        Assert.That(new MapNode { kind = NodeKind.City }.IsMatch);
        Assert.That(new MapNode { kind = NodeKind.Elite }.IsMatch);
        Assert.That(new MapNode { kind = NodeKind.Rest }.IsMatch, Is.False);
        Assert.That(new MapNode { kind = NodeKind.Shop }.IsMatch, Is.False);
        Assert.That(new MapNode { kind = NodeKind.Event }.IsMatch, Is.False);
    }

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(99)]
    public void Generate_PlaysTheBaseOnOneOfTheBaseLayouts(int seed)
    {
        WorldMap map = WorldMap.Generate(seed, 7, 5, 2, 3, 2, default, new[] { 3, 4 });
        Assert.That(new[] { 3, 4 }, Does.Contain(map.Base.layout));
    }

    [Test]
    public void Generate_WithoutBaseLayoutsChangesNothing()
    {
        WorldMap plain = WorldMap.Generate(5, 7, 5, 2, 3, 2);
        WorldMap empty = WorldMap.Generate(5, 7, 5, 2, 3, 2, default, new int[0]);
        Assert.That(empty.nodes.Select(n => n.layout), Is.EqualTo(plain.nodes.Select(n => n.layout)));
    }
}
