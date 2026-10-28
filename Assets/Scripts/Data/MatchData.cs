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
    [Tooltip("Captains available to place, in order.")]
    public List<CaptainData> roster = new();
    [Tooltip("Placeholder until the player can choose soldier mixes.")]
    public SoldierClassData defaultSoldierClass;
    [Tooltip("GDD: 2 at bond ranks D and C.")]
    public int soldiersPerTower = 2;

    [Header("Waves")]
    [Tooltip("Seconds before the next wave starts on its own. The first wave waits for the player.")]
    public float timeBetweenWaves = 15f;
    public List<WaveDefinition> waves = new();
}
