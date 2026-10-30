using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> Active = new();

    const float WeaknessMarkerSize = 0.15f;
    const float ShotStreakWidth = 0.05f;
    const float ShotStreakSeconds = 0.12f;
    const float StatusIconHeight = 0.7f;
    // Enemy art is drawn for this size; other sizes scale the sprite.
    const float ArtSize = 0.6f;

    [SerializeField] SpriteRenderer body;
    [SerializeField] HealthBar healthBar;

    public EnemyData Data { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    // False once the enemy has died or leaked, even before the GameObject is destroyed.
    public bool IsTargetable { get; private set; }
    // The tower credited with the kill; null for leaks or kills with no tower behind them.
    public Tower KilledBy { get; private set; }

    Vector3[] path;
    int nextPoint;
    float attackTimer;
    float towerAttackTimer;
    // Seconds left on each status, indexed by StatusKind.
    readonly float[] statusTime = new float[StatusRules.KindCount];
    float burnDamagePerSecond;
    Tower burnSource;
    StatusIcons statusIcons;
    int tintedStatus = -1;
    // The body colour with no status: white for drawn art, or the data colour for squares.
    Color baseColor = Color.white;
    // Unscaled time until which the enemy shows the white ability-hit flash.
    float hitFlashUntil;
    // Every tower or soldier hit flashes white and swells the body briefly.
    const float HitPopSeconds = 0.05f;
    const float HitPopScale = 1.15f;
    Vector3 bodyScale = Vector3.one;
    float hitPopUntil;
    const float SquashSeconds = 0.15f;
    const float DustStreakWidth = 0.08f;
    static readonly Color DustColor = new(0.8f, 0.76f, 0.65f, 0.8f);
    static readonly Color StarColor = new(1f, 0.9f, 0.3f);
    const int StarCount = 3;
    const float DizzyWobbleDegrees = 18f;
    float squashUntil;
    Transform[] stars;
    const int HitFlashTint = -2;
    // Burn and zone damage arrives every frame, so it shows as one number per this many seconds.
    const float DotNumberSeconds = 1f;
    const float DamageNumberHeight = 0.4f;
    const float DamageNumberSpread = 0.2f;
    static readonly Color ResistedNumberColor = new(0.6f, 0.6f, 0.6f);
    float dotDamage;
    float dotNumberTimer;
    Color dotNumberColor;

    float MoveSpeed => Data.moveSpeed * (HasStatus(StatusKind.Slow) ? StatusRules.SlowSpeedMultiplier : 1f);
    float AttackIntervalMultiplier => HasStatus(StatusKind.Stun) ? StatusRules.StunAttackIntervalMultiplier : 1f;

    public void Init(EnemyData data, Vector3[] pathPoints, float healthMultiplier)
    {
        Data = data;
        path = pathPoints;
        nextPoint = 1;
        MaxHealth = Health = data.maxHealth * healthMultiplier;
        transform.position = path[0];
        ApplyArt(data);
        healthBar.SetFraction(1f);
        statusIcons = StatusIcons.Create(transform, new Vector3(0f, StatusIconHeight, 0f), body.sortingOrder + 1);
        AddStunStars();
        if (data.hasWeakness) AddWeaknessMarker();
        IsTargetable = true;
        Active.Add(this);
    }

    // Drawn art shows as painted and scales with size; without it the body is a coloured square.
    void ApplyArt(EnemyData data)
    {
        if (data.sprite != null)
        {
            body.sprite = data.sprite;
            body.transform.localScale = Vector3.one * (data.size / ArtSize);
            baseColor = Color.white;
        }
        else
        {
            body.transform.localScale = Vector3.one * data.size;
            baseColor = data.color;
        }
        body.color = baseColor;
        bodyScale = body.transform.localScale;
    }

    public bool IsWeakTo(Element element) => Data.hasWeakness && Data.weakness == element;

    // A small dot in the weakness element's status colour above the body, so players can read
    // weaknesses in grey-box. Created at runtime so the enemy prefab needs no change.
    void AddWeaknessMarker()
    {
        var marker = new GameObject("Weakness Marker");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(-0.3f, 0.45f, 0f);
        marker.transform.localScale = Vector3.one * WeaknessMarkerSize;
        var renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = SpriteUtil.Square;
        renderer.color = StatusRules.TintOf(StatusRules.ForElement(Data.weakness));
        renderer.sortingOrder = body.sortingOrder + 1;
    }

    // Instant shot at the closest deployed soldier in range while walking, on the soldier attack
    // timer. A thin streak shows the shot. Only runs while the enemy walks, like tower attacks.
    void TickRangedAttack()
    {
        if (Data.rangedAttackRange <= 0f) return;
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;
        Soldier target = Soldier.FindDeployedNear(transform.position, Data.rangedAttackRange);
        if (target == null) return;
        target.TakeDamage(Data.attackDamage);
        attackTimer = Data.attackInterval * AttackIntervalMultiplier;

        Vector2 shot = target.transform.position - transform.position;
        AbilityZone.Spawn((Vector2)transform.position + shot * 0.5f, Mathf.Atan2(shot.y, shot.x) * Mathf.Rad2Deg,
            new Vector3(shot.magnitude, ShotStreakWidth, 1f), SpriteUtil.Square, Data.color, ShotStreakSeconds, 0f);
    }

    void Update()
    {
        if (!IsTargetable)
        {
            // Only reached while lingering after an ability kill; see Remove.
            if (Time.unscaledTime >= hitFlashUntil) Destroy(gameObject);
            return;
        }

        TickStatuses();
        if (!IsTargetable) return;
        UpdateBodyLook();

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
        TickRangedAttack();

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

    // Reapplying a status refreshes its timer rather than stacking. Burn keeps the strongest
    // caster, who is credited if the burn lands the killing blow.
    public void ApplyStatus(StatusKind kind, float casterPower, Tower source = null)
    {
        if (!IsTargetable) return;
        if (kind == StatusKind.Burn)
        {
            float damagePerSecond = casterPower * StatusRules.BurnPowerPerSecond;
            if (!HasStatus(StatusKind.Burn) || damagePerSecond >= burnDamagePerSecond)
            {
                burnDamagePerSecond = damagePerSecond;
                burnSource = source;
            }
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
        statusIcons.Refresh(statusTime);

        if (HasStatus(StatusKind.Burn))
            TakeDamage(burnDamagePerSecond * Time.deltaTime, burnSource, true, Element.Fire);
        if (IsTargetable && dotDamage > 0f && (dotNumberTimer += Time.deltaTime) >= DotNumberSeconds) FlushDot();
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
        else body.color = tint < 0 ? baseColor : Color.Lerp(baseColor, StatusRules.TintOf((StatusKind)tint), 0.6f);
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

    // source is the tower whose soldier, ability or burn dealt the damage, if any; the one that
    // lands the killing blow is remembered in KilledBy for XP.
    // overTime marks damage dealt a slice per frame (burn, zones), which shows as a periodic
    // number instead of one per call. The number is coloured by the source's element, or by
    // numberElement when there is no source.
    public void TakeDamage(float amount, Tower source = null, bool overTime = false, Element? numberElement = null)
    {
        if (!IsTargetable) return;
        bool weak = source != null && IsWeakTo(source.Captain.element);
        if (weak) amount *= StatusRules.WeaknessDamageMultiplier;
        Health -= amount;
        healthBar.SetFraction(Health / MaxHealth);
        Element? element = source != null ? source.Captain.element : numberElement;
        if (overTime) AccumulateDot(amount, element);
        else
        {
            ShowDamageNumber(amount, element, weak);
            FlashHit(HitPopSeconds);
            hitPopUntil = Time.unscaledTime + HitPopSeconds;
        }
        if (Health > 0f) return;
        FlushDot();
        KilledBy = source;
        Die();
    }

    Color NumberColor(Element? element) =>
        element.HasValue && Data.hasWeakness && element.Value == StatusRules.OppositeOf(Data.weakness) ? ResistedNumberColor
        : element.HasValue ? UIKit.ElementColor(element.Value) : Color.white;

    Vector3 NumberPosition() =>
        transform.position + new Vector3(Random.Range(-DamageNumberSpread, DamageNumberSpread), DamageNumberHeight, 0f);

    void ShowDamageNumber(float amount, Element? element, bool weak) =>
        FloatingText.Spawn(NumberPosition(), Mathf.Max(1, Mathf.RoundToInt(amount)) + (weak ? "!" : ""),
            NumberColor(element), weak ? 1.5f : 1f);

    void AccumulateDot(float amount, Element? element)
    {
        dotDamage += amount;
        dotNumberColor = NumberColor(element);
    }

    void FlushDot()
    {
        if (dotDamage <= 0f) return;
        FloatingText.Spawn(NumberPosition(), Mathf.Max(1, Mathf.RoundToInt(dotDamage)).ToString(), dotNumberColor);
        dotDamage = 0f;
        dotNumberTimer = 0f;
    }

    // Pushes the enemy back along its path (never before the start); it then walks on as usual.
    public void Knockback(float distance)
    {
        if (!IsTargetable || !(distance > 0f)) return;
        Vector3 from = transform.position;
        transform.position = PathMath.MoveBack(path, nextPoint, transform.position, distance, out nextPoint);
        ShowKnockback(from);
    }

    // Three small stars that circle above the head while stunned; shown only then.
    void AddStunStars()
    {
        stars = new Transform[StarCount];
        for (int i = 0; i < StarCount; i++)
        {
            var star = new GameObject("Stun Star");
            star.transform.SetParent(transform, false);
            star.transform.localScale = Vector3.one * 0.1f;
            star.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var renderer = star.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteUtil.Square;
            renderer.color = StarColor;
            renderer.sortingOrder = body.sortingOrder + 1;
            star.SetActive(false);
            stars[i] = star.transform;
        }
    }

    // Body squash and hit swell, the dizzy wobble, and the stun stars, each shown only while it applies.
    void UpdateBodyLook()
    {
        Vector3 scale = bodyScale;
        if (Time.unscaledTime < hitPopUntil) scale *= HitPopScale;
        float squash = Mathf.Clamp01((squashUntil - Time.unscaledTime) / SquashSeconds);
        if (squash > 0f) scale = Vector3.Scale(scale, new Vector3(1f - 0.3f * squash, 1f + 0.25f * squash, 1f));
        body.transform.localScale = scale;

        body.transform.localRotation = HasStatus(StatusKind.Dizzy)
            ? Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 14f) * DizzyWobbleDegrees)
            : Quaternion.identity;

        bool stunned = HasStatus(StatusKind.Stun);
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].gameObject.SetActive(stunned);
            if (!stunned) continue;
            float angle = Time.time * 6f + Mathf.PI * 2f * i / stars.Length;
            stars[i].localPosition = new Vector3(Mathf.Cos(angle) * 0.28f, 0.3f + Mathf.Sin(angle) * 0.07f, 0f);
        }
    }

    // The knockback squashes the body against the push and leaves a dust streak and a few puffs
    // where the enemy was.
    void ShowKnockback(Vector3 from)
    {
        squashUntil = Time.unscaledTime + SquashSeconds;
        Vector2 push = transform.position - from;
        if (push.sqrMagnitude < 0.0001f) return;
        AbilityZone.Spawn((Vector2)from + push * 0.5f, Mathf.Atan2(push.y, push.x) * Mathf.Rad2Deg,
            new Vector3(push.magnitude, DustStreakWidth, 1f), SpriteUtil.Square, DustColor, ShotStreakSeconds, 0f);
        Burst.Spawn(from, DustColor, 4, 0.8f, 0.08f, 0.25f);
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

    // The enemy leaves play at once (no targeting, blocking or wave count), but one still showing
    // an ability-hit flash stays on screen until the flash, and so the hit-stop, is over.
    void Remove()
    {
        IsTargetable = false;
        Active.Remove(this);
        if (Time.unscaledTime < hitFlashUntil)
        {
            healthBar.gameObject.SetActive(false);
            statusIcons.gameObject.SetActive(false);
        }
        else
            Destroy(gameObject);
    }

    void OnDestroy() => Active.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active.Clear();
}
