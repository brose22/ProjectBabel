using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SpawnGroup
{
    public EnemyData enemy;
    public int count = 5;
    public float spawnInterval = 1f;
    public float delayBefore;
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
    [Tooltip("Gold for one approval gift at a shop.")]
    public int giftCost = 75;
    [Tooltip("Approval a gift adds to the captain it is given to (approval runs -100 to 100).")]
    public float giftApproval = 10f;

    [Header("Events")]
    [Tooltip("Events that event nodes on the world map pick from.")]
    public List<EventData> events = new();

    [Header("Towers")]
    public int maxTowers = 5;
    [Tooltip("How many roster captains the player picks to start a run.")]
    public int partySize = 3;
    [Tooltip("Most captains in the party at once (GDD: 8 per run). Walked-out captains don't count.")]
    public int maxPartySize = 8;
    [Tooltip("Captains offered to recruit after each won city.")]
    public int recruitChoices = 3;
    [Tooltip("Captains available to place, in order.")]
    public List<CaptainData> roster = new();
    [Tooltip("Soldier classes the player can choose from, in display order.")]
    public List<SoldierClassData> availableClasses = new();
    [Tooltip("Class of each soldier slot on a newly placed tower. Slots past the end reuse the last entry; empty uses the first available class.")]
    public List<SoldierClassData> defaultMix = new();
    // Legacy single class from before soldier mixes. Kept so assets saved with only this field
    // still work: it is the last fallback when both lists above are empty.
    [HideInInspector] public SoldierClassData defaultSoldierClass;
    [Tooltip("GDD: 2 at bond ranks D and C.")]
    public int soldiersPerTower = 2;
    [Tooltip("Gold to restore a disabled level 1 tower; scales with the captain's level.")]
    public int restoreBaseCost = 15;
    [Tooltip("Gold taken from the shared pool to relocate a tower mid-match. 0 makes moving free.")]
    public int relocationCost = 25;

    [Header("Cities")]
    [Tooltip("Map templates the world map's cities are played on. Empty keeps the scene's own road.")]
    public List<CityLayout> layouts = new();

    [Header("World map")]
    [Tooltip("Matches in a run, the enemy base included (GDD: about 30; short for testing).")]
    public int matchesPerRun = 7;
    public int minCitiesPerLayer = 2;
    public int maxCitiesPerLayer = 3;
    [Tooltip("How many layers of roads past a won city its intel reveals.")]
    public int intelDepth = 2;
    [Tooltip("Chance of each special node (elite city, rest, shop, event) on the middle layers. The first layer, the layer before the base and the base stay plain cities.")]
    public NodeOdds nodeOdds = new(0.15f, 0.12f, 0.1f, 0.15f);
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
    [Tooltip("Role icons for menus, in CaptainRole order: Attacker, Buffer, Healer.")]
    public Sprite[] roleIcons = new Sprite[3];

    public ElementData ElementInfo(Element element) => elements.Find(e => e != null && e.element == element);

    public Sprite RoleIcon(CaptainRole role) => roleIcons != null && (int)role < roleIcons.Length ? roleIcons[(int)role] : null;

    public IReadOnlyList<SoldierClassData> ChoosableClasses =>
        availableClasses.Count > 0 ? availableClasses
        : defaultSoldierClass != null ? new[] { defaultSoldierClass }
        : Array.Empty<SoldierClassData>();

    public SoldierClassData DefaultClassForSlot(int slot)
    {
        if (defaultMix.Count > 0)
        {
            SoldierClassData fromMix = defaultMix[Mathf.Clamp(slot, 0, defaultMix.Count - 1)];
            if (fromMix != null) return fromMix;
        }
        IReadOnlyList<SoldierClassData> choosable = ChoosableClasses;
        return choosable.Count > 0 && choosable[0] != null ? choosable[0] : defaultSoldierClass;
    }
}
