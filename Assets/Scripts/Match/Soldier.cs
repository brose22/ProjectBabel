using System.Collections.Generic;
using UnityEngine;

// Soldiers wait at their tower, pay the act cost to run out and hit the closest enemy,
// then return home and recover before they can act again. Dead soldiers respawn after
// a delay once the tower can pay the respawn cost.
public class Soldier : MonoBehaviour
{
    const float RespawnDelay = 3f;
    // How far past the tower's range a soldier will chase before giving up.
    const float LeashSlack = 1f;
    // Size of a shot drawn with the body sprite when no projectile sprite is assigned.
    const float FallbackProjectileScale = 0.35f;

    enum State { Ready, Advancing, Returning, Recovering, Dead }

    static readonly List<Soldier> deployed = new();

    [SerializeField] SpriteRenderer body;
    [SerializeField] HealthBar healthBar;
    [Tooltip("Optional; ranged shots fall back to a shrunken body sprite.")]
    [SerializeField] Sprite projectileSprite;

    public SoldierClassData Class { get; private set; }
    public Tower Tower { get; private set; }
    public bool IsDead => state == State.Dead;
    public int ActCost => Tower.ActCostOf(Class);
    public int RespawnCost => Tower.RespawnCostOf(Class);
    bool IsDeployed => state is State.Advancing or State.Returning;

    Vector3 homeOffset;
    State state;
    Enemy target;
    float timer;
    float health;

    Vector3 Home => Tower.transform.position + homeOffset;

    public void Init(Tower tower, SoldierClassData soldierClass, Vector3 offset)
    {
        Tower = tower;
        Class = soldierClass;
        homeOffset = offset;
        body.color = soldierClass.color;
        Revive();
    }

    // The closest deployed soldier within radius, or null. Soldiers at home can't be attacked.
    public static Soldier FindDeployedNear(Vector3 position, float radius)
    {
        Soldier closest = null;
        float closestDistance = radius;
        foreach (Soldier soldier in deployed)
        {
            float distance = Vector2.Distance(position, soldier.transform.position);
            if (distance <= closestDistance)
            {
                closest = soldier;
                closestDistance = distance;
            }
        }
        return closest;
    }

    void Update()
    {
        switch (state)
        {
            case State.Ready: TickReady(); break;
            case State.Advancing: TickAdvancing(); break;
            case State.Returning: TickReturning(); break;
            case State.Recovering: TickRecovering(); break;
            case State.Dead: TickDead(); break;
        }
    }

    void TickReady()
    {
        Enemy enemy = Tower.FindClosestEnemy(transform.position);
        if (enemy == null || !Tower.TrySpendReserve(ActCost)) return;
        target = enemy;
        SetState(State.Advancing);
    }

    void TickAdvancing()
    {
        if (target == null || !target.IsTargetable || !Tower.IsInRange(target.transform.position, LeashSlack))
        {
            // The act is already paid for, so look for another target before giving up.
            target = Tower.FindClosestEnemy(transform.position);
            if (target == null)
            {
                SetState(State.Returning);
                return;
            }
        }

        // Ranged and magic soldiers already in reach strike without stepping forward.
        bool inReach = Class.attackStyle != SoldierClassData.AttackStyle.Melee &&
            Vector2.Distance(transform.position, target.transform.position) <= Class.attackRange + 0.001f;
        if (inReach || MoveTowards(target.transform.position, Class.attackRange))
        {
            Strike(target);
            target = null;
            SetState(State.Returning);
        }
    }

    void Strike(Enemy enemy)
    {
        float damage = Tower.SoldierPower * Class.damageMultiplier;
        switch (Class.attackStyle)
        {
            case SoldierClassData.AttackStyle.Ranged:
                // Fire and forget: the soldier heads home while the shot is in flight.
                bool hasOwnSprite = projectileSprite != null;
                Projectile.Launch(transform.position, enemy, damage, Class.projectileSpeed,
                    hasOwnSprite ? projectileSprite : body.sprite, Class.color,
                    hasOwnSprite ? 1f : body.transform.lossyScale.x * FallbackProjectileScale,
                    body.sortingOrder + 1);
                break;
            case SoldierClassData.AttackStyle.Magic:
                SplashDamage(enemy, damage);
                break;
            default:
                enemy.TakeDamage(damage);
                enemy.Knockback(Class.knockbackDistance);
                break;
        }
    }

    void SplashDamage(Enemy enemy, float damage)
    {
        if (Class.splashRadius <= 0f)
        {
            enemy.TakeDamage(damage);
            return;
        }

        // Copy first: a kill removes the enemy from Enemy.Active mid-loop.
        Vector3 center = enemy.transform.position;
        var victims = new List<Enemy>(Enemy.Active);
        foreach (Enemy victim in victims)
        {
            if (victim != null && victim.IsTargetable &&
                Vector2.Distance(center, victim.transform.position) <= Class.splashRadius)
                victim.TakeDamage(damage);
        }
    }

    void TickReturning()
    {
        if (!MoveTowards(Home, 0f)) return;
        timer = Tower.ActCooldown;
        SetState(State.Recovering);
    }

    void TickRecovering()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f) SetState(State.Ready);
    }

    void TickDead()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f && Tower.TrySpendReserve(RespawnCost))
            Revive();
    }

    // Returns true once within stopDistance of the destination.
    bool MoveTowards(Vector3 destination, float stopDistance)
    {
        transform.position = Vector3.MoveTowards(transform.position, destination, Class.moveSpeed * Time.deltaTime);
        return Vector2.Distance(transform.position, destination) <= stopDistance + 0.001f;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        health -= amount;
        healthBar.SetFraction(health / Class.maxHealth);
        if (health <= 0f) Die();
    }

    void Die()
    {
        target = null;
        timer = RespawnDelay;
        SetState(State.Dead);
        body.enabled = false;
        healthBar.gameObject.SetActive(false);
        GameEvents.RaiseSoldierDied(this);
    }

    void Revive()
    {
        health = Class.maxHealth;
        transform.position = Home;
        body.enabled = true;
        healthBar.SetFraction(1f);
        SetState(State.Ready);
    }

    void SetState(State next)
    {
        state = next;
        if (IsDeployed)
        {
            if (!deployed.Contains(this)) deployed.Add(this);
        }
        else
        {
            deployed.Remove(this);
        }
    }

    void OnDestroy() => deployed.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => deployed.Clear();
}
