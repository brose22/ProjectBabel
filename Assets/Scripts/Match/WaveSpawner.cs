using System.Collections;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [SerializeField] Enemy enemyPrefab;
    [SerializeField] EnemyPath path;

    public bool IsSpawning { get; private set; }

    public void SpawnWave(WaveDefinition wave) => StartCoroutine(Run(wave));

    // One enemy straight onto the road, outside any wave (the enemy base's boss).
    public Enemy SpawnOne(EnemyData data, float healthMultiplier, float goldMultiplier = 1f)
    {
        Vector3[] points = path.GetPoints();
        Enemy enemy = Instantiate(enemyPrefab, points[0], Quaternion.identity);
        enemy.Init(data, points, healthMultiplier, goldMultiplier);
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
        Vector3[] points = path.GetPoints();

        foreach (SpawnGroup group in wave.groups)
        {
            if (group.delayBefore > 0f)
                yield return new WaitForSeconds(group.delayBefore);

            for (int i = 0; i < group.count; i++)
            {
                Enemy enemy = Instantiate(enemyPrefab, points[0], Quaternion.identity);
                enemy.Init(group.enemy, points, wave.healthMultiplier, wave.goldMultiplier);
                if (i < group.count - 1)
                    yield return new WaitForSeconds(group.spawnInterval);
            }
        }

        IsSpawning = false;
    }
}
