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
    float towerAttackTimer;
    // Seconds left on each status, indexed by StatusKind.
    readonly float[] statusTime = new float[StatusRules.KindCount];
    float burnDamagePerSecond;
    int tintedStatus = -1;
    // Unscaled time until which the enemy shows the white ability-hit flash.
    float hitFlashUntil;
    const int HitFlashTint = -2;

    float MoveSpeed => Data.moveSpeed * (HasStatus(StatusKind.Slow) ? StatusRules.SlowSpeedMultiplier : 1f);
    float AttackIntervalMultiplier => HasStatus(StatusKind.Stun) ? StatusRules.StunAttackIntervalMultiplier : 1f;

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

        TickStatuses();
        if (!IsTargetable) return;

        // Deployed soldiers in reach block the enemy until one side falls back or dies.
        Soldier blocker = Soldier.FindDeployedNear(transform.position, Data.attackRange);
        if (blocker != null)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                blocker.TakeDamage(Data.attackDamage);
                attackTimer = Data.attackInterval * AttackIntervalMultiplier;
            }
            return;
        }

        TickTowerAttack();

        float step = MoveSpeed * Time.deltaTime;
        if (HasStatus(StatusKind.Dizzy))
        {
            transform.position = PathMath.MoveBack(path, nextPoint, transform.position, step, out nextPoint);
            return;
        }

        Vector3 target = path[nextPoint];
        transform.position = Vector3.MoveTowards(transform.position, target, step);
        if (transform.position == target && ++nextPoint >= path.Length)
            Leak();
    }

    public bool HasStatus(StatusKind kind) => statusTime[(int)kind] > 0f;

    // Reapplying a status refreshes its timer rather than stacking. Burn keeps the strongest caster.
    public void ApplyStatus(StatusKind kind, float casterPower)
    {
        if (!IsTargetable) return;
        if (kind == StatusKind.Burn)
        {
            float damagePerSecond = casterPower * StatusRules.BurnPowerPerSecond;
            burnDamagePerSecond = HasStatus(StatusKind.Burn) ? Mathf.Max(burnDamagePerSecond, damagePerSecond) : damagePerSecond;
        }
        statusTime[(int)kind] = Mathf.Max(statusTime[(int)kind], StatusRules.DurationOf(kind));
    }

    void TickStatuses()
    {
        int tint = -1;
        for (int i = 0; i < statusTime.Length; i++)
        {
            if (statusTime[i] <= 0f) continue;
            statusTime[i] -= Time.deltaTime;
            if (statusTime[i] > 0f && tint < 0) tint = i;
        }
        UpdateTint(tint);

        if (HasStatus(StatusKind.Burn))
            TakeDamage(burnDamagePerSecond * Time.deltaTime);
    }

    // Flashes white for real-time seconds, so the flash shows during an ability's hit-stop.
    public void FlashHit(float seconds)
    {
        if (seconds <= 0f) return;
        hitFlashUntil = Mathf.Max(hitFlashUntil, Time.unscaledTime + seconds);
        UpdateTint(tintedStatus == HitFlashTint ? -1 : tintedStatus);
    }

    // Shows a hit flash, else the first active status, as a tint on the body so both read in grey-box.
    void UpdateTint(int status)
    {
        int tint = Time.unscaledTime < hitFlashUntil ? HitFlashTint : status;
        if (tint == tintedStatus) return;
        tintedStatus = tint;
        if (tint == HitFlashTint) body.color = Color.white;
        else body.color = tint < 0 ? Data.color : Color.Lerp(Data.color, StatusRules.TintOf((StatusKind)tint), 0.6f);
    }

    // A passing hit on its own timer: the enemy keeps walking. It only runs while the enemy walks,
    // so an enemy held up by soldiers fights them and nothing else.
    void TickTowerAttack()
    {
        if (Data.towerDamage <= 0f) return;
        towerAttackTimer -= Time.deltaTime;
        if (towerAttackTimer > 0f) return;
        Tower tower = Tower.FindClosestActive(transform.position, Data.towerAttackRange);
        if (tower == null) return;
        tower.TakeDamage(Data.towerDamage);
        towerAttackTimer = Mathf.Max(0.1f, Data.towerAttackInterval) * AttackIntervalMultiplier;
    }

    public void TakeDamage(float amount)
    {
        if (!IsTargetable) return;
        Health -= amount;
        healthBar.SetFraction(Health / MaxHealth);
        if (Health <= 0f) Die();
    }

    // Pushes the enemy back along its path (never before the start); it then walks on as usual.
    public void Knockback(float distance)
    {
        if (!IsTargetable || !(distance > 0f)) return;
        transform.position = PathMath.MoveBack(path, nextPoint, transform.position, distance, out nextPoint);
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
