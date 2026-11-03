using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class SpawnGroup
{
    public EnemyData enemy;
    // The element the group fights with in place of the enemy's own; set on the waves a location
    // reshapes, never authored.
    [NonSerialized] public Element? element;
    public int count = 5;
    public float spawnInterval = 1f;
    public float delayBefore;
    [Tooltip("Which route this group walks onto, counting from 0 in the city layout. -1 spreads the group over all routes. Maps with one route ignore it.")]
    public int route = -1;
}

[Serializable]
public class WaveDefinition
{
    public List<SpawnGroup> groups = new();
    [Tooltip("Multiplies every enemy's max health in this wave.")]
    public float healthMultiplier = 1f;
    [HideInInspector] public float goldMultiplier = 1f;
}

[CreateAssetMenu(menuName = "Babel/Match", fileName = "Match")]
public class MatchData : ScriptableObject
{
    [Header("New run (used when no run is in progress)")]
    public int startingHealth = 20;
    public int startingGold = 300;

    [Header("Healing")]
    [Tooltip("Global health restored after every won city (never past the run's max).")]
    public int healthPerWin = 2;
    [Tooltip("Share of max health a rest restores (0.3 = 30%).")]
    [Range(0f, 1f)] public float restHealFraction = 0.3f;

    [Header("Shop")]
    [Tooltip("Gifts every shop sells. They go to the run's inventory and are given to captains from the party window.")]
    public List<GiftData> shopGifts = new();
    [Tooltip("How many of those gifts a single shop has on its shelves.")]
    public int shopSize = 6;

    // The least a gift costs, used by the AI to judge whether a shop is worth visiting.
    public int GiftCost
    {
        get
        {
            int cheapest = int.MaxValue;
            foreach (GiftData gift in shopGifts)
                if (gift != null) cheapest = Mathf.Min(cheapest, gift.cost);
            return cheapest == int.MaxValue ? 75 : cheapest;
        }
    }

    [Header("Events")]
    [Tooltip("Events that event nodes on the world map pick from.")]
    public List<EventData> events = new();

    [Header("Towers")]
    public int maxTowers = 5;
    [Tooltip("How many captains the player picks to start a run.")]
    public int partySize = 3;
    [Tooltip("Most captains in the party at once (GDD: 8 per run). Walked-out captains don't count.")]
    public int maxPartySize = 8;
    [Tooltip("Captains offered to recruit after each won city.")]
    public int recruitChoices = 3;
    [Tooltip("The captain templates captains are rolled from, one per role.")]
    [FormerlySerializedAs("roster")] public List<CaptainData> captainTemplates = new();
    [Tooltip("The soldier classes, one per SoldierClass, in display order. Soldier items find their art and combat stats here.")]
    public List<SoldierClassData> availableClasses = new();
    [Tooltip("Soldier slots open on every captain (GDD: 2 at bond ranks D and C). Higher ranks open more, up to 4.")]
    public int soldiersPerTower = 2;
    [Tooltip("Random soldiers each captain brings when it joins a run, until the starting captains come with their own.")]
    public int startingSoldiersPerCaptain = 2;
    [Tooltip("Gold to restore a disabled level 1 tower; scales with the captain's level.")]
    public int restoreBaseCost = 15;
    [Tooltip("Gold taken from the shared pool to relocate a tower mid-match. 0 makes moving free.")]
    public int relocationCost = 25;

    [Header("Cities")]
    [Tooltip("Map templates the world map's cities are played on. Empty keeps the scene's own road.")]
    public List<CityLayout> layouts = new();

    [Header("Locations")]
    [Tooltip("Where runs are played, in unlock order: winning one opens the next. Each brings its own world map settings, enemies and elements. Empty plays every run with the world map settings below.")]
    public List<LocationData> locations = new();

    // The location with this id, or null when there is none.
    public LocationData FindLocation(string id) => string.IsNullOrEmpty(id) ? null : locations.Find(l => l != null && l.id == id);

    public List<string> LocationIds => locations.ConvertAll(l => l != null ? l.id : null);

