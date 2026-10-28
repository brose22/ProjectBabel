using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> Active = new();

    [SerializeField] SpriteRenderer body;
    [SerializeField] HealthBar healthBar;

    public EnemyData Data { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    // False once the enemy has died or leaked, even before the GameObject is destroyed.
    public bool IsTargetable { get; private set; }

    Vector3[] path;
    int nextPoint;
    float attackTimer;

    public void Init(EnemyData data, Vector3[] pathPoints, float healthMultiplier)
    {
        Data = data;
        path = pathPoints;
        nextPoint = 1;
        MaxHealth = Health = data.maxHealth * healthMultiplier;
        transform.position = path[0];
        body.color = data.color;
        body.transform.localScale = Vector3.one * data.size;
        healthBar.SetFraction(1f);
        IsTargetable = true;
        Active.Add(this);
    }

    void Update()
    {
        if (!IsTargetable) return;

        // Deployed soldiers in reach block the enemy until one side falls back or dies.
        Soldier blocker = Soldier.FindDeployedNear(transform.position, Data.attackRange);
        if (blocker != null)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                blocker.TakeDamage(Data.attackDamage);
                attackTimer = Data.attackInterval;
            }
            return;
        }

        Vector3 target = path[nextPoint];
        transform.position = Vector3.MoveTowards(transform.position, target, Data.moveSpeed * Time.deltaTime);
        if (transform.position == target && ++nextPoint >= path.Length)
            Leak();
    }

    public void TakeDamage(float amount)
    {
        if (!IsTargetable) return;
        Health -= amount;
        healthBar.SetFraction(Health / MaxHealth);
        if (Health <= 0f) Die();
    }

    void Die()
    {
        Remove();
        RunState.Current?.AddGold(Data.goldReward);
        GameEvents.RaiseEnemyKilled(this);
    }

    void Leak()
    {
        Remove();
        RunState.Current?.TakeDamage(Data.leakDamage);
        GameEvents.RaiseEnemyLeaked(this);
    }

    void Remove()
    {
        IsTargetable = false;
        Active.Remove(this);
        Destroy(gameObject);
    }

    void OnDestroy() => Active.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active.Clear();
}
