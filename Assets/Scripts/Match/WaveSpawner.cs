using System.Collections;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [SerializeField] Enemy enemyPrefab;
    [SerializeField] EnemyPath path;

    // Enemies spawned so far, so groups that use any route take the routes in turn.
    int spawned;

    public bool IsSpawning { get; private set; }

    // The wave being spawned and how many of its enemies are out, so a request can say when the next elite comes.
    WaveDefinition current;
    int spawnedInWave;

    // Spawns until the next elite is out this wave (1 = the very next enemy), or -1 when none is still to come.
    public int SpawnsToElite()
    {
        if (current == null || !IsSpawning) return -1;
        int index = 0;
        foreach (SpawnGroup group in current.groups)
        {
            for (int i = 0; i < group.count; i++, index++)
                if (index >= spawnedInWave && group.enemy != null && group.enemy.elite) return index - spawnedInWave + 1;
        }
        return -1;
    }

    public void SpawnWave(WaveDefinition wave) => StartCoroutine(Run(wave));

    // One enemy straight onto a random road, outside any wave (the enemy base's boss).
    public Enemy SpawnOne(EnemyData data, float healthMultiplier, float goldMultiplier = 1f, Element? element = null)
    {
        Vector3[] points = path.GetPoints(Random.Range(0, path.RouteCount));
        Enemy enemy = Instantiate(enemyPrefab, points[0], Quaternion.identity);
        enemy.Init(data, points, healthMultiplier, goldMultiplier, element);
        return enemy;
    }

    public void StopSpawning()
    {
        StopAllCoroutines();
        IsSpawning = false;
    }

    IEnumerator Run(WaveDefinition wave)
    {
        IsSpawning = true;
        current = wave;
        spawnedInWave = 0;

        foreach (SpawnGroup group in wave.groups)
        {
            if (group.delayBefore > 0f)
                yield return new WaitForSeconds(group.delayBefore);

            for (int i = 0; i < group.count; i++)
            {
                Vector3[] points = path.GetPoints(RouteMath.Pick(group.route, path.RouteCount, spawned++));
                Enemy enemy = Instantiate(enemyPrefab, points[0], Quaternion.identity);
                enemy.Init(group.enemy, points, wave.healthMultiplier, wave.goldMultiplier, group.element);
                spawnedInWave++;
                if (i < group.count - 1)
                    yield return new WaitForSeconds(group.spawnInterval);
            }
        }

        IsSpawning = false;
    }
}