    [Header("World map")]
    [Tooltip("Used when the match has no locations, and by AI single matches. Matches in a run, the enemy base included (GDD: about 30; short for testing).")]
    public int matchesPerRun = 7;
    public int minCitiesPerLayer = 2;
    public int maxCitiesPerLayer = 3;
    [Tooltip("How many layers of roads past a won city its intel reveals.")]
    public int intelDepth = 2;
    [Tooltip("The first layer, counting from 0, whose cities play layouts with two gates. Earlier layers play single-gate layouts.")]
    public int doubleGateFirstLayer = 2;
    [Tooltip("The first layer, counting from 0, whose cities (and the enemy base) play layouts with three gates.")]
    public int tripleGateFirstLayer = 5;
    [Tooltip("Chance of each special node (elite city, rest, event) on the middle layers; shop is kept at 0 because the shop opens from the map. The first layer, the layer before the base and the base stay plain cities.")]
    public NodeOdds nodeOdds = new(0.15f, 0.12f, 0.15f);
    [Tooltip("Extra enemy health per match into the run (0.12 = +12% per layer).")]
    public float enemyHealthGrowthPerMatch = 0.12f;
    [Tooltip("Extra Gold per kill and per win further into the run (0.07 = +7% per layer).")]
    public float goldGrowthPerMatch = 0.07f;
    [Tooltip("Flat Gold for winning a match, before the per-layer growth.")]
    public int winGold = 50;
    [Tooltip("Gold for clearing a wave, before the per-wave step and per-layer growth.")]
    public int waveClearGold = 5;
    [Tooltip("Extra Gold per wave number cleared (wave 10 pays waveClearGold + 10 x this).")]
    public int waveClearGoldPerWave = 2;
    [Tooltip("Share of each tower's unspent reserve returned to the Gold pool when a match is won.")]
    [Range(0f, 1f)] public float reserveRefundFraction = 1f;
    [Tooltip("Extra enemies per match into the run (0.08 = +8% per layer, rounded).")]
    public float enemyCountGrowthPerMatch = 0.08f;
    [Tooltip("Elite cities play this many layers tougher than their place on the map.")]
    public int eliteLayerBonus = 2;
    [Tooltip("Elite cities multiply the Gold from kills by this.")]
    public float eliteGoldMultiplier = 2f;
    [Tooltip("Extra captains offered to recruit after winning an elite city.")]
    public int eliteExtraRecruitChoices = 1;
    [Tooltip("The enemy base plays its waves this many times over (GDD: double the waves).")]
    public int baseWaveRepeats = 2;
    [Tooltip("The enemy base's very slow boss (GDD). It walks on with the first wave and the base falls only once it dies. Give it a huge leakDamage so reaching the end loses the run. Empty plays the base without a boss.")]
    public EnemyData boss;

    [Header("Captain XP")]
    [Tooltip("Most XP a captain can earn from healing and buffing in one wave (GDD: support XP is capped per wave). Kill XP is not capped.")]
    public int supportXpCapPerWave = 60;

    [Header("Waves")]
    [Tooltip("Seconds before the next wave starts on its own. The first wave waits for the player.")]
    public float timeBetweenWaves = 15f;
    public List<WaveDefinition> waves = new();

    [Header("Presentation")]
    [Tooltip("How each element is shown in menus (name, colour, icon).")]
    public List<ElementData> elements = new();
    [Tooltip("Role icons for menus, in CaptainRole order: Knight, Mage, Rogue, Archer, Healer.")]
    public Sprite[] roleIcons = new Sprite[5];

    public ElementData ElementInfo(Element element) => elements.Find(e => e != null && e.element == element);

    public Sprite RoleIcon(CaptainRole role) => roleIcons != null && (int)role < roleIcons.Length ? roleIcons[(int)role] : null;

    public IReadOnlyList<SoldierClassData> ChoosableClasses => availableClasses;

    // The asset for a soldier item's class, or null when the match has none.
    public SoldierClassData ClassData(SoldierClass soldierClass) =>
        availableClasses.Find(c => c != null && c.soldierClass == soldierClass);

    // What the soldiers add to one captain stat, from each class's statBonus and their rarity.
    public float StatBonus(IEnumerable<SoldierItem> soldiers, CaptainStat stat) =>
        SoldierRules.StatTotal(soldiers, stat, c => ClassData(c) is SoldierClassData data ? data.statBonus : 0f);
}
