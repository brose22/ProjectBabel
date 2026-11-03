using System.Linq;
using NUnit.Framework;

public class LocationRulesTests
{
    const int Water = 0, Fire = 1, Ice = 2, Grass = 3;

    [Test]
    public void Unlocked_TheFirstIsAlwaysOpen()
    {
        Assert.That(LocationRules.IsUnlocked(0, 0, 3), Is.True);
        Assert.That(LocationRules.IsUnlocked(1, 0, 3), Is.False);
        Assert.That(LocationRules.Unlocked(0, 0), Is.EqualTo(0));
    }

    [TestCase(1, 0, true)]
    [TestCase(1, 1, false)]
    [TestCase(2, 1, true)]
    [TestCase(2, 2, false)]
    [TestCase(3, 2, true)]
    [TestCase(3, 3, false)]
    [TestCase(9, 2, true)]
    public void IsUnlocked_OpensTheFirstFew(int unlocked, int index, bool open) =>
        Assert.That(LocationRules.IsUnlocked(index, unlocked, 3), Is.EqualTo(open));

    [Test]
    public void IsUnlocked_RejectsIndexesOutsideTheList()
    {
        Assert.That(LocationRules.IsUnlocked(-1, 3, 3), Is.False);
        Assert.That(LocationRules.IsUnlocked(3, 3, 3), Is.False);
    }

    [Test]
    public void UnlockedAfterWin_OpensTheNextInOrder()
    {
        int unlocked = 1;
        unlocked = LocationRules.UnlockedAfterWin(0, unlocked, 3);
        Assert.That(unlocked, Is.EqualTo(2));
        unlocked = LocationRules.UnlockedAfterWin(1, unlocked, 3);
        Assert.That(unlocked, Is.EqualTo(3));
    }

    [Test]
    public void UnlockedAfterWin_StopsAtTheLastLocation() =>
        Assert.That(LocationRules.UnlockedAfterWin(2, 3, 3), Is.EqualTo(3));

    [Test]
    public void UnlockedAfterWin_ReplayingAnEarlierOneLocksNothing() =>
        Assert.That(LocationRules.UnlockedAfterWin(0, 3, 3), Is.EqualTo(3));

