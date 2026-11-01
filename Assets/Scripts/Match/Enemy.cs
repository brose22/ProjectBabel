using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> Active = new();

    const float ArmorMarkerSize = 0.15f;
    const float ShotStreakWidth = 0.05f;
    const float ShotStreakSeconds = 0.12f;
    const float StatusIconHeight = 0.7f;
    // Enemy art is drawn for this size; other sizes scale the sprite.
    const float ArtSize = 0.6f;

    [SerializeField] SpriteRenderer body;
    [SerializeField] HealthBar healthBar;

    public EnemyData Data { get; private set; }
    // True for the enemy base's boss, set by the match once it has walked on.
    public bool IsBoss { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    // False once the enemy has died or leaked, even before the GameObject is destroyed.
    public bool IsTargetable { get; private set; }
    // The tower credited with the kill; null for leaks or kills with no tower behind them.
    public Tower KilledBy { get; private set; }
    // Where the enemy walked on.
    public Vector3 SpawnPoint => path[0];

    Vector3[] path;
    int nextPoint;
    // Where the enemy is on the road's centre line; transform.position adds the sideways lane offset to it.
    Vector3 lanePosition;
    float laneOffset;
    Vector2 laneNormal;
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
    // Most an enemy strays sideways from the road's centre line, in tiles; each picks its own lane.
    const float LaneSpread = 0.3f;
    // How fast the lane direction eases round a corner, per second.
    const float LaneTurnSpeed = 10f;
    float squashUntil;
    // Game seconds each pose of the walk cycle shows.
    const float WalkPoseSeconds = 0.18f;
    float walkTime;
    // Armor labels show at most this often so rapid hits do not stack them.
    const float ArmorLabelGapSeconds = 0.6f;
    float armorLabelUntil;
    Transform[] stars;
    const int HitFlashTint = -2;
    // Burn and zone damage arrives every frame, so it shows as one number per this many seconds.
    const float DotNumberSeconds = 1f;
    const float DamageNumberHeight = 0.4f;
    const float DamageNumberSpread = 0.2f;
    const float DamageNumberSize = 1.5f;
    const float StrongNumberSize = 2.25f;
    const float ArmorLabelSize = 1.2f;
    static readonly Color ResistedNumberColor = new(0.6f, 0.6f, 0.6f);
    float dotDamage;
    float dotNumberTimer;
    Color dotNumberColor;

    float MoveSpeed => Data.moveSpeed * (HasStatus(StatusKind.Slow) ? StatusRules.SlowSpeedMultiplier : 1f);
    float AttackIntervalMultiplier => HasStatus(StatusKind.Stun) ? StatusRules.StunAttackIntervalMultiplier : 1f;

    // Gold paid into the shared pool when this enemy dies, after the run's per-layer growth.
    public int GoldReward { get; private set; }

    public void Init(EnemyData data, Vector3[] pathPoints, float healthMultiplier, float goldMultiplier = 1f)
    {
        Data = data;
        GoldReward = Mathf.RoundToInt(data.goldReward * goldMultiplier);
        path = pathPoints;
        nextPoint = 1;
        MaxHealth = Health = data.maxHealth * healthMultiplier;
        lanePosition = path[0];
        laneOffset = Random.Range(-LaneSpread, LaneSpread);
        laneNormal = PathMath.LeftNormal(path[0], path[1]);
        ApplyLane();
        ApplyArt(data);
        // Each starts at its own point in the cycle so a crowd doesn't march in step.
        walkTime = Random.Range(0f, 4f * WalkPoseSeconds);
        healthBar.UseEnemyStyle(Data.elite ? 1 : 0);
        healthBar.SetFraction(1f);
        statusIcons = StatusIcons.Create(transform, new Vector3(0f, StatusIconHeight, 0f), body.sortingOrder + 1);
        AddStunStars();
        if (data.armor != ArmorType.Unarmored) AddArmorMarker();
        IsTargetable = true;
        Active.Add(this);
        if (data.elite) GameEvents.RaiseEnemyArrived(this);
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

    public void MarkBoss()
    {
        IsBoss = true;
        GameEvents.RaiseEnemyArrived(this);
        healthBar.SetTier(2);
    }

    // A small square above the body in the armor's colour (steel for Armored, teal for Swift), so
    // players can read armor in grey-box. Created at runtime so the enemy prefab needs no change.
    void AddArmorMarker()
    {
        var marker = new GameObject("Armor Marker");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(-0.3f, 0.45f, 0f);
        marker.transform.localScale = Vector3.one * ArmorMarkerSize;
        var renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = SpriteUtil.Square;
        renderer.color = Data.armor == ArmorType.Armored ? new Color(0.7f, 0.74f, 0.82f) : new Color(0.35f, 0.9f, 0.85f);
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
            ShowWalkPose(false);
            return;
        }

        TickTowerAttack();
        TickRangedAttack();
        ShowWalkPose(true);

        float step = MoveSpeed * Time.deltaTime;
        if (HasStatus(StatusKind.Dizzy))
        {
            lanePosition = PathMath.MoveBack(path, nextPoint, lanePosition, step, out nextPoint);
            ApplyLane();
            return;
        }

        Vector3 target = path[nextPoint];
        lanePosition = Vector3.MoveTowards(lanePosition, target, step);
        ApplyLane();
        if (lanePosition == target && ++nextPoint >= path.Length)
            Leak();
    }

    // Places the enemy beside the centre line: its lane offset along the sideways direction of the
    // segment it is on, which eases round corners. Everything that reads transform.position (blocking,
    // soldier reach, targeting) sees the enemy where it is drawn.
    void ApplyLane()
    {
        int next = Mathf.Clamp(nextPoint, 1, path.Length - 1);
        laneNormal = PathMath.EaseNormal(laneNormal, PathMath.LeftNormal(path[next - 1], path[next]), LaneTurnSpeed * Time.deltaTime);
        transform.position = lanePosition + (Vector3)(laneNormal * laneOffset);
    }

    // Steps through the walk cycle, standing between steps; a blocked enemy just stands.
    void ShowWalkPose(bool walking)
    {
        Sprite[] frames = Data.walkFrames;
        if (frames == null || frames.Length == 0 || Data.sprite == null) return;
        if (walking) walkTime += Time.deltaTime;
        int pose = Mathf.FloorToInt(walkTime / WalkPoseSeconds);
        body.sprite = !walking || pose % 2 == 0 ? Data.sprite : frames[pose / 2 % frames.Length];
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
        if (seconds <= 0f || !GameSettings.HitFlash) return;
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
    // numberElement when there is no source. soldierClass, when a soldier landed the hit, lets the
    // enemy's armor scale it.
    public void TakeDamage(float amount, Tower source = null, bool overTime = false, Element? numberElement = null,
        SoldierClassData soldierClass = null)
    {
        if (!IsTargetable) return;
        float counter = ArmorMultiplier(soldierClass);
        amount *= counter;
        Element? element = source != null ? source.Captain.element : numberElement;
        GameEvents.RaiseDamageDealt(source, Mathf.Min(amount, Health), element);
        if (IsBoss && source != null) GameEvents.RaiseBossDamaged(source);
        if (source != null && !overTime) source.AddFightCharge(FightCharge(amount));
        Health -= amount;
        healthBar.SetFraction(Health / MaxHealth);
        if (overTime) AccumulateDot(amount, element);
        else
        {
            ShowDamageNumber(amount, element, counter);
            ShowArmorLabel(counter);
            FlashHit(HitPopSeconds);
            if (GameSettings.HitFlash) hitPopUntil = Time.unscaledTime + HitPopSeconds;
        }
        if (Health > 0f) return;
        FlushDot();
        KilledBy = source;
        if (source != null) source.AddFightCharge(ChargeMath.ForKill(Data.elite, IsBoss));
        Die();
    }

    // Charge a soldier hit earns its captain: the health it removed, plus a bonus for hitting an
    // enemy another captain has already afflicted.
    float FightCharge(float damage)
    {
        float charge = ChargeMath.ForDamage(Mathf.Min(damage, Health), MaxHealth);
        for (int i = 0; i < statusTime.Length; i++)
        {
            if (statusTime[i] <= 0f) continue;
            charge += ChargeMath.AfflictedHitBonus;
            break;
        }
        return charge;
    }

    // Damage multiplier this enemy's armor gives a soldier class: below 1 when the armor counters
    // the class, above 1 when the class counters the armor. 1 for anything that is not a soldier.
    float ArmorMultiplier(SoldierClassData soldierClass) =>
        soldierClass != null && Relationships.TryGetClassTag(soldierClass, out CaptainTag tag)
            ? ArmorRules.DamageMultiplier(Data.armor, (int)tag)
            : 1f;

    Color NumberColor(Element? element, bool resisted = false) =>
        resisted ? ResistedNumberColor
        : element.HasValue ? UIKit.ElementColor(element.Value) : Color.white;

    Vector3 NumberPosition() =>
        transform.position + new Vector3(Random.Range(-DamageNumberSpread, DamageNumberSpread), DamageNumberHeight, 0f);

    void ShowDamageNumber(float amount, Element? element, float counter)
    {
        if (!GameSettings.DamageNumbers) return;
        bool strong = counter > 1f;
        FloatingText.Spawn(NumberPosition(), Mathf.Max(1, Mathf.RoundToInt(amount)) + (strong ? "!" : ""),
            NumberColor(element, counter < 1f), strong ? StrongNumberSize : DamageNumberSize);
    }

    // "Strong!" or "Resist" above the enemy when its armor counters the hit or is countered by it,
    // at most once every ArmorLabelGapSeconds so rapid hits do not stack labels.
    void ShowArmorLabel(float counter)
    {
        if (Mathf.Approximately(counter, 1f) || !GameSettings.ArmorLabels) return;
        if (Time.unscaledTime < armorLabelUntil) return;
        armorLabelUntil = Time.unscaledTime + ArmorLabelGapSeconds;
        FloatingText.Spawn(transform.position + Vector3.up * 0.65f, counter > 1f ? "Strong!" : "Resist",
            counter > 1f ? UIKit.AccentColor : ResistedNumberColor, ArmorLabelSize, 0.9f);
    }

    void AccumulateDot(float amount, Element? element)
    {
        dotDamage += amount;
        dotNumberColor = NumberColor(element);
    }

    void FlushDot()
    {
        if (dotDamage <= 0f) return;
        if (GameSettings.DamageNumbers)
            FloatingText.Spawn(NumberPosition(), Mathf.Max(1, Mathf.RoundToInt(dotDamage)).ToString(), dotNumberColor, DamageNumberSize);
        dotDamage = 0f;
        dotNumberTimer = 0f;
    }

    // Pushes the enemy back along its path (never before the start); it then walks on as usual.
    // The boss cannot be pushed. source, if given, earns special charge for a push that lands.
    public void Knockback(float distance, Tower source = null)
    {
        if (!IsTargetable || IsBoss || !(distance > 0f)) return;
        Vector3 from = transform.position;
        lanePosition = PathMath.MoveBack(path, nextPoint, lanePosition, distance, out nextPoint);
        ApplyLane();
        ShowKnockback(from);
        if (source != null) source.AddFightCharge(ChargeMath.KnockbackBonus);
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

        body.transform.localRotation = GameSettings.DizzyWobble && HasStatus(StatusKind.Dizzy)
            ? Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 14f) * DizzyWobbleDegrees)
            : Quaternion.identity;

        bool stunned = GameSettings.StunStars && HasStatus(StatusKind.Stun);
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
        if (!GameSettings.KnockbackEffects) return;
        squashUntil = Time.unscaledTime + SquashSeconds;
        Vector2 push = transform.position - from;
        if (push.sqrMagnitude < 0.0001f) return;
        AbilityZone.Spawn((Vector2)from + push * 0.5f, Mathf.Atan2(push.y, push.x) * Mathf.Rad2Deg,
            new Vector3(push.magnitude, DustStreakWidth, 1f), SpriteUtil.Square, DustColor, ShotStreakSeconds, 0f);
        Burst.Spawn(from, DustColor, 4, 0.8f, 0.08f, 0.25f);
    }

    // A pop of squares in the enemy colour and a floating gold reward where it fell.
    void ShowDeath()
    {
        if (GameSettings.DeathBurst) Burst.Spawn(transform.position, Data.color, 8, 1.8f, 0.14f, 0.4f);
        if (GameSettings.DamageNumbers) FloatingText.Spawn(transform.position + Vector3.up * 0.2f, "+" + GoldReward, UIKit.AccentColor, 1.2f, 1f);
    }

    void Die()
    {
        ShowDeath();
        Remove();
        RunState.Current?.AddGold(GoldReward);
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
