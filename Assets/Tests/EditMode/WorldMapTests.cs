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
}