    [Test]
    public void UnlockedAfterWin_UnknownLocationChangesNothing()
    {
        Assert.That(LocationRules.UnlockedAfterWin(-1, 2, 3), Is.EqualTo(2));
        Assert.That(LocationRules.UnlockedAfterWin(5, 2, 3), Is.EqualTo(2));
    }

    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 1)]
    [TestCase(3, 2)]
    [TestCase(7, 2)]
    public void Furthest_IsTheLastOpen(int unlocked, int expected) =>
        Assert.That(LocationRules.Furthest(unlocked, 3), Is.EqualTo(expected));

    [Test]
    public void IndexOf_FindsTheId()
    {
        string[] ids = { "greenmarch", "saltmarsh", "cinderpeak" };
        Assert.That(LocationRules.IndexOf(ids, "saltmarsh"), Is.EqualTo(1));
        Assert.That(LocationRules.IndexOf(ids, "atlantis"), Is.EqualTo(-1));
        Assert.That(LocationRules.IndexOf(ids, null), Is.EqualTo(-1));
    }

    [Test]
    public void StandIn_PicksTheNearestStrength()
    {
        float[] pool = { 30f, 25f, 120f, 90f };
        Assert.That(LocationRules.StandIn(35f, pool), Is.EqualTo(0));
        Assert.That(LocationRules.StandIn(100f, pool), Is.EqualTo(3));
        Assert.That(LocationRules.StandIn(500f, pool), Is.EqualTo(2));
    }

    [Test]
    public void StandIn_TiesGoToTheEarlier() =>
        Assert.That(LocationRules.StandIn(50f, new[] { 40f, 60f }), Is.EqualTo(0));

    [Test]
    public void StandIn_EmptyPoolHasNone() =>
        Assert.That(LocationRules.StandIn(30f, new float[0]), Is.EqualTo(-1));

    [Test]
    public void EnemyElement_KeepsAnElementTheLocationUses() =>
        Assert.That(LocationRules.EnemyElement(Fire, new[] { Grass, Fire }, Grass), Is.EqualTo(Fire));

    [Test]
    public void EnemyElement_OtherElementsTakeTheCitys() =>
        Assert.That(LocationRules.EnemyElement(Water, new[] { Grass, Fire }, Grass), Is.EqualTo(Grass));

    [Test]
    public void EnemyElement_NoLocationElementsKeepsItsOwn()
    {
        Assert.That(LocationRules.EnemyElement(Water, new int[0], Fire), Is.EqualTo(Water));
        Assert.That(LocationRules.EnemyElement(Water, null, Fire), Is.EqualTo(Water));
        Assert.That(LocationRules.EnemyElement(Water, new[] { Fire }, -1), Is.EqualTo(Water));
    }

    [Test]
    public void CityElement_IsOneOfTheLocations()
    {
        int[] elements = { Water, Ice };
        foreach (int seed in new[] { 0, 1, 2, 7, -5, int.MinValue, int.MaxValue })
            Assert.That(elements, Does.Contain(LocationRules.CityElement(seed, elements)));
        Assert.That(LocationRules.CityElement(3, new int[0]), Is.EqualTo(-1));
    }

    static LocationSpec Spec(int matches, int minLanes, int maxLanes, params int[] elements) => new()
    {
        map = new LocationMapSettings { matchesPerRun = matches, minCitiesPerLayer = minLanes, maxCitiesPerLayer = maxLanes },
        elements = elements,
    };

    [Test]
    public void Generate_UsesTheLocationsMapSettings()
    {
        WorldMap map = WorldMap.Generate(4, Spec(9, 3, 4, Fire), 4, 2);
        Assert.That(map.layerCount, Is.EqualTo(9));
        for (int layer = 0; layer < 8; layer++) Assert.That(map.LayerSize(layer), Is.InRange(3, 4));
    }

    [Test]
    public void Generate_GivesEveryCityOneOfTheLocationsElements()
    {
        int[] elements = { Water, Ice };
        WorldMap map = WorldMap.Generate(11, Spec(11, 2, 4, elements), 4, 2);
        Assert.That(map.nodes.All(n => elements.Contains(n.element)));
        Assert.That(map.nodes.Select(n => n.element).Distinct().Count(), Is.EqualTo(2), "a long map leans to both elements somewhere");
    }

    [Test]
    public void Generate_WithoutElementsLeavesCitiesWithout() =>
        Assert.That(WorldMap.Generate(3, Spec(7, 2, 3), 4, 2).nodes.All(n => n.element == -1));

    [Test]
    public void Generate_ForALocationIsTheSameForTheSameSeed()
    {
        LocationSpec spec = Spec(9, 2, 3, Fire, Ice);
        WorldMap a = WorldMap.Generate(21, spec, 4, 2), b = WorldMap.Generate(21, spec, 4, 2);
        Assert.That(a.nodes.Select(n => (n.layer, n.lane, n.layout, n.kind, n.element, string.Join(",", n.next))),
            Is.EqualTo(b.nodes.Select(n => (n.layer, n.lane, n.layout, n.kind, n.element, string.Join(",", n.next)))));
    }

    [Test]
    public void Generate_ForALocationMatchesThePlainGenerator()
    {
        var settings = new LocationMapSettings { matchesPerRun = 8, minCitiesPerLayer = 2, maxCitiesPerLayer = 3 };
        WorldMap located = WorldMap.Generate(5, new LocationSpec { map = settings }, 4, 2);
        WorldMap plain = WorldMap.Generate(5, 8, 4, 2, 3, 2, settings.nodeOdds, null, settings.doubleGateFirstLayer, settings.tripleGateFirstLayer);
        Assert.That(located.nodes.Select(n => (n.layer, n.lane, n.layout, n.kind, string.Join(",", n.next))),
            Is.EqualTo(plain.nodes.Select(n => (n.layer, n.lane, n.layout, n.kind, string.Join(",", n.next)))));
    }
}
