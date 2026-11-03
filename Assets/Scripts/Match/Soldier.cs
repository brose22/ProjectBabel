using System.Collections.Generic;
using UnityEngine;

// A fielded soldier item. Soldiers wait at their tower, run out to hit the closest enemy with
// their class's damage type and their captain's element, then return home and recover before
// they can act again. Acting is free; dead soldiers respawn after a delay once the tower can pay
// the respawn cost.
public class Soldier : MonoBehaviour
{
    const float RespawnDelay = 3f;
    // How far past the tower's range a soldier will chase before giving up.
    const float LeashSlack = 1f;
    // Size of a shot drawn with the body sprite when no projectile sprite is assigned.
    const float FallbackProjectileScale = 0.35f;

    // Spawn pop-in length, and the sparks that gather in or scatter out around a soldier.
    const float PopTime = 0.25f;
    const int FxPieces = 6;
    const float FxSize = 0.08f;
    const float GatherRadius = 0.35f;

    enum State { Ready, Advancing, Returning, Recovering, Dead }

    static readonly List<Soldier> deployed = new();

    [SerializeField] SpriteRenderer body;
    [SerializeField] HealthBar healthBar;
    [Tooltip("Optional; ranged shots fall back to a shrunken body sprite.")]
    [SerializeField] Sprite projectileSprite;

    public SoldierClassData Class { get; private set; }
    // The owned soldier this is; its rarity and slot.
    public SoldierItem Item { get; private set; }
    public Tower Tower { get; private set; }
    public bool IsDead => state == State.Dead;
    // Soldier health scales with the captain's level, like their damage.
    public float MaxHealth => Class.maxHealth * LevelMath.StatFactor(Tower.Level);
    public int RespawnCost => Tower.RespawnCostOf(Class);
    bool IsDeployed => state is State.Advancing or State.Returning;

    Vector3 homeOffset;
    State state;
    Enemy target;
    float timer;
    float health;
    Vector3 bodyScale = Vector3.one;
    float popTimer;

    Vector3 Home => Tower.transform.position + homeOffset;

    public void Init(Tower tower, SoldierItem item, SoldierClassData soldierClass, Vector3 offset)
    {
        Tower = tower;
        Item = item;
        Class = soldierClass;
        homeOffset = offset;
        if (soldierClass.sprite != null)
        {
            // Drawn art is 8 px, half a tile, and shows as painted.
            body.sprite = soldierClass.sprite;
            body.transform.localScale = Vector3.one;
            body.color = Color.white;
        }
        else
        {
            body.color = soldierClass.color;
        }
        bodyScale = body.transform.localScale;
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
        if (popTimer > 0f) TickPop();
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
        if (Tower.IsWalking) return;
        Enemy enemy = Tower.FindClosestEnemy(transform.position);
        if (enemy == null) return;
        target = enemy;
        SetState(State.Advancing);
    }

    void TickAdvancing()
    {
        if (target == null || !target.IsTargetable || !Tower.IsInRange(target.transform.position, LeashSlack))
        {
            // Look for another target before giving up.
            target = Tower.FindClosestEnemy(transform.position);
            if (target == null)
            {
                SetState(State.Returning);
                return;
            }
        }

        // Ranged and magic soldiers already in reach strike without stepping forward.
        bool inReach = Class.damageType != DamageType.Melee &&
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
        switch (Class.damageType)
        {
            case DamageType.Range:
                // Fire and forget: the soldier heads home while the shot is in flight.
                bool hasOwnSprite = projectileSprite != null;
                Projectile.Launch(transform.position, enemy, damage, Tower, Class, Class.projectileSpeed,
                    hasOwnSprite ? projectileSprite : body.sprite, Class.color,
                    hasOwnSprite ? 1f : body.transform.lossyScale.x * FallbackProjectileScale,
                    body.sortingOrder + 1);
                break;
            case DamageType.Magic:
                SplashDamage(enemy, damage);
                break;
            default:
                enemy.TakeDamage(damage, Tower, damageType: Class.damageType);
                if (Random.value < Class.knockbackChance) enemy.Knockback(Class.knockbackDistance, Tower);
                break;
        }
    }

