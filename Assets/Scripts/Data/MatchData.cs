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
}

[CreateAssetMenu(menuName = "Babel/Match", fileName = "Match")]
public class MatchData : ScriptableObject
{
    [Header("New run (used when no run is in progress)")]
    public int startingHealth = 20;
    public int startingGold = 300;

    [Header("Towers")]
    public int maxTowers = 5;
    [Tooltip("How many roster captains the player takes on a run.")]
    public int partySize = 3;
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
    [Tooltip("Extra enemy health per match into the run (0.12 = +12% per layer).")]
    public float enemyHealthGrowthPerMatch = 0.12f;
    [Tooltip("Extra enemies per match into the run (0.08 = +8% per layer, rounded).")]
    public float enemyCountGrowthPerMatch = 0.08f;
    [Tooltip("The enemy base plays its waves this many times over (GDD: double the waves).")]
    public int baseWaveRepeats = 2;

    [Header("Captain XP")]
    [Tooltip("Most XP a captain can earn from healing and buffing in one wave (GDD: support XP is capped per wave). Kill XP is not capped.")]
    public int supportXpCapPerWave = 60;

    [Header("Waves")]
    [Tooltip("Seconds before the next wave starts on its own. The first wave waits for the player.")]
    public float timeBetweenWaves = 15f;
    public List<WaveDefinition> waves = new();

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