    void SplashDamage(Enemy enemy, float damage)
    {
        if (Class.splashRadius <= 0f)
        {
            enemy.TakeDamage(damage, Tower, damageType: Class.damageType);
            return;
        }

        // Copy first: a kill removes the enemy from Enemy.Active mid-loop.
        Vector3 center = enemy.transform.position;
        var victims = new List<Enemy>(Enemy.Active);
        foreach (Enemy victim in victims)
        {
            if (victim != null && victim.IsTargetable &&
                Vector2.Distance(center, victim.transform.position) <= Class.splashRadius)
                victim.TakeDamage(damage, Tower, damageType: Class.damageType);
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
        if (timer > 0f) return;
        int cost = RespawnCost;
        if (!Tower.TrySpendReserve(cost)) return;
        GameEvents.RaiseSoldierRespawned(this, cost);
        Revive();
    }

    // Returns true once within stopDistance of the destination.
    bool MoveTowards(Vector3 destination, float stopDistance)
    {
        transform.position = Vector3.MoveTowards(transform.position, destination, Class.moveSpeed * Time.deltaTime);
        return Vector2.Distance(transform.position, destination) <= stopDistance + 0.001f;
    }

    // A soldier out on an act heads for the new home when it returns.
    public void SetHome(Vector3 offset)
    {
        homeOffset = offset;
        if (!IsDeployed && !IsDead) transform.position = Home;
    }

    // Cancels any act in progress and snaps home. Deployed soldiers recover as if they had
    // returned normally; dead soldiers stay dead and keep their respawn timer.
    public void ReturnHome()
    {
        if (IsDead) return;
        target = null;
        transform.position = Home;
        if (IsDeployed)
        {
            timer = Tower.ActCooldown;
            SetState(State.Recovering);
        }
    }

    // Cheat: soldiers still take damage but stop at 1 health instead of dying.
    public static bool CannotDie;

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        health -= Tower.DamageAfterDefense(amount);
        if (CannotDie) health = Mathf.Max(health, 1f);
        healthBar.SetFraction(health / MaxHealth);
        if (health <= 0f) Die();
    }

    // Returns the health actually restored, which is less than amount near full health.
    public float Heal(float amount)
    {
        if (IsDead || amount <= 0f || !gameObject.activeSelf) return 0f;
        float healed = Mathf.Min(amount, MaxHealth - health);
        if (healed <= 0f) return 0f;
        health += healed;
        healthBar.SetFraction(health / MaxHealth);
        return healed;
    }

    // Scales the body in from nothing with a small overshoot.
    void TickPop()
    {
        popTimer = Mathf.Max(0f, popTimer - Time.deltaTime);
        float t = 1f - popTimer / PopTime;
        float overshoot = 1f + 2.7f * Mathf.Pow(t - 1f, 3f) + 1.7f * Mathf.Pow(t - 1f, 2f);
        body.transform.localScale = bodyScale * overshoot;
    }

    void PlaySpawnFx()
    {
        if (!GameSettings.SoldierSpawnEffects) return;
        popTimer = PopTime;
        body.transform.localScale = Vector3.zero;
        Burst.Spawn(transform.position, Class.color, FxPieces, 0f, FxSize, PopTime, GatherRadius);
    }

    void PlayDespawnFx()
    {
        if (!GameSettings.SoldierSpawnEffects) return;
        Burst.Spawn(transform.position, Class.color, FxPieces, 1.2f, FxSize, 0.3f);
    }

    void Die()
    {
        PlayDespawnFx();
        target = null;
        timer = RespawnDelay;
        SetState(State.Dead);
        body.enabled = false;
        healthBar.gameObject.SetActive(false);
        GameEvents.RaiseSoldierDied(this);
    }

    // Called when the tower is disabled. Not a death: no event, no casualty, no respawn timer.
    public void Despawn()
    {
        PlayDespawnFx();
        popTimer = 0f;
        body.transform.localScale = bodyScale;
        target = null;
        SetState(State.Ready);
        gameObject.SetActive(false);
    }

    // Called when the tower is restored: the soldier comes back fresh at home.
    public void Respawn()
    {
        gameObject.SetActive(true);
        Revive();
    }

    void Revive()
    {
        health = MaxHealth;
        transform.position = Home;
        body.enabled = true;
        healthBar.SetFraction(1f);
        SetState(State.Ready);
        popTimer = 0f;
        body.transform.localScale = bodyScale;
        PlaySpawnFx();
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
